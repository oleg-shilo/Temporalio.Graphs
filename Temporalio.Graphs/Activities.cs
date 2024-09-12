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
}

public class GenericActivities
{
    [Activity]
    [Decision]
    public bool MakeDecision(bool result, string name)
    {
        return result;
    }
}
static class GenericActivitiesExtension
{
    public static string GetGenericActivityName(this ExecuteActivityInput input)
    {
        var activityMethod = input.Activity.GetActivityMethod();
        var activityName = activityMethod.FullName();

        if (activityMethod.DeclaringType == typeof(GenericActivities) &&
            activityMethod.Name == nameof(GenericActivities.MakeDecision))
        {
            // activityMethod: "public bool MakeDecision(bool result, string name)"

            // IE: "new StepResult().IsPdf"
            var decisionName = input.Args.Last().ToString().Split('.').Last();
            decisionName = decisionName.Split("=>").Last().Trim();

            return decisionName;
        }
        return null;
    }
}