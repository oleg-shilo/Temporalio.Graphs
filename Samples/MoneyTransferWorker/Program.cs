// @@@SNIPSTART money-transfer-project-template-dotnet-worker
// This file is designated to run the worker
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.Graphs;
using System.Diagnostics;

// Create a client to connect to localhost on "default" namespace
var client = await TemporalClient.ConnectAsync(
    new("localhost:7233")
    {
    });

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

var workerOptions = new TemporalWorkerOptions(taskQueue: "MONEY_TRANSFER_TASK_QUEUE")
{
    Interceptors = [new GraphBuilder()]
};

workerOptions
    .AddAllActivities(activities)           // Register activities
    .AddWorkflow<MoneyTransferWorkflow>();  // Register workflow

// Create a worker with the activity and workflow registered
using var worker = new TemporalWorker(client, workerOptions);

//#if DEBUG
//Task.Run(() =>
//{
//    //Thread.Sleep(2000);
//    Process.Start(@".\..\..\MoneyTransferClient\bin\Debug\net8.0\MoneyTransferClient.exe", "-graph");
//});

//#endif

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
// @@@SNIPEND

