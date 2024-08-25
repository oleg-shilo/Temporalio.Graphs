using System.Runtime.CompilerServices;
using static System.Environment;
using Temporalio.Activities;
using System.Reflection;

namespace Temporalio.Graph;

public class Graph
{
    public List<string> Elements = new();
    public string AddStep([CallerMemberName] string name = "")
    {
        Elements.Add(name.TrimEnd("Async"));
        return $"Generating DAG: {name}"; // will be used as a WF runtime log entry
    }
    public void AddDecision(string id, bool value, [CallerMemberName] string name = "")
        => Elements.Add($"{id}{{{name}}}:{(value ? "yes" : "no")}");
}

public class DagGenerator
{
    public List<List<string>> Scenarios = new();
    public string ToGraphs() => Scenarios.Select(x => x.JoinBy(" > ")).JoinBy(NewLine);

    public string ToMermaidSyntax()
    {
        if (Scenarios.IsEmpty())
            return "";

        try
        {
            var uniqueGraphs = Scenarios.OrderByDescending(x => x.Count);

            var mermaidDefinition = new List<string>
            {
                "```mermaid",
                "flowchart LR"
            };
            // the longest graph goes first and then adding only the unique step sequences
            mermaidDefinition.Add(uniqueGraphs.First().JoinBy(" --> "));

            bool isCapturedAlready(string name)
                => mermaidDefinition.Any(x => x.Contains(name));

            foreach (List<string> graphElements in uniqueGraphs.Skip(1))
            {
                // 1. extracting unique sequence of elements from this graph that is not present in other graphs
                // 2. compose a unique sub-graph:   <unique elements> <first non-unique element>

                for (int i = 0; i < graphElements.Count; i++)
                {
                    var currentElement = graphElements[i];
                    if (!isCapturedAlready(currentElement))
                    {
                        // it's the first element that is not captured yet (unique element)
                        var uniqueSequence = graphElements.Skip(i).Take(1).ToList();
                        var remainingElements = graphElements.Skip(i + 1);
                        foreach (var item in remainingElements)
                        {
                            uniqueSequence.Add(item);
                            if (isCapturedAlready(item))
                            {
                                // it's the first remaining element that is not unique
                                var lastElement = uniqueSequence.Last();
                                // if it is a condition element then remove the condition value.
                                // the sequence should end with the element name
                                uniqueSequence[uniqueSequence.Count - 1] = lastElement.TrimEnd(":no", ":yes");

                                mermaidDefinition.Add(uniqueSequence.JoinBy(" --> "));
                                break;
                            }
                        }

                    }
                }
            }
            mermaidDefinition.Add("```");

            string EnsureSyntax(string text)
            {
                // converting to mermaid syntax
                // `--> decision0{IsTFN_Known}:yes -->` into  `--> decision0{IsTFN_Known} -- yes -->`
                // and make start/end into a circle shape
                return text
                    .Replace("}:no", "} -- no")
                    .Replace("}:yes", "} -- yes")
                    .Replace("Start -->", "s((Start)) -->")
                    .Replace("--> End", "--> e((End))");
            }

            mermaidDefinition = mermaidDefinition.Select(EnsureSyntax).ToList();

            return mermaidDefinition.JoinBy(NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Cannot generate mermaid Diagram: " + ex.Message);
            return "";
        }
    }

    public static async Task<DagGenerator> Play(Func<Task<string>> action, Action[] decisionsProfiles)
    {
        // Generating the complete diagram (DAG) based on the analysts of all unique path graphs.
        // MermaidGenerator will print diagram at disposal
        var generator = new DagGenerator();

        foreach (Action setProfile in decisionsProfiles)
        {
            setProfile();

            using (new GraphRecorder(redirectTo: generator.Scenarios)) // single scenario graph recorder
            {
                try
                {
                    await action();
                }
                catch { /* ignore any error as we are mocking the execution */ }
            }
        }

        return generator;
    }
}
public static class DagValidator
{
    public static string ValidateAgainst<T>(this DagGenerator dag)
    {
        var allGraphElements = dag.Scenarios.SelectMany(x => x).Distinct().ToArray();

        var allAassemblyTypes = typeof(T).Assembly.GetTypes();

        var allActivities = allAassemblyTypes
            .SelectMany(x => x.GetMethods())
            .Where(x => x.GetAttributes<ActivityAttribute>().Any())
            .Select(x => new
            {
                Type = x.DeclaringType,
                Member = x.Name,
                ElementName = x.Name.TrimEnd("Async")
            });

        var allDecisions = allAassemblyTypes.SelectMany(x => x.GetProperties())
            .Where(x => x.GetAttributes<DecisionAttribute>().Any())
            .Select(x => new
            {
                Type = x.DeclaringType,
                ElementName = x.Name
            });

        var missingActivities = allActivities
            .Where(x => !allGraphElements.Contains(x.ElementName))
            .Select(x => $"{x.Type}.{x.Member}");

        var missingDecisions = allDecisions
            .Where(x =>
            {
                var yesDecision = $"{{{x.ElementName}}}:yes";
                var noDecision = $"{{{x.ElementName}}}:no";

                return
                    !allGraphElements.Any(y => y.Contains(yesDecision)) ||
                    !allGraphElements.Any(y => y.Contains(noDecision));
            }
            )
            .Select(x => $"{x.Type}.{x.ElementName}");

        var message = "";

        if (!missingActivities.IsEmpty())
            message =
                $"""
                WARNING: the following activities are not present in the full DAG: [{missingActivities.JoinBy(", ")}] 
                This is either because the activity was not run during the WF execution or because the activity does not have the following statement as the first line in the implementation block:
                    if (Dag.IsBuildingGraph)
                        return Dag.ActiveGraph.AddStep();

                """;
        if (!missingDecisions.IsEmpty())
            message +=
                $"""
                WARNING: the following decisions are not present in the full DAG: [{missingDecisions.JoinBy(", ")}]
                This is either because the decision or its all permutations were not evaluated during the WF execution or because it was not implemented correctly.
                See Temporalio.Graphs samples.
                """;

        return message;
    }
}

public class GraphRecorder : IDisposable
{
    List<List<string>> redirectTo;

    public GraphRecorder(List<List<string>> redirectTo)
    {
        this.redirectTo = redirectTo;
        Dag.IsBuildingGraph = true;
        Dag.ActiveGraph.Elements.Clear();
        Dag.ActiveGraph.AddStep("Start");
    }

    public void Dispose()
    {
        Dag.ActiveGraph.AddStep("End");
        redirectTo?.AddGraph(Dag.ActiveGraph.Elements);
    }
}

public static class Dag
{
    public static Graph ActiveGraph = new();
    public static bool IsBuildingGraph = false;
}

static class Extensions
{
    public static string TrimEnd(this string text, params string[] trimText)
    {
        var result = text;
        foreach (var pattern in trimText)
            if (result.EndsWith(pattern))
                result = result.Substring(0, result.Length - pattern.Length);
        return result;
    }
    public static string JoinBy(this IEnumerable<string> items, string separator)
        => string.Join(separator, items);
    public static bool IsEmpty<T>(this IEnumerable<T> items)
        => items.Count() == 0;
    public static void AddGraph(this List<List<string>> scenarios, IEnumerable<string> elements)
        => scenarios.Add(elements.ToList());

    public static T[] GetAttributes<T>(this MemberInfo info, bool inherit = true)
        => info.GetCustomAttributes(typeof(T), inherit).Cast<T>().ToArray();
}