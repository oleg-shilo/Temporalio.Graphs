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
internal class RuntimeContext
{
    public bool InitFrom(ExecuteWorkflowInput input)
        => InitFrom(input.Args.OfType<GraphBuilingContext>().FirstOrDefault());

    public bool InitFrom(Temporalio.Graphs.GraphBuilingContext context)
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
    public Dictionary<(string Name, int Index), bool> CurrentDecisionsPlan => DecisionsPlans.FirstOrDefault();
    public List<Dictionary<(string Name, int Index), bool>> DecisionsPlans = new();
    public bool IsBuildingGraph;
    public bool ExitAfterBuildingGraph;
    public string? GraphOutputFile;
    public GraphPath CurrentGraphPath = new GraphPath();
    internal bool initialized = false;
}