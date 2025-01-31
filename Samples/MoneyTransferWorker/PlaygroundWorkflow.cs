// @@@SNIPSTART money-transfer-project-template-dotnet-workflow
#pragma warning disable CS8604 // Possible null reference argument for parameter
namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.Workflows;
using static Temporalio.Workflows.Workflow;
using Temporalio.Common;

[Workflow]
public class PlaygroundWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(object context)
    {
        string files = await ExecuteActivityAsync(
         (MiscActivities m) => m.DiscoverFilesAsync(@".\"), options);

        foreach (var file in (files ?? "33|55").Split('|'))
        {
            await ExecuteActivityAsync(
               (MiscActivities m) => m.LockFileAsync(file), options);

            await ExecuteActivityAsync(
               (MiscActivities m) => m.StartFileProcessingWorkflowAsync(file), options);
        }

        return "Done...";
    }

    static ActivityOptions options = new ActivityOptions
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(100),
            BackoffCoefficient = 2,
            MaximumAttempts = 500,
            NonRetryableErrorTypes = new[] { "InvalidAccountException", "InsufficientFundsException" }
        }
    };
}
