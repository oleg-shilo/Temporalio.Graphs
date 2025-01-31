// @@@SNIPSTART money-transfer-project-template-dotnet-workflow
#pragma warning disable CS8604 // Possible null reference argument for parameter
namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;
using Temporalio.MoneyTransferProject.BankingService.Exceptions;
using Temporalio.Workflows;
using static Temporalio.Workflows.Workflow;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Graphs;
using System.Numerics;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Temporalio.Api.Protocol.V1;
using Temporalio.Worker.Interceptors;
using System.Linq.Expressions;
using System.Reflection;
using Temporalio.Activities;
using Microsoft.Extensions.Options;


[Workflow]
public class MoneyTransferWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(PaymentDetails details, object context)
    {
        string withdrawResult = await ExecuteActivityAsync(
            (BankingActivities b) => b.WithdrawAsync(details), options);

        //bool needToConvert = await WF.Decision(() => BankingActivities.NeedToConvert(details));

        bool needToConvert = await (details?.Currency != "AUD").ToDecision();

        if (needToConvert)
        {
            await ExecuteActivityAsync(
                () => BankingActivities.ConvertCurrencyAsync(details), options);
        }

        //bool isTFN_Known = await WF.Decision(() => BankingActivities.IsTFN_Known(details));
        bool isTFN_Known = await (details?.TargetAccount?.StartsWith("AU_") == true)
                                                         .ToDecision("Is TFN Known");
        if (isTFN_Known)
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
        }

        // simulate a long running check
        BankingActivities.CheckWithInterpol(ref interpolCheck);

        // need to retrieve the decision value so it can be recorded during the graph generation
        var isIllegal = await WF.WaitConditionAsync(
               () =>
               {
                   return interpolCheck;
               },
               TimeSpan.FromMicroseconds(BankingActivities.averageActivityDuration),
               conditionName: "Interpol Check"
           );

        if (isIllegal)
        {
            await Workflow.ExecuteActivityAsync(
               () => BankingActivities.RefundAsync(details), options);

            await Workflow.ExecuteActivityAsync(
               () => BankingActivities.NotifyPoliceAsync(details), options);
        }

        return depositActionResult;
    }

    bool interpolCheck = false;

    [WorkflowSignal]
    public Task FlagAsIllegal()
    {
        interpolCheck = true;
        return Task.CompletedTask;
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
