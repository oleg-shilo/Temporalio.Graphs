using static System.Environment;
using Temporalio.Activities;
using System.Reflection;
using System.Linq.Expressions;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Client;
using System.Text;

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

    internal static string SplitByWords(this string text, bool isMermaid = false)
    {
        if (!GraphBuilder.SplitNamesByWords)
            return text;

        var rawText = text.Replace("_", " ");

        if (rawText.Contains("{"))
        {
            var parts = rawText.Split('{', '}');
            var prefix = parts[0];
            var name = parts[1];
            var suffix = parts[2];

            return $"{prefix}{{{name.SplitByCharCase()}}}{suffix}";
        }
        else
        {
            if (isMermaid)
            {
                var textView = rawText.SplitByCharCase();

                if (textView.Contains(" ")) // more than a single word
                    return $"{rawText}[{textView}]";
                else
                    return rawText;
            }
            else
                return rawText.SplitByCharCase();
        }
    }
    static string Capitalise(this string text)
    {
        if (text.IsNotEmpty())
        {
            if (!char.IsUpper(text.First()))
                return text.Substring(0, 1).ToUpper() + text.Substring(1, text.Length - 1);
        }
        return text;
    }
    static string SplitByCharCase(this string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]) && !char.IsUpper(text[i - 1]))
            {
                words.Add(word.ToString());
                word.Clear();
            }
            word.Append(text[i]);
        }
        words.Add(word.ToString());
        return words.JoinBy(" ").Trim().Capitalise();
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
static class GraphsExtensions
{
    public static string ToSimpleNodeName(this string name)
    {
        // activity:  "longName"
        // decision:  "d1{longName}:yes"
        var longName = name.Split('{').Last().Split('}').First();

        // namespace.class.method
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
}