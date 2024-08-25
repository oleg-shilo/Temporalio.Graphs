// @@@SNIPSTART money-transfer-project-template-dotnet-workflow
namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.MoneyTransferProject.BankingService.Exceptions;
using Temporalio.Workflows;
using static Temporalio.Workflows.Workflow;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Graph;
using System.Numerics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Temporalio.Api.Protocol.V1;

[Workflow]
public class MoneyTransferWorkflow
{
    internal static Action Stop;

    [WorkflowRun]
    public async Task<string> RunAsync(PaymentDetails details, ExecutionContext context)
    {
        if (!context.IsBuildingGraph)
        {
            // normal execution
            return await WorkflowAsync(details);
        }
        else
        {
            // run WF with the DAG generator for all permutations of WF decisions (profiles)
            var dagInfo = await DagGenerator.Play(
                () => WorkflowAsync(details),
                Decisions.Profiles);

            var graphs = dagInfo.ToGraphs();
            var mermaid = dagInfo.ToMermaidSyntax();
            var validationResult = dagInfo.ValidateAgainst<BankingActivities>();

            Console.WriteLine("=====================");
            Console.WriteLine(graphs);
            Console.WriteLine("--------");
            Console.WriteLine(mermaid);
            Console.WriteLine("--------");
            Console.WriteLine(validationResult);
            Console.WriteLine("=====================");

            MoneyTransferWorkflow.Stop();

            return "All Graphs are generated";
        }
    }

    public async Task<string> WorkflowAsync(PaymentDetails details)
    {
        string withdrawResult = await ExecuteActivityAsync(
            () => BankingActivities.WithdrawAsync(details), options);

        if (Decisions.ActiveProfile.NeedToConvert(details))
        {
            await ExecuteActivityAsync(
                () => BankingActivities.CurrencyConvertAsync(details), options);
        }

        if (Decisions.ActiveProfile.IsTFN_Known(details))
        {
            await ExecuteActivityAsync(
                () => BankingActivities.NotifyAtoAsync(details), options);
        }
        else
        {
            await ExecuteActivityAsync(
                () => BankingActivities.TakeNonResidentTaxAsync(details), options);
        }

        string depositActionResult = "";
        string depositError = "";
        try
        {
            string depositResult = await ExecuteActivityAsync(
                () => BankingActivities.DepositAsync(details), options);

            // If everything succeeds, return transfer complete
            depositActionResult = $"Transfer complete (transaction IDs: {withdrawResult}, {depositResult})";
        }
        catch (ApplicationFailureException ex)
        {
            depositError = ex.Message;
            Decisions.ActiveProfile.DepositFailed = true;
        }

        // need to retrieve the decision value so it can be recorded during the graph generation

        if (Decisions.ActiveProfile.DepositFailed)
        {
            try
            {
                // if the deposit fails, attempt to refund the withdrawal
                string refundResult = await Workflow.ExecuteActivityAsync(
                    () => BankingActivities.RefundAsync(details), options);

                // If refund is successful, but deposit failed
                throw new ApplicationFailureException($"Failed to deposit money into account {details.TargetAccount}. Money returned to {details.SourceAccount}. Cause: {depositError}");
            }
            catch (ApplicationFailureException refundEx)
            {
                // If both deposit and refund fail
                throw new ApplicationFailureException($"Failed to deposit money into account {details.TargetAccount}. Money could not be returned to {details.SourceAccount}. Cause: {refundEx.Message}", refundEx);
            }
        }
        else
            return depositActionResult;
    }

    ActivityOptions options = new ActivityOptions
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
// @@@SNIPEND