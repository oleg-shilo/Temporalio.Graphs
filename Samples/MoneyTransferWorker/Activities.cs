// @@@SNIPSTART money-transfer-project-template-dotnet-withdraw-activity
namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;

using System.Runtime.CompilerServices;
using Temporalio.Graph;
using Temporalio.Activities;
using Temporalio.Exceptions;

public class BankingActivities
{
    [Activity]
    public static async Task<string> WithdrawAsync(PaymentDetails details)
    {
        if (Dag.IsBuildingGraph)
            return Dag.ActiveGraph.AddStep();

        var bankService = new BankingService("bank1.example.com");
        Console.WriteLine($"Withdrawing ${details.Amount} from account {details.SourceAccount}.");
        try
        {
            return await bankService.WithdrawAsync(details.SourceAccount, details.Amount, details.ReferenceId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException("Withdrawal failed", ex);
        }
    }
    // @@@SNIPEND

    // @@@SNIPSTART money-transfer-project-template-dotnet-deposit-activity
    [Activity]
    public static async Task<string> DepositAsync(PaymentDetails details)
    {
        if (Dag.IsBuildingGraph)
            return Dag.ActiveGraph.AddStep();

        var bankService = new BankingService("bank2.example.com");
        Console.WriteLine($"Depositing ${details.Amount} into account {details.TargetAccount}.");

        // Uncomment below and comment out the try-catch block below to simulate unknown failure
        //*
        //return await bankService.DepositThatFailsAsync(details.TargetAccount, details.Amount, details.ReferenceId);
        //*/

        try
        {
            return await bankService.DepositAsync(details.TargetAccount, details.Amount, details.ReferenceId);
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException("Deposit failed", ex);
        }
    }
    // @@@SNIPEND

    // @@@SNIPSTART money-transfer-project-template-dotnet-refund-activity
    [Activity]
    public static async Task<string> RefundAsync(PaymentDetails details)
    {
        if (Dag.IsBuildingGraph)
            return Dag.ActiveGraph.AddStep();

        var bankService = new BankingService("bank1.example.com");
        Console.WriteLine($"Refunding ${details.Amount} to account {details.SourceAccount}.");
        try
        {
            return await bankService.RefundAsync(details.SourceAccount, details.Amount, details.ReferenceId);
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException("Refund failed", ex);
        }
    }

    [Activity]
    public static async Task<string> CurrencyConvertAsync(PaymentDetails details)
    {
        if (Dag.IsBuildingGraph)
            return Dag.ActiveGraph.AddStep();

        var bankService = new BankingService("bank1.example.com");

        try
        {
            (var amount, var referenceId) = await bankService.CurrencyConvertAsync(details.Amount, details.ReferenceId);
            return referenceId;
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException("CurrencyConvertAsync failed", ex);
        }
    }

    [Activity]
    public static async Task<string> NotifyAtoAsync(PaymentDetails details)
    {
        if (Dag.IsBuildingGraph)
            return Dag.ActiveGraph.AddStep();

        var bankService = new BankingService("bank1.example.com");
        Console.WriteLine($"CurrencyConvertAsync ${details.Amount} to account {details.SourceAccount}.");
        try
        {
            var referenceId = await bankService.NotifyAtoAsync(details.Amount, details.ReferenceId);
            return referenceId;
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException("CurrencyConvertAsync failed", ex);
        }
    }

    [Activity]
    public static async Task<string> TakeNonResidentTaxAsync(PaymentDetails details)
    {
        if (Dag.IsBuildingGraph)
            return Dag.ActiveGraph.AddStep();

        var bankService = new BankingService("bank1.example.com");

        try
        {
            (var amountAfterTax, var referenceId) = await bankService.TakeNonResidentTaxAsync(details.Amount, details.ReferenceId);
            return referenceId;
        }
        catch (Exception ex)
        {
            throw new ApplicationFailureException("CurrencyConvertAsync failed", ex);
        }
    }

    [Activity]
    public static async Task<string> DeliberatelyAbandonedActivityAsync(PaymentDetails details)
    {
        if (Dag.IsBuildingGraph)
            return Dag.ActiveGraph.AddStep();

        return "";
    }
}
