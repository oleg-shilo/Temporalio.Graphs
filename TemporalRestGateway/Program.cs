using Microsoft.Extensions.FileProviders;
using System;
using System.Diagnostics;
using System.Text.Json;

using Temporalio.Api.Enums.V1;
using Temporalio.Api.TaskQueue.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;
using Temporalio.Workflows;

// minapi: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0
//var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });
//grpc_GetJobsQueue();
//DiscoverTaskQueues();

//await grpc_GetQueues();
//await grpc_StartWorkflow();
//return;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Urls.Add("http://127.0.0.1:5027");
app.Urls.Add("https://127.0.0.1:7269");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseDefaultFiles();

app.MapGet("/api/start-server", StartServer);
app.MapGet("/api/start-worker", StartWorker);
app.MapGet("/api/stop-worker", StopWorker);
app.MapGet("/api/start-wf", grpc_StartWorkflow);
app.MapGet("/api/status", grpc_getStatus);

app.MapGet("/api/workflows/{wfId}/runs/{runId}", async (string wfId, string runId) =>
{
    var json = await grpc_GetWorkflowHistory(wfId, runId);
    return json;
});

app.MapGet("/api/workflows", async () =>
{
    var json = await grpc_GetWorkflows();
    return json;
});

app.Run();

//================================================================================

async Task<string> grpc_GetWorkflows()
{
    var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });

    var list = client.ListWorkflowsAsync("WorkflowType='MoneyTransferWorkflow'");

    var result = new List<object>();

    await foreach (WorkflowExecution execution in list)
    {
        result.Add(new
        {
            execution = new
            {
                workflow_id = execution.Id,
                run_id = execution.RunId
            }
        });
    }
    return JsonSerializer.Serialize(result);
}

async Task<string> grpc_GetWorkflowHistory(string workflowId, string runId)
{
    var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });
    var history = client.ListWorkflowHistoriesAsync($"WorkflowId='{workflowId}' AND RunId='{runId}'");
    await foreach (var item in history)
    {
        var json = item.ToJson(); // first page; good for now
        return json;
    }

    return "{events:[]}";
}

async Task grpc_StartWorkflow()
{
    Random random = new();

    var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });

    var workflowType = "MoneyTransferWorkflow";
    var workflowId = $"pay-invoice-{Guid.NewGuid()}";

    var input = new
    {
        SourceAccount = "85-150",
        TargetAccount = "43-812",
        Amount = (int)(random.NextSingle() * 100) + 10,
        Currency = random.NextSingle() > 0.5 ? "USD" : "AUD",
        ReferenceId = "12345"
    };

    await client.StartWorkflowAsync(workflowType, new[] { input }, new(id: workflowId, taskQueue: "MONEY_TRANSFER_TASK_QUEUE"));
}

async Task<string> grpc_getStatus()
{
    var executions = new List<object>();
    TemporalClient client = null;

    try
    {
        client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });
    }
    catch { }

    if (client != null)
    {
        var list = client.ListWorkflowsAsync("WorkflowType='MoneyTransferWorkflow'");

        await foreach (WorkflowExecution execution in list)
        {
            var completionStatus = "";

            if (execution.Status == WorkflowExecutionStatus.Running)
            {
                completionStatus = "pending";
                var history = client.ListWorkflowHistoriesAsync($"WorkflowId='{execution.Id}' AND RunId='{execution.RunId}'");
                var resultPage = await history.Take(1).FirstOrDefaultAsync();

                if (resultPage?.Events.Any(x => x.EventType == EventType.WorkflowTaskStarted) == true)
                    completionStatus = "inprogress";
            }
            else
                completionStatus = "processed";

            executions.Add(new
            {
                workflow_id = execution.Id,
                run_id = execution.RunId,
                start_time = execution.StartTime,
                close_time = execution.CloseTime,
                completion_status = completionStatus
            });
        }
    }
    var result = new
    {
        serverAvailable = client != null,
        workerAvailable = IsWorkerAvailable(),
        executions
    };

    return JsonSerializer.Serialize(result);
}

string cli_GetWorkflows()
{
    return TemporalQuery("workflow list --namespace default --query \"ExecutionStatus='Completed' OR ExecutionStatus='Running'\" -o json");
}

string cli_GetWorkflowHistory(string workflowId, string runId)
{
    var json = TemporalQuery($"workflow show -w {workflowId} -r {runId} -o json");
    return json.Any() ? json : "{[]}";
}

void StartServer()
{
    // terminate temporal process if running
    foreach (var item in Process.GetProcesses().Where(x => x.ProcessName.ToLower() == "temporal"))
        try
        {
            item.Kill();
        }
        catch { }

    Process.Start("temporal", "server start-dev");
}
bool IsWorkerAvailable()
   => Process.GetProcesses().Any(x => x.ProcessName.ToLower() == "moneytransferworker");

void StartWorker()
{
    var exe = @"..\Samples\MoneyTransferWorker\bin\Debug\net8.0\MoneyTransferWorker.exe";

    if (!File.Exists(exe))
        exe = exe.Replace("Debug", "Release");

    if (File.Exists(exe))
        Process.Start(exe);
}

void StopWorker()
{
    foreach (var item in Process.GetProcesses().Where(x => x.ProcessName.ToLower() == "moneytransferworker"))
        try
        {
            item.Kill();
        }
        catch { }
}

string TemporalQuery(string query)
{
    var stopWatch = Stopwatch.StartNew();

    var processStartInfo = new ProcessStartInfo
    {
        FileName = "temporal",
        Arguments = query,
        RedirectStandardOutput = true,
        UseShellExecute = false
    };

    // curl https://localhost:7269/api/workflows/pay-invoice-aaabe356-759c-4a8e-9214-488f5e774ead/runs/b1220178-61d9-4e90-a05e-428b2e6a0e7c
    using (var process = Process.Start(processStartInfo))
    {
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Console.WriteLine($"Elapsed: {stopWatch.ElapsedMilliseconds}ms");
        return output;
    }
}

