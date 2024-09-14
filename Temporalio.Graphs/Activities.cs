using System.Runtime.CompilerServices;
using static System.Environment;
using Temporalio.Activities;
using System.Reflection;
using System.Linq.Expressions;
using Temporalio.Api.Update.V1;
using Temporalio.Worker.Interceptors;

namespace Temporalio.Graphs;

public class DecisionAttribute : Attribute
{
    public string PositiveResultName { get; set; } = "Yes";
    public string NegativeResultName { get; set; } = "No";
}

public class GenericActivities
{
    [Activity]
    [Decision]
    public bool MakeDecision(bool result, string name, string resultText)
    {
        return result;
    }
}

static class GenericActivitiesExtension
{
    public static (string name, string resultText) GetGenericActivityName(this ExecuteActivityInput input)
    {
        var activityMethod = input.Activity.GetActivityMethod();
        var activityName = activityMethod.FullName();

        if (activityMethod.DeclaringType == typeof(GenericActivities) &&
            activityMethod.Name == nameof(GenericActivities.MakeDecision))
        {
            // args will always have 3 args
            // activityMethod: bool MakeDecision(bool result, string name, string resultText)

            // IE: name "new StepResult().IsPdf"
            var decisionName = input.Args[1].ToString().Split('.').Last();
            decisionName = decisionName.Split("=>").Last().Trim();


            return (decisionName, input.Args[2]?.ToString() ?? "");
        }

        var decisionInfo = activityMethod.GetCustomAttribute<DecisionAttribute>();
        if (decisionInfo != null)
        {
            return (activityName, $"{decisionInfo.PositiveResultName}|{decisionInfo.NegativeResultName}");
        }

        return default;
    }
}