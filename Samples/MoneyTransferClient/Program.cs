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

var context = new Temporalio.Graphs.GraphBuilingContext(
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


//void Test()
//{
//	// Step 1: Create a channel to connect to the Temporal gRPC service.
//	var channel = Channel.CreateBounded(( ("localhost:7233", ChannelCredentials.Insecure); // Adjust the address to your Temporal server.

//	// Step 2: Create a gRPC client for the Temporal service.
//	var client = new WorkflowService. WorkflowServiceClient(channel);

//	// Step 3: Define a request to list workflows.
//	var listRequest = new ListWorkflowExecutionsRequest
//	{
//		Namespace = "default", // Change this to your Temporal namespace.
//		PageSize = 10, // Number of workflows to fetch per page.
//		Query = "WorkflowType='YourWorkflowTypeName'", // Optional query to filter workflows by type.
//	};

//	// Step 4: Make the call to list workflow executions.
//	try
//	{
//		var response = client.ListWorkflowExecutions(listRequest);

//		Console.WriteLine("Listing Workflows:");
//		foreach (var execution in response.Executions)
//		{
//			Console.WriteLine($"WorkflowId: {execution.Execution.WorkflowId}, RunId: {execution.Execution.RunId}, Type: {execution.Type.Name}");
//		}
//	}
//	catch (RpcException e)
//	{
//		Console.WriteLine($"Error listing workflows: {e.Message}");
//	}

//	// Step 5: Shut down the channel after use.
//	await channel.ShutdownAsync();
//}

