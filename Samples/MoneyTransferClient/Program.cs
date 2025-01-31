// @@@SNIPSTART money-transfer-project-template-dotnet-start-workflow
// This file is designated to run the workflow
using Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.Client;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Channels;
using Temporalio.Exceptions;

// Connect to the Temporal server
var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });

// Define payment details
var details = new PaymentDetails(
    SourceAccount: "85-150",
    TargetAccount: "43-812",
    Amount: 400,
    Currency: "USD",
    ReferenceId: "12345"
);

var context = new Temporalio.Graphs.GraphBuildingContext(
    IsBuildingGraph: args.Contains("-graph"),
    ExitAfterBuildingGraph: args.Contains("-graph-exit"),
    GraphOutputFile: args.FirstOrDefault(x => x.StartsWith("-graph-out:"))?.Replace("-graph-out:", "")
);

Console.WriteLine($"Starting transfer from account {details.SourceAccount} to account {details.TargetAccount} for ${details.Amount}");

var workflowId = $"pay-invoice-{Guid.NewGuid()}";

try
{
    // If you want to pass the interception context you can modify `RunAsync` signature to accept
    // an additional parameter of type `ExecutionContext` and pass it here. And the interceptor will
    // detect and handle it.

    // Start the workflow
    var handle = await client.StartWorkflowAsync(
        (MoneyTransferWorkflow wf) => wf.RunAsync(details, context),
        new(id: workflowId, taskQueue: "MONEY_TRANSFER_TASK_QUEUE"));

    Console.WriteLine($"Started WorkFlowAsync {workflowId}");

    // Await the result of the workflow
    //var result = await handle.GetResultAsync();
    //Console.WriteLine($"WorkFlowAsync result: {result}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"WorkFlowAsync execution failed: {ex.Message}");
}

// @@@SNIPEND
