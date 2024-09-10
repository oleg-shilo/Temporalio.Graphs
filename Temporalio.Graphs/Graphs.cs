using System.Runtime.CompilerServices;
using static System.Environment;
using Temporalio.Activities;
using System.Reflection;
using System.Linq.Expressions;

[assembly: InternalsVisibleTo("Temporalio.Graphs.Tests")]

namespace Temporalio.Graphs;
public class DecisionAttribute : Attribute
{
    // can add a member to store Decision description
    public string PositiveValue { get; set; } = true.ToString();
    public string NegativeValue { get; set; } = false.ToString();
}
public class GenericDecisionActivity
{
    [Activity]
    [Decision]
    public bool TakeDecision(bool result, string name)
    {
        return result;
    }

    static string GetMemberName(Expression<Func<bool>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        throw new InvalidOperationException("Expression is not a member access");
    }
}

public class GraphPath
{
    public List<string> Elements = new();
    public GraphPath Clear()
    {
        Elements.Clear();
        return this;
    }
    public string AddStep(string name = "")
    {
        Elements.Add(name);
        return $"DAG step: {name}"; // will be used as a WF runtime log entry
    }
    public string AddDecision(string id, bool value, [CallerMemberName] string name = "")
    {
        Elements.Add($"{id}{{{name}}}:{(value ? "yes" : "no")}");
        return $"DAG decision: {name}"; // will be used as a WF runtime log entry
    }
}

public class GraphGenerator
{
    public List<List<string>> Scenarios = new();
    public string ToPaths()
        => Scenarios
        .Select(x => x.Select(x => x.ToSimpleMermaidName())
                      .JoinBy(" > "))
        .JoinBy(NewLine);

    public string ToMermaidSyntax()
    {
        if (Scenarios.IsEmpty())
            return "";

        // The following technique is used to reduce the number of steps in the graph:
        // - Find the longest graph and add it to the mermaid definition.
        // - Then for each remaining graph, find the unique sequence of elements that are not
        // present in other graphs.
        // 
        // The alternative technique would be much simpler but would result in a larger graph definition:
        // - break each graph path into the pairs (transitions) of node to another node transitions.
        // - add all transitions to the mermaid definition (ensure no duplication).
        // - the mermaid will automatically create a graph with all nodes and transitions.
        try
        {
            var uniqueGraphs = Scenarios
                .Select(x => x.Select(x => x.ToSimpleMermaidName()).ToList())
                .OrderByDescending(x => x.Count);

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
}
public static class DagValidator
{
    public static string ValidateGraphAgainst(this GraphGenerator dag, Assembly assembly)
    {
        var allGraphElements = dag.Scenarios.SelectMany(x => x).Distinct().ToArray();

        var allAassemblyTypes = assembly.GetTypes();

        var allActivities = allAassemblyTypes
            .SelectMany(x => x.GetMethods())
            .Where(x => x.GetAttributes<ActivityAttribute>().IsNotEmpty() &&
                        x.GetAttributes<DecisionAttribute>().IsEmpty())
            .Select(x => x.FullName());

        var allDecisions = allAassemblyTypes.SelectMany(x => x.GetProperties())
            .Where(x => x.GetAttributes<DecisionAttribute>().IsNotEmpty());

        var missingActivities = allActivities
            .Where(x => !allGraphElements.Contains(x))
            .ToList();

        var missingDecisions = allDecisions
            .Where(x =>
            {
                var yesDecision = $"{{{x}}}:yes";
                var noDecision = $"{{{x}}}:no";

                return
                    !allGraphElements.Any(y => y.Contains(yesDecision)) ||
                    !allGraphElements.Any(y => y.Contains(noDecision));
            })
            .Select(x => x.FullName())
            .ToArray();

        missingActivities.AddRange(missingDecisions);

        var message = "";

        if (!missingActivities.IsEmpty())
            message = $"WARNING: the following activities are not present in the full WF graph: " +
                      $"{NewLine}{missingActivities.JoinBy($",{NewLine}")}";

        return message;
    }
}
