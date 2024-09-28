using System;
#pragma warning disable CS4014
// minapi: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-8.0

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
app.MapGet("/api/start-wf-graph", Api.grpc_StartWorkflowGraphBuild);
app.MapGet("/api/status", Api.grpc_getStatus);
app.MapGet("/api/workflows", Api.grpc_GetWorkflows);
app.MapGet("/api/workflows/{wfId}/runs/{runId}",
    async (string wfId, string runId) => await Api.grpc_GetWorkflowHistory(wfId, runId));

Api.ContentRootPath = app.Environment.ContentRootPath;

Task.Run(() =>
{
    Console.WriteLine("");
    Task.Delay(3000).Wait();
    Console.WriteLine("");

    Console.WriteLine("***************************************************");
    Console.WriteLine("*  Navigate to https://localhost:7269/index.html  *");
    Console.WriteLine("***************************************************");
});

app.Run();

