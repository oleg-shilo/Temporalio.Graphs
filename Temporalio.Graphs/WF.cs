using System.Runtime.CompilerServices;
using System.Linq.Expressions;
using Temporalio.Workflows;

namespace Temporalio.Graphs;

/// <summary>
/// The class that contains various extension methods for the <see cref="Temporalio.Workflows.Workflow"/> 
/// class. These methods are used to define the workflow graph nodes that otherwise have no direct interception support 
/// in Temporal. IE Temporal allows interception of workflows and activities but not signals and in-code decisions.
/// <p>Thus unsupported workflow entities are converted into on-fly activities when the workflow is running in the 
/// building-graph mode.</p>
/// </summary>
public static class WF
{
    /// <summary>
    /// This method is the renamed equivalent of a specific <see cref="Workflow.ExecuteActivityAsync"/> case that return bool action result, 
    /// which allows more expressive invoking of the dedicated decision activities in the workflow definition.
    /// <p>Note, this method is used for dedicated decision activities (returning <c>bool</c> result), but not 
    /// dynamic decision activities.</p>
    /// <code>
    /// bool needToConvert = await WF.Decision(() => BankingActivities.NeedToConvert(details));
    /// </code>
    /// </summary>
    /// <param name="activityCall">Invocation of activity method.</param>
    /// <param name="options">Activity options. This is required and either
    /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
    /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
    /// <returns></returns>
    public static Task<bool> Decision(Expression<Func<bool>> activityCall, ActivityOptions? options = null)
      => Workflow.ExecuteActivityAsync(activityCall, options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });

    /// <summary>
    /// Converts a boolean value into a dynamically generated <see cref="Activity"/> and executes it 
    /// if the workflow is running in the build-graph mode. Otherwise, it returns the result immediately.
    /// </summary>
    /// <param name="result">The decision result to be wrapped into a dynamic activity.</param>
    /// <param name="decisionName">Name of the decision. If not specified, it will be derived from the result parameter name.</param>
    /// <param name="options">Activity options. If not specified then the option with <see cref="ActivityOptions.StartToCloseTimeout" /> 5 minutes will be used.</param>
    /// <returns>Task for completion with result.</returns>
    public static Task<bool> ToDecision(this bool? result, [CallerArgumentExpression("result")] string decisionName = "", ActivityOptions? options = null)
        => ToDecision(result ?? false, decisionName, options);

    /// <summary>
    /// Converts a boolean value into a dynamically generated <see cref="Activity"/> and executes it 
    /// if the workflow is running in the build-graph mode. Otherwise, it returns the result immediately.
    /// </summary>
    /// <param name="result">The decision result to be wrapped into a dynamic activity.</param>
    /// <param name="decisionName">Name of the decision. If not specified, it will be derived from the result parameter name.</param>
    /// <param name="options">Activity options. If not specified then the option with <see cref="ActivityOptions.StartToCloseTimeout" /> 5 minutes will be used.</param>
    /// <returns>Task for completion with result.</returns>
    public static Task<bool> ToDecision(this bool result, [CallerArgumentExpression("result")] string decisionName = "", ActivityOptions? options = null)
        => Workflow.ExecuteActivityAsync( // add step with the dcisionName to the graph definition
               (GenericActivities b) => b.MakeDecision(result, decisionName, decisionName.ToDecisionId(), "yes|no"), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });

#pragma warning disable CS8604 // Possible null reference argument.
    /// <summary>
    /// Converts a boolean returning expression into a dynamically generated <see cref="Activity"/> and executes it 
    /// if the workflow is running in the build-graph mode. Otherwise, it returns the result immediately.
    /// </summary>
    /// <param name="result">The decision result to be wrapped into a dynamic activity.</param>
    /// <param name="decisionName">Name of the decision. If not specified, it will be derived from the result parameter name.</param>
    /// <param name="options">Activity options. If not specified then the option with <see cref="ActivityOptions.StartToCloseTimeout" /> 5 minutes will be used.</param>
    /// <returns>Task for completion with result.</returns>
    public static Task<bool> GenericDecision(this Expression<Func<bool>> activityCall, string? decisionName = null, ActivityOptions? options = null)
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
        return Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(result, activityName, activityName.ToDecisionId(), "yes|no"), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
    }
#pragma warning restore CS8604 // Possible null reference argument.

    /// <summary>
    /// This method overwrites <see cref="Workflow.WaitConditionAsync"/> (wait for signal) to allow 
    /// it to be executed as an dynamically generated activity when the workflow executed in the build-graph mode.
    /// In the normal mode, it will execute the condition check as a normal wait condition <see cref="Workflow.WaitConditionAsync"/>.
    /// </summary>
    /// <param name="conditionCheck">Condition function.</param>
    /// <param name="timeout">Optional timeout for waiting.</param>
    /// <param name="cancellationToken">Cancellation token. If unset, this defaults to <see cref="CancellationToken" />.</param>
    /// <param name="conditionName">Name of the condition that will be used as the name of the dynamic Activity. 
    /// If it is not specified, the name will be derived from the expression.</param>
    /// <param name="options">Activity options. This is required and either
    /// <see cref="ActivityOptions.ScheduleToCloseTimeout" /> or
    /// <see cref="ActivityOptions.StartToCloseTimeout" /> must be set.</param>
    /// <returns>Task for completion with result.</returns>
    public static Task<bool> WaitConditionAsync(Func<bool> conditionCheck, TimeSpan timeout, CancellationToken? cancellationToken = null, [CallerArgumentExpression("conditionCheck")] string conditionName = "", ActivityOptions? options = null)
    {
        if (!GraphBuilder.IsBuildingGraph)
        {
            return Workflow.WaitConditionAsync(conditionCheck, timeout, cancellationToken);
        }
        else
        {
            // add step with the conditionName to the graph definition
            bool dummy = false;
            return Workflow.ExecuteActivityAsync((GenericActivities b) => b.MakeDecision(dummy, conditionName + ":&sgnl;", conditionName.ToDecisionId(), "Signaled|Timeout"), options ?? new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(5) });
        }
    }
}