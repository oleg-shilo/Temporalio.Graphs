using System.Runtime.CompilerServices;
using static System.Environment;
using Temporalio.Activities;
using System.Reflection;

[assembly: InternalsVisibleTo("Temporalio.Graphs.Tests")]

namespace Temporalio.Graphs;


public class GraphPath
{
    /// <summary>
    /// The elements (steps/nodes) of te path.
    /// </summary>
    public List<string> Elements = new();
    /// <summary>
    /// Clears all steps/nodes of the path.
    /// </summary>
    /// <returns></returns>
    public GraphPath Clear()
    {
        Elements.Clear();
        return this;
    }
    /// <summary>
    /// Adds the step/node to the graph.
    /// </summary>
    /// <param name="name">The step name.</param>
    /// <returns></returns>
    public string AddStep(string name = "")
    {
        Elements.Add(name);
        return $"DAG step: {name}"; // will be used as a WF runtime log entry
    }

    /// <summary>
    /// Adds a decision node to the graph.
    /// </summary>
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
    internal List<List<string>> Scenarios = new();

    /// <summary>
    /// Improves readability of the nodes by replacing string hash based IDs with a simplified numeric ID.
    /// Numeric ID is just indexes of the nodes in the graph.
    /// </summary>
    public void PrittyfyNodes()
    {
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

    /// <summary>
    /// Converts <see cref="GraphGenerator.Scenarios"/> into a collection of individual paths, where path is a sequence of the '>' separated step names.
    /// </summary>
    /// <returns></returns>
    public string ToPaths()
        => Scenarios
        .Select(x => x.Select(x => x.ToSimpleNodeName().SplitByWords())
                      .JoinBy(" > "))
        .JoinBy(NewLine);

    /// <summary>
    /// Converts <see cref="GraphGenerator.Scenarios"/> into Mermaid syntax for flowchart. Each line of Mermaid definition is a single pair of two node transition.
    /// </summary>
    /// <returns></returns>
    public string ToMermaidSyntax()
    {
        if (Scenarios.IsEmpty())
            return "";

        try
        {
            var uniqueGraphs = Scenarios
                .Select(x => x.Select(x => x.ToSimpleNodeName().SplitByWords(isMermaid: true).DecorateSignals()).ToList())
                .OrderByDescending(x => x.Count);

            var mermaidDefinition = new List<string>
            {
                "```mermaid",
                "flowchart LR"
            };

            foreach (List<string> path in uniqueGraphs)
            {
                for (int i = 1; i < path.Count; i++) // will have at least two nodes: start and end
                {
                    var start = path[i - 1].Trim();
                    var end = path[i].Split(":").First().Trim();
                    var transition = $"{start} --> {end}";
                    if (!mermaidDefinition.Contains(transition))
                        mermaidDefinition.Add(transition);
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

    /// <summary>
    /// Converts <see cref="GraphGenerator.Scenarios"/> into Mermaid syntax for flowchart. 
    /// The Mermaid definition is a expressed in the compact form (minimal number of lines).
    /// </summary>
    /// <returns></returns>
    public string ToMermaidCompactSyntax()
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
                .Select(x => x.Select(x => x.ToSimpleNodeName().SplitByWords(isMermaid: true).DecorateSignals()).ToList())
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
static class DagValidator
{
    /// <summary>
    /// Validates the graph against activities defined in the assembly. Warns if some activities found in the assembly are not 
    /// present in the graph.
    /// </summary>
    /// <param name="graph">The workflow graph generator instance with the all workflow paths.</param>
    /// <param name="assembly">The assembly.</param>
    /// <returns></returns>
    public static string ValidateGraphAgainst(this GraphGenerator graph, Assembly assembly)
    {
        var allGraphElements = graph.Scenarios.SelectMany(x => x).Distinct().ToArray();

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
