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

app.MapGet("/api/start-server", Api.StartServer);
app.MapGet("/api/stop-server", Api.StopServer);
app.MapGet("/api/start-worker", Api.StartWorker);
app.MapGet("/api/stop-worker", Api.StopWorker);
app.MapGet("/api/start-wf", Api.grpc_StartWorkflow);
app.MapGet("/api/status", Api.grpc_getStatus);

app.MapGet("/api/workflows/{wfId}/runs/{runId}", async (string wfId, string runId) =>
{
    var json = await Api.grpc_GetWorkflowHistory(wfId, runId);
    return json;
});

app.MapGet("/api/workflows", async () =>
{
    var json = await Api.grpc_GetWorkflows();
    return json;
});

app.Run();

