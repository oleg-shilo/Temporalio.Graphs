using System.Runtime.CompilerServices;
using static System.Environment;
using Temporalio.Activities;
using System.Reflection;
using System.Linq.Expressions;
using Temporalio.Workflows;
using System.Diagnostics;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Client;
using System.Runtime.Intrinsics.Arm;
using Microsoft.Extensions.Options;

namespace Temporalio.Graphs;

public static class TemporalExtensions
{
    public static TemporalWorkerOptions AddAllActivities<T>(this TemporalWorkerOptions options) where T : new()
    {
        options.AddAllActivities(new T());
        return options;
    }
    public static TemporalWorkerOptions AddStaticActivitiesFrom<T>(this TemporalWorkerOptions options)
    {
        var methods = typeof(T).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(Task) || m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));

        foreach (var method in methods)
        {
            var parameters = method.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var call = Expression.Call(method, parameters);

            var lambda = Expression.Lambda(call, parameters);
            var compiledDelegate = lambda.Compile();

            options.AddActivity(compiledDelegate);
        }

        return options;
    }
    public async static Task ExecuteWorkerInMemory<TWorkflow, TResult>(this TemporalWorkerOptions workerOptions, Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, bool rethrow = false)
    {
        await using var env = await WorkflowEnvironment.StartLocalAsync();
        using var worker = new TemporalWorker(env.Client, workerOptions);
        WorkflowOptions options = new(id: $"wf-{Guid.NewGuid()}", taskQueue: worker.Options.TaskQueue!);
        try
        {

            await worker.ExecuteAsync(async () =>
                                      {
                                          var result = await ((ITemporalClient)worker.Client).ExecuteWorkflowAsync(workflowRunCall, options);
                                      });
        }
        catch (Exception)
        {
            if (rethrow)
                throw;
        }

    }
}

public static class GenericExtensions
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
        => items == null ? true : items.Count() == 0;

    public static bool IsNotEmpty<T>(this IEnumerable<T> items)
        => items?.Any() == true;

    public static void AddPath(this List<List<string>> scenarios, GraphPath path)
        => scenarios.Add(path.Elements.ToList());

    public static string FullName(this MemberInfo info)
        => $"{info.DeclaringType.FullName}.{info.Name}";
    public static int ToInt(this string text)
        => int.Parse(text);

    public static string ChangeExtension(this string file, string newExtension)
        => Path.ChangeExtension(file, newExtension);

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
public static class GraphsExtensions
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

    public static void GeneratePermutationsFor(this List<Dictionary<(string name, int index), bool>> result, params (string, int)[] names)
        => GeneratePermutations(result, names, 0, new bool[names.Count()]);

    static void GeneratePermutations(List<Dictionary<(string name, int index), bool>> result, (string, int)[] names, int index, bool[] currentPermutation)
    {
        bool[] values = { true, false };

        if (index == currentPermutation.Length)
        {
            //Debug.WriteLine(currentPermutation.Select((x, i) => $"{names[i]}: {x}").JoinBy(", "));
            result.Add(
                currentPermutation
                    .Select((x, i) => new { name = (names[i], i), value = x })
                    .ToDictionary(x => x.name.Item1, x => x.value));
            return;
        }

        // Tail-recursive calls
        foreach (bool value in values)
        {
            currentPermutation[index] = value;
            GeneratePermutations(result, names, index + 1, currentPermutation);
        }
    }

    public static (string Name, int Index)[] GetDecisions(this Assembly assembly)
    {
        return assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods())
            .Where(p => p.GetCustomAttributes<DecisionAttribute>().Any())
            .Select((m, i) => (m.FullName(), i))
            .ToArray();
    }
}