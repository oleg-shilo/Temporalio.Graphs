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
public static class WF
{
    public static async Task<bool> Decision(Expression<Func<bool>> activityCall, ActivityOptions options = null)
    {
        bool result = await Workflow.ExecuteActivityAsync(activityCall, options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        return result;
    }
    public static async Task<bool> ToDecision(this bool? result, [CallerArgumentExpression("result")] string decisionName = "", ActivityOptions options = null)
    {
        //Console.WriteLine($"Variable name is \"{dcisionName}\"; value is \"{result}\".");
        // add step with the dcisionName to the graph definition

        if (!GraphBuilder.IsBuildingGraph)
        {
            // Normal execution. All exception will bubble up
            return await Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision((bool)result, decisionName), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
        else
        {
            bool dummy = false; // this is a dummy value that will be replaced by the actual value in the interceptor
            if (result.HasValue)
                dummy = result.Value;
            return await Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(dummy, decisionName), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }

    public static async Task<bool> ToDecision(this bool result, [CallerArgumentExpression("result")] string decisionName = "", ActivityOptions options = null)
    {
        //Console.WriteLine($"Variable name is \"{dcisionName}\"; value is \"{result}\".");
        // add step with the dcisionName to the graph definition
        result = await Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(result, decisionName), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        return result;
    }

    public static async Task<bool> GenericDecision(this object workflow, Expression<Func<bool>> activityCall, string decisionName = null, ActivityOptions options = null)
    {
        var activityName = decisionName;
        if (activityName.IsEmpty() && activityCall.Body is MemberExpression memberExpression)
        {
            activityName = memberExpression.Member.Name;
        }

        bool result = false;
        try
        {
            result = activityCall.Compile().Invoke();
        }
        catch
        {
            if (!GraphBuilder.IsBuildingGraph)
                throw;
        }

        result = await Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(result, activityName), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        return result;
    }

    public static async Task<bool> WaitConditionAsync(Func<bool> conditionCheck, TimeSpan timeout, CancellationToken? cancellationToken = null, [CallerArgumentExpression("conditionCheck")] string conditionName = "", ActivityOptions options = null)
    {
        if (!GraphBuilder.IsBuildingGraph)
        {
            return await Workflow.WaitConditionAsync(conditionCheck, TimeSpan.FromDays(1), cancellationToken);
        }
        else
        {
            // add step with the conditionName to the graph definition
            bool dummy = false;
            return await Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(dummy, conditionName), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }
}