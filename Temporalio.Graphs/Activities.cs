using Temporalio.Activities;
using System.Reflection;
using Temporalio.Worker.Interceptors;

namespace Temporalio.Graphs;

/// <summary>
/// This attribute is used to mark a method as a decision activity in the workflow. 
/// Such methods will be used to create decision nodes in the graph and they are refereed in the 
/// documentation as 'dedicated decision activities'.
/// </summary>
/// <seealso cref="System.Attribute" />
public class DecisionAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the positive result to appear in the graph.
    /// </summary>
    /// <value>
    /// The name of the positive result.
    /// </value>
    public string PositiveResultName { get; set; } = "Yes";

    /// <summary>
    /// Gets or sets the name of the negative result to appear in the graph.
    /// </summary>
    /// <value>
    /// The name of the negative result.
    /// </value>
    public string NegativeResultName { get; set; } = "No";
}

/// <summary>
/// This class contains the generic activity that are used internally by <see cref="GraphBuilder"/> to implement decision nodes the workflow graph.
/// </summary>
public class GenericActivities
{
    /// <summary>
    /// A generic activity that are used internally by <see cref="GraphBuilder"/> to implement decision nodes the workflow graph.
    /// </summary>
    /// <param name="result">if set to <c>true</c> [result].</param>
    /// <param name="name">The name.</param>
    /// <param name="resultText">The result text.</param>
    /// <returns></returns>
    [Activity]
    [Decision]
    public bool MakeDecision(bool decisionResult, string decisionName, int decisionId, string resultText)
    {
        return decisionResult;
    }
}

static class GenericActivitiesExtension
{
    public static (string name, int id, string resultText) GetGenericActivityName(this ExecuteActivityInput input)
    {
        var activityMethod = input.Activity.GetActivityMethod();
        var activityName = activityMethod.FullName();

        if (activityMethod.DeclaringType == typeof(GenericActivities) &&
            activityMethod.Name == nameof(GenericActivities.MakeDecision))
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8605 // Unboxing a possibly null value.
            // args will always have 3 args
            // activityMethod: bool MakeDecision(bool result, string name, string id, string resultText)

            // IE: name "new StepResult().IsPdf"
            //     return interpolCheck;               :&sgnl;
            var decisionName = input.Args[1].ToString().Split('.').Last();
            decisionName = decisionName.Split("=>").Last()
                .Replace("\n", "").Replace("\r", "")
                .Replace("{", "").Replace("}", "")
                .Replace("  ", "").Replace(" :", ":")
                .Trim();
            var decisionId = (int)input.Args[2];

            return (decisionName, decisionId, input.Args[3]?.ToString() ?? "");
        }
#pragma warning restore CS8605 // Unboxing a possibly null value.
#pragma warning restore CS8602 // Dereference of a possibly null reference.

        var decisionInfo = activityMethod.GetCustomAttribute<DecisionAttribute>();
        if (decisionInfo != null)
        {
            // dor dedicated the id is irrelevant as the name is unique anyway 
            return (activityName, -1, $"{decisionInfo.PositiveResultName}|{decisionInfo.NegativeResultName}");
        }

        return default;
    }
}