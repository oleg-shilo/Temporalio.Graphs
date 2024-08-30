// @@@SNIPSTART money-transfer-project-template-dotnet-withdraw-activity

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Temporalio.Activities;
using Temporalio.Graphs;

namespace Temporalio.Graphs;

public class DecisionsBase
{
    public string Id([CallerMemberName] string name = "")
    {
        var indexOfProperty = this.GetType().GetProperties()
            .Where(p => p.GetAttributes<DecisionAttribute>().Any())
            .TakeWhile(p => p.Name != name)
            .Count();
        return $"decision{indexOfProperty}";
    }
}

public class DecisionAttribute : Attribute
{
    // can add a member to store Decision description
}

public static class GraphsExtensions
{
    public static MethodInfo GetActivityMethod(this ActivityDefinition activity)
    {
        dynamic invoker = activity.GetFieldValue("invoker");
        object target = invoker.Target;
        dynamic invoker1 = target.GetFieldValue("invoker");
        object target1 = invoker1.Target;
        var method = (MethodInfo)target1.GetFieldValue("method");
        return method;
    }
    internal static (Stack<bool>, string) GetDecisionExecutionPlan(this Dictionary<string, (Stack<bool>, string)> permutations, string decision)
    {
        // TODO: Implement this method by using reflection against an activity definition

        var key = decision;

        if (permutations.ContainsKey(key))
            return permutations[key];
        else if (permutations.ContainsKey(key += "Async"))
            return permutations[key];
        else
            return default;
    }

    public static Dictionary<string, (Stack<bool> Plan, string Id)> SetupPermutations(this (int Index, string Name)[] decisions)
    {
        var permutations = new Dictionary<string, (Stack<bool>, string)>();
        foreach (var decision in decisions)
        {
            permutations[decision.Name] = (new Stack<bool>(new[] { true, false }), $"decision{decision.Index}");
        }
        return permutations;
    }

    public static (int Index, string Name)[] GetDecisions(this Assembly assembly)
    {
        return assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods())
            .Where(p => p.GetCustomAttributes<DecisionAttribute>().Any())
            .Select((m, i) => (i, m.Name))
            .ToArray();
    }

    public static dynamic GetFieldValue(this object obj, string value, BindingFlags flag = BindingFlags.Default)
    {
        return obj
          .GetType()
          .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | flag)
          .FirstOrDefault(x => x.Name == value)
          .GetValue(obj);
    }

    public static dynamic GetPropValue(this object obj, string value, BindingFlags flag = BindingFlags.Default)
    {
        return obj
          .GetType()
          .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | flag)
          .FirstOrDefault(x => x.Name == value)
          .GetValue(obj);
    }


}