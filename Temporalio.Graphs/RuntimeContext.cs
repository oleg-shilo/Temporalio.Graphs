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
using System.Text.Json;

namespace Temporalio.Graphs;
internal class RuntimeContext
{
    public bool InitFrom(ExecuteWorkflowInput input)
    {
        var context = input.Args.OfType<GraphBuilingContext>().FirstOrDefault();
        if (context == null)
            try
            {
                // last hope attempt. The context can be passed as a JSON string
                var inputArg = input.Args.LastOrDefault()?.ToString();
                if (inputArg.IsNotEmpty())
                    context = JsonSerializer.Deserialize<GraphBuilingContext>(inputArg);
            }
            catch (Exception) { }
        return InitFrom(context);
    }

    public bool InitFrom(GraphBuilingContext? context)
    {
        if (context != null)
        {
            ClientRequest = context;
            return true;
        }
        return false;
    }
#pragma warning disable CS8603 // Possible null reference return.
    public Dictionary<(string Name, int Index), bool> CurrentDecisionsPlan => DecisionsPlans.FirstOrDefault();
#pragma warning restore CS8603 // Possible null reference return.

    public List<Dictionary<(string Name, int Index), bool>> DecisionsPlans = new();
    public bool IsBuildingGraph => ClientRequest?.IsBuildingGraph == true;
    public bool SplitNamesByWords => ClientRequest?.SplitNamesByWords == true;
    internal GraphBuilingContext? ClientRequest = null;
    public GraphPath CurrentGraphPath = new GraphPath();
    internal bool initialized = false;
}