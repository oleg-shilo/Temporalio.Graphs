using static System.Environment;
using Temporalio.Activities;
using System.Reflection;
using System.Linq.Expressions;
using Temporalio.Testing;
using Temporalio.Worker;
using Temporalio.Client;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Temporalio.Graphs;

/// <summary>
/// 
/// </summary>
public static class TemporalExtensions
{
    /// <summary>
    /// Adds all instance activities from a type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options">The options.</param>
    /// <returns></returns>
    public static TemporalWorkerOptions AddAllActivities<T>(this TemporalWorkerOptions options) where T : new()
    {
        options.AddAllActivities(new T());
        return options;
    }

    /// <summary>
    /// Adds the static activities from a type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="options">The options.</param>
    /// <returns></returns>
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

    /// <summary>
    /// Executes the worker in memory.
    /// <p>It uses `WorkflowEnvironment.StartLocalAsync()` to start a local test server with full Temporal capabilities but no time skipping. </p>
    /// </summary>
    /// <typeparam name="TWorkflow">The type of the workflow.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="workerOptions">The worker options.</param>
    /// <param name="workflowRunCall">The workflow run call.</param>
    /// <param name="rethrow">if set to <c>true</c> [rethrow].</param>
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

    public async static Task ExecuteWorker<TWorkflow, TResult>(this WorkflowEnvironment env, TemporalWorkerOptions workerOptions, Expression<Func<TWorkflow, Task<TResult>>> workflowRunCall, bool rethrow = false)
    {
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

/// <summary>
/// 
/// </summary>
public static class GenericExtensions
{
    /// <summary>
    /// Trims the end of the string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="trimText">The trim text.</param>
    /// <returns></returns>
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

        var rawText = text.Replace("_", " ").Replace("\"", "'"); // " interferes with Mermaid syntax

        if (rawText.Contains("{"))
        {
            // 2{isPdf}:No
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
                if (!textView.Contains("(") && !textView.Contains("[")) // has no aliases defined
                {
                    if (textView.Contains(" ")) // more than a single word
                        return $"{rawText}[{textView}]";
                }
                return rawText;
            }
            else
                return rawText
                    .SplitByCharCase()
                    .Replace("s((", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("e((", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("))", "");
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
        //bool isPunctuationChar(char c)=> char.IsPunctuation(c) || char.IsSymbol(c);

        for (int i = 0; i < text.Length; i++)
        {
            if (i > 0)
            {
                var prevChar = text[i - 1];
                var currChar = text[i];
                if (currChar.IsUpper() && !prevChar.IsUpper()
                    && !char.IsPunctuation(prevChar) && !char.IsPunctuation(currChar))
                {
                    words.Add(word.ToString());
                    word.Clear();
                }
            }
            word.Append(text[i]);
        }
        words.Add(word.ToString());
        return words.Select(x => x.Trim()).JoinBy(" ").Trim().Capitalise();
    }

    /// <summary>
    /// Joins the items by the specified separator.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="separator">The separator.</param>
    /// <returns></returns>
    public static string JoinBy(this IEnumerable<string> items, string separator)
        => string.Join(separator, items);

    /// <summary>
    /// Determines whether the specified items is empty.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items">The items.</param>
    /// <returns>
    ///   <c>true</c> if the specified items is empty; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsEmpty<T>(this IEnumerable<T>? items)
        => items == null ? true : items.Count() == 0;

    /// <summary>
    /// Determines whether the specified character is upper.
    /// </summary>
    /// <param name="char">The character.</param>
    /// <returns>
    ///   <c>true</c> if the specified character is upper; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsUpper(this char @char) => char.IsUpper(@char);

    /// <summary>
    /// Determines whether the specified items is not empty.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="items">The items.</param>
    /// <returns>
    ///   <c>true</c> if [is not empty] [the specified items]; otherwise, <c>false</c>.
    /// </returns>
    public static bool IsNotEmpty<T>(this IEnumerable<T> items)
        => items?.Any() == true;

    /// <summary>
    /// Adds graph path to the collection of scenarios (paths).
    /// </summary>
    /// <param name="scenarios">The scenarios.</param>
    /// <param name="path">The path.</param>
    public static void AddPath(this List<List<string>> scenarios, GraphPath path)
        => scenarios.Add(path.Elements.ToList());

    /// <summary>
    /// Gets the full name of the member.
    /// </summary>
    /// <param name="info">The information.</param>
    /// <returns></returns>
    public static string FullName(this MemberInfo info)
        => $"{info.DeclaringType?.FullName}.{info.Name}";

    /// <summary>
    /// Gets the attributes.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="info">The information.</param>
    /// <param name="inherit">if set to <c>true</c> [inherit].</param>
    /// <returns></returns>
    public static T[] GetAttributes<T>(this MemberInfo info, bool inherit = true)
        => info.GetCustomAttributes(typeof(T), inherit).Cast<T>().ToArray();

    /// <summary>
    /// Gets the field value via reflection.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="value">The value.</param>
    /// <param name="flag">The flag.</param>
    /// <returns></returns>
    public static dynamic GetFieldValue(this object obj, string value, BindingFlags flag = BindingFlags.Default)
    {
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        return obj?
          .GetType()
          .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | flag)
          .FirstOrDefault(x => x.Name == value)
          .GetValue(obj);
    }

    /// <summary>
    /// Gets the property value via reflection.
    /// </summary>
    /// <param name="obj">The object.</param>
    /// <param name="value">The value.</param>
    /// <param name="flag">The flag.</param>
    /// <returns></returns>
    public static dynamic GetPropValue(this object obj, string value, BindingFlags flag = BindingFlags.Default)
    {
        return obj
          .GetType()
          .GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | flag)
          .FirstOrDefault(x => x.Name == value)
          .GetValue(obj);
    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8603 // Possible null reference return.
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

    //public static int ToDecisionId(this string name)
    //    => name.GetHashCode();

    // calculates string crc
    public static int ToDecisionId(this string name)
    {
        var hash = 0;
        if (name.Length == 0)
            return hash;
        for (int i = 0; i < name.Length; i++)
        {
            hash = ((hash << 5) - hash) + name[i];
            hash &= hash;
        }
        return hash;
    }

    public static string DecorateSignals(this string name)
    {
        if (name.Contains(":&sgnl;"))
            name = name
                .Replace(":&sgnl;", "")
                .Replace("{", "{{")
                .Replace("}", "}}");
        return name;
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