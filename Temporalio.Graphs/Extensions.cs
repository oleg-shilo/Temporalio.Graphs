using System.Runtime.CompilerServices;
using static System.Environment;
using Temporalio.Activities;
using System.Reflection;

namespace Temporalio.Graphs;
static class GenericExtensions
{
    public static string TrimEnd(this string text, params string[] trimText)
    {
        var result = text;
        foreach (var pattern in trimText)
            if (result.EndsWith(pattern))
                result = result.Substring(0, result.Length - pattern.Length);
        return result;
    }
    public static T CastTo<T>(this object obj) => (T)obj;

    public static string JoinBy(this IEnumerable<string> items, string separator)
        => string.Join(separator, items);

    public static bool IsEmpty<T>(this IEnumerable<T> items)
        => items.Count() == 0;

    public static bool IsNotEmpty<T>(this IEnumerable<T> items)
        => items.Any();

    public static void AddPath(this List<List<string>> scenarios, GraphPath path)
        => scenarios.Add(path.Elements.ToList());

    public static string FullName(this MemberInfo info)
        => $"{info.DeclaringType.FullName}.{info.Name}";

    public static T[] GetAttributes<T>(this MemberInfo info, bool inherit = true)
        => info.GetCustomAttributes(typeof(T), inherit).Cast<T>().ToArray();
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

static class WorkflowExtensions
{
    public static string ToSimpleMermaidName(this string name)
    {
        // activity:  "longName"
        // decision:  "d1{longName}:yes"
        var longName = name.Split('{').Last().Split('}').First();

        var shortName = longName.Split('.').Last().TrimEnd("Async");
        return name.Replace(longName, shortName);
    }
    public static MethodInfo GetActivityMethod(this ActivityDefinition activity)
    {
        // there is no other way to access the actual method that is being invoked by the activity
        dynamic invoker = activity.GetFieldValue("invoker");
        object target = invoker.Target;
        dynamic invoker1 = target.GetFieldValue("invoker");
        object target1 = invoker1.Target;
        var method = (MethodInfo)target1.GetFieldValue("method");
        return method;
    }
    public static (Stack<bool>, string) GetDecisionExecutionPlan(this Dictionary<string, (Stack<bool>, string)> permutations, string decision)
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
}