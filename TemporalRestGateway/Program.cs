using Microsoft.Extensions.FileProviders;
using System.Diagnostics;
using System.Text.Json;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.TaskQueue.V1;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Client;

// minapi: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0
//var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });
//grpc_GetJobsQueue();
//DiscoverTaskQueues();

await grpc_GetQueues();
return;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Urls.Add("http://127.0.0.1:5027");
app.Urls.Add("https://127.0.0.1:7269");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseDefaultFiles();

string workFlowEventsJson = null;
string wfId = null, runId = null;

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

async Task<string> grpc_GetQueues()
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

string cli_GetWorkflows()
{
	return TemporalQuery("workflow list --namespace default --query \"ExecutionStatus='Completed' OR ExecutionStatus='Running'\" -o json");
}

string cli_GetWorkflowHistory(string workflowId, string runId)
{
	var json = TemporalQuery($"workflow show -w {wfId} -r {runId} -o json");
	return json.Any() ? json : "{[]}";
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


static async Task DiscoverTaskQueues()
{
	// Connect to Temporal gRPC server
	var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });

	// Create request for DescribeTaskQueue
	var request = new DescribeTaskQueueRequest
	{
		Namespace = "default",           // Replace with your Temporal namespace
		TaskQueue = new TaskQueue
		{
			Name = "MONEY_TRANSFER_TASK_QUEUE",     // Replace with the task queue name you want to query
			Kind = TaskQueueKind.Normal // Can be Normal or Sticky
		}
	};

	try
	{
		// Call DescribeTaskQueue gRPC method
		var response = await client.WorkflowService.DescribeTaskQueueAsync(request);

		// Print the response
		Console.WriteLine($"Task Queue: {response.TaskQueueStatus.TaskIdBlock}");
		Console.WriteLine($"Backlog Count: {response.TaskQueueStatus.BacklogCountHint}");

		// Display the poller information (i.e., workers polling the queue)
		foreach (var poller in response.Pollers)
		{
			Console.WriteLine($"Poller Identity: {poller.Identity}");
			Console.WriteLine($"Last Access Time: {poller.LastAccessTime}");
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"Error describing task queue: {ex.Message}");
	}
}