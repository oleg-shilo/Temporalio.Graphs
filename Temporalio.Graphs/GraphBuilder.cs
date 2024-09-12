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

namespace Temporalio.Graphs;
public record GraphBuilingContext(bool IsBuildingGraph, bool ExitAfterBuildingGraph, string? GraphOutputFile);

public class GraphBuilder : IWorkerInterceptor
{
    static public Action StopWorkflowWorker { get; set; } = () => { };

    static internal Dictionary<string, RuntimeContext> Sessions = new();

    public static bool IsBuildingGraph => GraphBuilder.GetRuntimeContext()?.IsBuildingGraph == true;
    static internal RuntimeContext GetRuntimeContext()
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

    public GraphBuilingContext Context { get; set; }

    public GraphBuilder(Action stopWorker)
    {
        StopWorkflowWorker = stopWorker;
    }
    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor nextInterceptor) => new WorkflowInbound(nextInterceptor, this.Context);
    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor nextInterceptor) => new ActivityInbound(nextInterceptor);

    class ActivityInbound : ActivityInboundInterceptor
    {
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
        public ActivityInbound(ActivityInboundInterceptor next) : base(next) { }

        public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            try
            {
                if (Runtime.IsBuildingGraph)
                {
                    var activityMethod = input.Activity.GetActivityMethod();
                    var activityName = activityMethod.FullName();
                    var decisionName = input.GetGenericActivityName();

                    var nodeName = activityName;


                    if (!decisionName.IsEmpty())
                    {
                        nodeName = decisionName;

                        //check if the current plan already has this decision data
                        var decisionExists = Runtime.CurrentDecisionsPlan
                                                    .Any(x => x.Key.Name == decisionName);
                        if (!decisionExists)
                        {
                            var currentPlan = Runtime.CurrentDecisionsPlan;
                            var cloneOfCurrentPlan = currentPlan.ToDictionary(x => x.Key, x => x.Value);

                            // add the new decision permutations to a current plan and to a clone of the current plan 
                            // one decision - two permutations (plans
                            currentPlan.Add((decisionName, decisionName.GetHashCode()), true);
                            cloneOfCurrentPlan.Add((decisionName, decisionName.GetHashCode()), false);

                            Runtime.DecisionsPlans.Add(cloneOfCurrentPlan);
                        }
                    }
                    else
                    {
                        // it's not a generic decision but a user activity marked with the decision attribute
                        var decisionInfo = activityMethod.GetCustomAttribute<DecisionAttribute>();

                        if (decisionInfo != null)
                        {
                            var decisionExists = Runtime.CurrentDecisionsPlan
                                                        .Any(x => x.Key.Name == activityName);
                            if (!decisionExists)
                            {
                                var currentPlan = Runtime.CurrentDecisionsPlan;
                                var cloneOfCurrentPlan = currentPlan.ToDictionary(x => x.Key, x => x.Value);

                                // add the new decision permutations to a current plan and to a clone of the current plan 
                                // one decision - two permutations (plans
                                currentPlan.Add((activityName, activityName.GetHashCode()), true);
                                cloneOfCurrentPlan.Add((activityName, activityName.GetHashCode()), false);

                                Runtime.DecisionsPlans.Add(cloneOfCurrentPlan);
                            }
                        }
                    }

                    var decision = Runtime.CurrentDecisionsPlan?
                        .Where(x => x.Key.Name == nodeName)
                        .Select(x => new { Name = x.Key.Name, Id = x.Key.Index.ToString(), Result = x.Value })
                        .FirstOrDefault();

                    if (decision != null)
                    {
                        var decisionInfo = activityMethod.GetCustomAttribute<DecisionAttribute>();

                        // mocked decision activity
                        Runtime.CurrentGraphPath.AddDecision(decision.Id, decision.Result, decision.Name);

                        // return decision.Result ? decisionInfo.PositiveValue : decisionInfo.NegativeValue;
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
        public WorkflowInbound(WorkflowInboundInterceptor next, GraphBuilingContext context) : base(next)
        {
            this.context = context;
        }

        public override async Task<object?> ExecuteWorkflowAsync(ExecuteWorkflowInput input)
        {
            try
            {
                // if we are running on a remote temporal server user may decide to push the custom context
                // otherwise we will use the context from the current execution (obtained during worker initialization)
                if (!Runtime.InitFrom(input))
                    if (!Runtime.InitFrom(this.context))
                        throw new ApplicationException("Execution context object was not supplied.");

                if (Runtime.IsBuildingGraph)
                {
                    Environment.SetEnvironmentVariable("TEMPORAL_GRAPH", "true");

                    var workflowAssembly = input.Instance.GetType().Assembly;

                    // Run WF with the DAG generator for all permutations of WF decisions (profiles)
                    // Generating the complete WF diagram based on the analysts of all unique path graphs.
                    // MermaidGenerator will print diagram at disposal

                    //Runtime.DecisionsPlans.GeneratePermutationsFor(workflowAssembly.GetDecisions());
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
                            Runtime.CurrentGraphPath
                                   .Clear()
                                   .AddStep("Start");

                            // executing the original WF where the activities will be mocked
                            await base.ExecuteWorkflowAsync(input);

                            Runtime.CurrentGraphPath.AddStep("End");

                            generator.Scenarios.AddPath(Runtime.CurrentGraphPath);
                        }
                        finally
                        {
                            Runtime.DecisionsPlans.RemoveAt(0);
                        }


                    generator.PrittyfyNodes();

                    var graphs = generator.ToPaths();
                    var mermaid = generator.ToMermaidSyntax();
                    var validationResult = generator.ValidateGraphAgainst(workflowAssembly);

                    var result = new StringBuilder();

                    result.AppendLine("=====================")
                          .AppendLine(graphs)
                          .AppendLine("--------")
                          .AppendLine(mermaid)
                          .AppendLine("--------")
                          .AppendLine(validationResult)
                          .AppendLine("=====================");

                    if (Runtime.GraphOutputFile.IsNotEmpty())
                    {
                        Console.WriteLine();
                        Console.WriteLine($"The WF graph is saved to `{Runtime.GraphOutputFile}`.");
                        File.WriteAllText(Runtime.GraphOutputFile, result.ToString());
                    }
                    else
                    {
                        Console.WriteLine(result.ToString());
                    }

                    if (Runtime.ExitAfterBuildingGraph)
                    {
                        StopWorkflowWorker();
                    }

                    return "The WF graph is generated";
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
        }
    }
}


