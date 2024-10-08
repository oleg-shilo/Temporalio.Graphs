using Temporalio.Workflows;
using static Temporalio.Workflows.Workflow;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Graphs;
using System.Numerics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Temporalio.Api.Protocol.V1;
using Temporalio.Worker.Interceptors;
using System.Reflection;
using System.Reflection.Emit;
using System.Diagnostics;
using Temporalio.Activities;
using Temporalio.Api.Update.V1;
using System.Text;
using Temporalio.Testing;
using Temporalio.Worker;
using System;
using System.Collections.Generic;

namespace Temporalio.Graphs;
/// <summary>
/// 
/// </summary>
/// <param name="IsBuildingGraph"></param>
/// <param name="ExitAfterBuildingGraph"></param>
/// <param name="GraphOutputFile">File name to store the graph. </param>
/// <param name="SplitNamesByWords"></param>
/// <param name="DoValidation"></param>
/// <param name="PreserveDecisionId"></param>
/// <param name="MermaidOnly"></param>
/// <param name="StartNode"></param>
/// <param name="EndNode"></param>
public record GraphBuilingContext(
        bool IsBuildingGraph,
        bool ExitAfterBuildingGraph,
        string? GraphOutputFile = null,
        bool SplitNamesByWords = false,
        bool DoValidation = true,
        bool PreserveDecisionId = true,
        bool MermaidOnly = false,
        string StartNode = "Start",
        string EndNode = "End");

public class GraphBuilder : IWorkerInterceptor
{

    /// <summary>
    /// Gets or sets the delegate for stopping workflow worker (e.g. at the end of the graph generation).
    /// </summary>
    /// <value>
    /// The stop workflow worker.
    /// </value>
    static public Action StopWorkflowWorker { get; set; } = () => { };

    static internal Dictionary<string, RuntimeContext> Sessions = new();

    /// <summary>
    /// Gets a value indicating whether this workflow instance is running in the building-graph mode.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is building graph; otherwise, <c>false</c>.
    /// </value>
    public static bool IsBuildingGraph => GraphBuilder.Runtime?.IsBuildingGraph == true;

    /// <summary>
    /// Gets a value indicating whether this workflow instance whould build the graph with the node's names being split by words for better readability of the graph.
    /// <p>IE activity `notifyCustomerByEmail` would appear in the graph as `Notify Customer By Email`.</p>
    /// </summary>
    /// <value>
    ///   <c>true</c> if [split names by words]; otherwise, <c>false</c>.
    /// </value>
    public static bool SplitNamesByWords => GraphBuilder.Runtime?.SplitNamesByWords == true;

    /// <summary>
    /// Gets the runtime context that is specific for a workflow run from which it is called.
    /// </summary>
    /// <value>
    /// The runtime context.
    /// </value>
    static internal RuntimeContext? Runtime
    {
        get
        {
            string runId;

            if (ActivityExecutionContext.HasCurrent)
                runId = ActivityExecutionContext.Current.Info.WorkflowRunId;
            else if (Workflow.InWorkflow)
                runId = Workflow.Info.RunId;
            else
                return null;

            return Sessions.ContainsKey(runId) ? Sessions[runId] : null;
        }
    }

    /// <summary>
    /// Gets or sets the client request.
    /// </summary>
    /// <value>
    /// The client request.
    /// </value>
    public GraphBuilingContext ClientRequest { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphBuilder"/> class.
    /// </summary>
    /// <param name="stopWorker">The stop worker.</param>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public GraphBuilder(Action stopWorker)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
        StopWorkflowWorker = stopWorker;
    }

    /// <summary>
    /// Create a workflow inbound interceptor to intercept calls.
    /// </summary>
    /// <param name="nextInterceptor">The next interceptor in the chain to call.</param>
    /// <returns>
    /// Created interceptor.
    /// </returns>
    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) => new WorkflowInbound(nextInterceptor, this.ClientRequest);

    /// <summary>
    /// Create an activity inbound interceptor to intercept calls.
    /// </summary>
    /// <param name="nextInterceptor">The next interceptor in the chain to call.</param>
    /// <returns>
    /// Created interceptor.
    /// </returns>
    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) => new ActivityInbound(nextInterceptor);

    /// <summary>
    /// Intercept activity execution.
    /// </summary>
    /// <param name="input">Input details of the call.</param>
    /// <returns>Completed activity result.</returns>
    class ActivityInbound : ActivityInboundInterceptor
    {
        /// <summary>
        /// Gets the runtime context that is specific for a workflow run from which it is called.
        /// </summary>
        /// <value>
        /// The runtime context.
        /// </value>
        RuntimeContext Runtime
        {
            get
            {
                var runId = ActivityExecutionContext.Current.Info.WorkflowRunId;
                if (!Sessions.ContainsKey(runId))
                    Sessions[runId] = new RuntimeContext();

                return Sessions[runId];
            }
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityInbound"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        public ActivityInbound(ActivityInboundInterceptor next) : base(next) { }

        public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            try
            {
                if (Runtime.IsBuildingGraph)
                {
                    var activityMethod = input.Activity.GetActivityMethod();
                    var activityName = activityMethod.FullName();
                    (var decisionName, var decisionId, var resultText) = input.GetGenericActivityName();

                    var nodeName = "";

                    if (!decisionName.IsEmpty())
                    {
                        nodeName = decisionName;

                        //check if the current plan already has this decision data
                        var decisionExists = Runtime.CurrentDecisionsPlan
                                                    .Any(x => x.Key.Name == nodeName);
                        if (!decisionExists)
                        {
                            var currentPlan = Runtime.CurrentDecisionsPlan;
                            var cloneOfCurrentPlan = currentPlan.ToDictionary(x => x.Key, x => x.Value);

                            // add the new decision permutations to the current plan and to a new clone of the current plan 
                            // one decision - two permutations 

                            currentPlan.Add((decisionName, decisionId), true);
                            cloneOfCurrentPlan.Add((decisionName, decisionId), false);

                            Runtime.DecisionsPlans.Add(cloneOfCurrentPlan);
                        }
                    }

                    var decision = Runtime.CurrentDecisionsPlan?
                        .Where(x => x.Key.Name == nodeName)
                        .Select(x => new { Name = x.Key.Name, Id = x.Key.Index.ToString(), Result = x.Value })
                        .FirstOrDefault();

                    if (decision != null)
                    {
                        // mocked decision activity
                        Runtime.CurrentGraphPath.AddDecision(decision.Id, decision.Result, decision.Name, resultText);
                        return decision.Result;
                    }
                    else
                    {
                        // mocked normal activity
                        Runtime.CurrentGraphPath.AddStep(activityName);

                        try
                        {
                            var type = activityMethod.ReturnType;
                            var isTask = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);
                            if (isTask)
                            {
                                var result = Activator.CreateInstance(type.GetGenericArguments()[0]);
                                return Task.FromResult(result);
                            }
                            else
                            {
                                return activityMethod.ReturnType.IsValueType
                                    ? Activator.CreateInstance(activityMethod.ReturnType)
                                    : null;
                            }
                        }
                        catch
                        {
                            return null;
                        }
                    }
                }
                else
                {
                    // normal activity
                    return await base.ExecuteActivityAsync(input);
                }
            }
            catch (Exception e)
            {
                throw new ApplicationFailureException("Assertion failed", e, "AssertFail");
            }
        }
    }
    class WorkflowInbound : WorkflowInboundInterceptor
    {
        /// <summary>
        /// Gets the runtime context that is specific for a workflow run from which it is called.
        /// </summary>
        /// <value>
        /// The runtime context.
        /// </value>
        RuntimeContext Runtime
        {
            get
            {
                var runId = Info.RunId;
                if (!Sessions.ContainsKey(Info.RunId))
                    Sessions[Info.RunId] = new RuntimeContext();

                return Sessions[Info.RunId];
            }
        }

        GraphBuilingContext context;
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowInbound"/> class.
        /// </summary>
        /// <param name="next">The next.</param>
        /// <param name="context">The context.</param>
        public WorkflowInbound(WorkflowInboundInterceptor next, GraphBuilingContext context) : base(next)
        {
            this.context = context;
        }


        /// <summary>
        /// Intercept workflow execution.
        /// </summary>
        /// <param name="input">Input details of the call.</param>
        /// <returns>Completed workflow result.</returns>
        public override async Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
        {
            try
            {
                // if we are running on a remote temporal server user may decide to push the custom context
                // otherwise we will use the context from the current execution (obtained during worker initialization)
                if (!Runtime.InitFrom(input))
                    Runtime.InitFrom(this.context);

                if (Runtime.IsBuildingGraph)
                {
                    var workflowAssembly = input.Instance.GetType().Assembly;

                    // Run WF with the DAG generator for all permutations of WF decisions (profiles)
                    // Generating the complete WF diagram based on the analysts of all unique path graphs.
                    // MermaidGenerator will print diagram at disposal

                    Runtime.DecisionsPlans.Add(item: new Dictionary<(string Name, int Index), bool>());

                    var generator = new GraphGenerator();

                    // if there is no decision nodes then there will be only once decision plan
                    // Otherwise there will be as many as decisions permutations (graph paths)
                    // IE:
                    // no decisions: 1 path,
                    // 1 decision:   2 paths,
                    // 2 decisions:  4 paths,
                    // 3 decisions:  8 paths,
                    // . . .

                    while (Runtime.DecisionsPlans.Any())
                        try
                        {
                            Runtime.CurrentGraphPath.Clear();

                            if (Runtime.ClientRequest?.StartNode.IsNotEmpty() == true)
                                Runtime.CurrentGraphPath.AddStep(Runtime.ClientRequest.StartNode);

                            // executing the original WF where the activities will be mocked
                            await base.ExecuteWorkflowAsync(input);

                            if (Runtime.ClientRequest?.EndNode.IsNotEmpty() == true)
                                Runtime.CurrentGraphPath.AddStep(Runtime.ClientRequest.EndNode);

                            generator.Scenarios.AddPath(Runtime.CurrentGraphPath);
                        }
                        finally
                        {
                            Runtime.DecisionsPlans.RemoveAt(0);
                        }

                    if (!Runtime.ClientRequest?.PreserveDecisionId == true)
                        generator.PrittyfyNodes();

                    var result = new StringBuilder();

                    if (!Runtime.ClientRequest?.MermaidOnly == true)
                        result.AppendLine(generator.ToPaths())
                              .AppendLine("--------");

                    result.AppendLine(generator.ToMermaidSyntax());

                    if (Runtime.ClientRequest?.DoValidation == true)
                        result.AppendLine("--------")
                              .AppendLine(generator.ValidateGraphAgainst(workflowAssembly));

                    if (Runtime.ClientRequest?.GraphOutputFile?.IsNotEmpty() == true)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"The WF graph is saved to `{Runtime.ClientRequest.GraphOutputFile}`.");
                        File.WriteAllText(Runtime.ClientRequest.GraphOutputFile, result.ToString());
                    }
                    else
                    {
                        Console.WriteLine("=====================");
                        Console.WriteLine(result.ToString().Trim());
                        Console.WriteLine("=====================");
                    }

                    if (Runtime.ClientRequest?.ExitAfterBuildingGraph == true)
                    {
                        StopWorkflowWorker();
                    }

                    return result.ToString().Trim();
                }
                else
                {
                    return await base.ExecuteWorkflowAsync(input);
                }
            }
            catch (Exception e)
            {
                throw new ApplicationFailureException("Assertion failed", e, "AssertFail");
            }
            finally
            {
                if (Sessions.ContainsKey(Info.RunId))
                    Sessions.Remove(Info.RunId);
            }
        }
    }
}


