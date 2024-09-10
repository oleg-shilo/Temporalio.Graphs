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
public record ExecutionContext(bool IsBuildingGraph, bool ExitAfterBuildingGraph, string? GraphOutputFile);

public class GraphBuilder : IWorkerInterceptor
{
    static public Action StopWorkflowWorker { get; set; } = () => { };

    static internal Dictionary<string, RuntimeContext> Sessions = new();

    public ExecutionContext Context { get; set; }

    public GraphBuilder(Action stopWorker)
    {
        StopWorkflowWorker = stopWorker;
    }

    public class RuntimeContext
    {
        public bool InitFrom(ExecuteWorkflowInput input)
            => InitFrom(input.Args.OfType<ExecutionContext>().FirstOrDefault());

        public bool InitFrom(Temporalio.Graphs.ExecutionContext context)
        {
            if (context != null)
            {
                IsBuildingGraph = context.IsBuildingGraph;
                ExitAfterBuildingGraph = context.IsBuildingGraph;
                GraphOutputFile = context.GraphOutputFile;
                return true;
            }
            return false;
        }
        public Dictionary<(string Name, int Index), bool> CurrentDecisionsPlan => DecisionsPlan.FirstOrDefault();
        public List<Dictionary<(string Name, int Index), bool>> DecisionsPlan = new();
        public bool IsBuildingGraph;
        public bool ExitAfterBuildingGraph;
        public string? GraphOutputFile;
        public GraphPath CurrentGraphPath = new GraphPath();
        internal bool initialized = false;
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

                    var decision = Runtime.CurrentDecisionsPlan?
                        .Where(x => x.Key.Name == activityName)
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

                        return activityMethod.ReturnType.IsValueType
                            ? Activator.CreateInstance(activityMethod.ReturnType)
                            : null;
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

        ExecutionContext context;
        public WorkflowInbound(WorkflowInboundInterceptor next, ExecutionContext context) : base(next)
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

                    Runtime.DecisionsPlan.GeneratePermutationsFor(workflowAssembly.GetDecisions());

                    var generator = new GraphGenerator();

                    // run once if no permutations or run as many times as permutations count

                    var totalPermutations = Runtime.DecisionsPlan.Count; // capture it now as the plan will be cleared as it executes

                    for (int i = 0; i < totalPermutations; i++)
                    {
                        Runtime.CurrentGraphPath
                               .Clear()
                               .AddStep("Start");

                        // executing the original WF where the activities will be mocked
                        await base.ExecuteWorkflowAsync(input);

                        Runtime.CurrentGraphPath.AddStep("End");

                        generator.Scenarios.AddPath(Runtime.CurrentGraphPath);

                        Runtime.DecisionsPlan.RemoveAt(0);
                    }

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


