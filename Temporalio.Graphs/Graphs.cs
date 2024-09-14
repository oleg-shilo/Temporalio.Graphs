using System.Runtime.CompilerServices;
using static System.Environment;
using Temporalio.Activities;
using System.Reflection;

[assembly: InternalsVisibleTo("Temporalio.Graphs.Tests")]

namespace Temporalio.Graphs;

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
    public string AddDecision(string id, bool value, [CallerMemberName] string name = "", string resultText = "yes|no")
    {
        var resultValues = resultText.Split("|");
        if (resultValues.Length != 2)
            throw new ArgumentException($"The {nameof(resultText)} must contain two values separated by '|'.");
        Elements.Add($"{id}{{{name}}}:{(value ? resultValues[0] : resultValues[1])}");
        return $"DAG decision: {name}"; // will be used as a WF runtime log entry
    }
}

public class GraphGenerator
{
    public List<List<string>> Scenarios = new();

    public void PrittyfyNodes()
    {
        // remove string hash based IDs with a simplified numeric ID that are just indexes of the nodes in the graph
        var idMap = Scenarios
            .SelectMany(path => path.Select(node => node))
            .Where(x => x.Contains("{"))
            .Distinct()
            .Select((x, i) => new { Id = x.Split("{").First(), SimplifiedId = i.ToString() })
            .DistinctBy(x => x.Id)
            .ToDictionary(x => x.Id, y => y.SimplifiedId);

        for (int i = 0; i < Scenarios.Count; i++)
        {
            for (int j = 0; j < Scenarios[i].Count; j++)
            {
                var node = Scenarios[i][j];
                if (node.Contains("{"))
                {
                    var id = node.Split("{").First();
                    var simplifiedId = idMap[id];
                    Scenarios[i][j] = node.Replace(id, simplifiedId);
                }
            }
        }
    }

    public string ToPaths()
        => Scenarios
        .Select(x => x.Select(x => x.ToSimpleNodeName().SplitByWords())
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
                .Select(x => x.Select(x => x.ToSimpleNodeName().SplitByWords(isMermaid: true)).ToList())
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
                    .Replace("}:", "} -- ")
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

        var missingActivities = allActivities
            .Where(x => !allGraphElements.Contains(x))
            .ToList();

        var message = "";

        if (!missingActivities.IsEmpty())
            message = $"WARNING: the following activities are not present in the full WF graph: " +
                      $"{NewLine}{missingActivities.JoinBy($",{NewLine}")}";

        return message;
    }
}
