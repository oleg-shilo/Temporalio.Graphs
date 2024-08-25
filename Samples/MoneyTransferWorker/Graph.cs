using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static System.Environment;
using Temporalio.Activities;
using Temporalio.Exceptions;
using System.Diagnostics;
using System.Collections.Generic;

namespace Temporalio.Graph;

public class Graph
{
    public List<string> Elements = new();
    public string AddStep([CallerMemberName] string name = "")
    {
        Elements.Add(name.Replace("Async", ""));
        return $"Generating DAG: {name}";
    }
    public void AddDecision(string id, bool value, [CallerMemberName] string name = "")
        => Elements.Add($"{id}{{{name}}}:{(value ? "yes" : "no")}");
    public void Print(string context = "")
    {
        var graphs = Elements.JoinBy(" > ");
        Console.WriteLine(graphs);
    }
}

public class MermaidGenerator : IDisposable
{
    public List<List<string>> Scenarios = new();

    public void Print() => Console.WriteLine($"====================={NewLine}{Generate()}");

    public string Generate()
    {
        if (Scenarios.IsEmpty())
            return "";
        try
        {
            var uniqueGraphs = Scenarios.OrderByDescending(x => x.Count);

            var mermaidDefinition = new List<string>();

            // the longest graph goes first and then adding only the unique step sequences
            mermaidDefinition.Add("```mermaid");
            mermaidDefinition.Add("flowchart LR");
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

            // prettify the diagram
            mermaidDefinition = mermaidDefinition
                .Select(x => x
                    .Replace("}:no", "} -- no")
                    .Replace("}:yes", "} -- yes")
                    .Replace("Start -->", "s((Start)) -->")
                    .Replace("--> End", "--> e((End))"))
                .ToList();

            return mermaidDefinition.JoinBy(NewLine);
        }
        catch (Exception ex)
        {
            //Debug.Assert(false);
            Console.WriteLine("Cannot generate mermaid Diagram: " + ex.Message);
            return "";
        }
    }

    public void Dispose()
    {
        Print();
    }
}

public class GraphRecorder : IDisposable
{
    List<List<string>> redirectTo;

    public GraphRecorder(List<List<string>> redirectTo)
    {
        this.redirectTo = redirectTo;
        Runtime.IsBuildingGraph = true;
        Runtime.Graph.Elements.Clear();
        Runtime.Graph.AddStep("Start");
    }

    public void Dispose()
    {
        Runtime.Graph.AddStep("End");
        Runtime.Graph.Print();
        redirectTo?.AddGraph(Runtime.Graph.Elements);
    }
}

public static class Runtime
{
    public static Graph Graph = new();
    public static bool IsBuildingGraph = false;
}

public static class Extensions
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
}