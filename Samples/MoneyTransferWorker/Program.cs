// @@@SNIPSTART money-transfer-project-template-dotnet-worker
// This file is designated to run the worker
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.Graphs;
using System.Diagnostics;

// Cancellation token to shutdown worker on ctrl+c
using var tokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{   
    tokenSource.Cancel();
    eventArgs.Cancel = true;
};

// Create an instance of the activities since we have instance activities.
// If we had all static activities, we could just reference those directly.
var activities = new BankingActivities();


var interceptor = new GraphBuilder(tokenSource.Cancel);

var workerOptions = new TemporalWorkerOptions(taskQueue: "MONEY_TRANSFER_TASK_QUEUE")
{
    Interceptors = [interceptor]
};

workerOptions
    .AddAllActivities<Temporalio.Graphs.GenericActivities>()
    .AddAllActivities(activities)           // Register activities
    .AddWorkflow<MoneyTransferWorkflow>();  // Register workflow

// ========================================================================================

bool isBuildingGraph = args.Contains("-graph");

if (isBuildingGraph) // graph building mode
{
    interceptor.ClientRequest = new Temporalio.Graphs.GraphBuilingContext(
        IsBuildingGraph: true,
        ExitAfterBuildingGraph: true,
        GraphOutputFile: Path.GetFullPath(Path.Combine(typeof(MoneyTransferWorkflow).Assembly.Location, "..", "..", "..", "..", "MoneyTransferWorkflow.graph")),
        SplitNamesByWords: true
        // if you need to modify edge nodes with Mermaid node syntax
        //StartNode: "s((In))",
        //EndNode: "e((Out))"
        );

    await workerOptions.ExecuteWorkerInMemory(
        (MoneyTransferWorkflow wf) => wf.RunAsync(null, null));
}
else // normal mode
{
    // Create a client to connect to localhost on "default" namespace 
    var client = await TemporalClient.ConnectAsync(
        new("localhost:7233")
        {
        });

    // Create a worker with the activity and workflow registered
    using var worker = new TemporalWorker(client, workerOptions);

    // Run the worker until it's cancelled
    Console.WriteLine("Running worker...");
    try
    {
        await worker.ExecuteAsync(tokenSource.Token);
    }
    catch (OperationCanceledException) // check and unpack for aggregated exception here as well and
    {
        Console.WriteLine("Worker canceled");
    }
}
// @@@SNIPEND
