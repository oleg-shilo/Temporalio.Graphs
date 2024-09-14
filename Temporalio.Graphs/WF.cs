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
    public static Task<bool> Decision(Expression<Func<bool>> activityCall, ActivityOptions options = null)
      => Workflow.ExecuteActivityAsync(activityCall, options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });

    public static Task<bool> ToDecision(this bool? result, [CallerArgumentExpression("result")] string decisionName = "", ActivityOptions options = null)
    {
        if (!GraphBuilder.IsBuildingGraph)
        {
            // Normal execution
            return Task.FromResult(result ?? false);
        }
        else
        {
            // add step with the dcisionName to the graph definition
            bool dummy = false; // this is a dummy value that will be replaced by the actual value in the interceptor
            return Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(dummy, decisionName, "yes|no"), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }

    public static Task<bool> ToDecision(this bool result, [CallerArgumentExpression("result")] string decisionName = "", ActivityOptions options = null)
        //Console.WriteLine($"Variable name is \"{dcisionName}\"; value is \"{result}\".");
        // add step with the dcisionName to the graph definition
        => Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(result, decisionName, "yes|no"), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });

    public static Task<bool> GenericDecision(this object workflowContext, Expression<Func<bool>> activityCall, string decisionName = null, ActivityOptions options = null)
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
        return Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(result, activityName, "yes|no"), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
    }

    public static Task<bool> WaitConditionAsync(Func<bool> conditionCheck, TimeSpan timeout, CancellationToken? cancellationToken = null, [CallerArgumentExpression("conditionCheck")] string conditionName = "", ActivityOptions options = null)
    {
        if (!GraphBuilder.IsBuildingGraph)
        {
            return Workflow.WaitConditionAsync(conditionCheck, TimeSpan.FromDays(1), cancellationToken);
        }
        else
        {
            // add step with the conditionName to the graph definition
            bool dummy = false;
            return Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(dummy, conditionName, "Signaled|Timeout"), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }
}