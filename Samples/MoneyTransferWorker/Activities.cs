// @@@SNIPSTART money-transfer-project-template-dotnet-withdraw-activity
namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;

using System.Runtime.CompilerServices;
using Temporalio.Graphs;
using Temporalio.Activities;
using Temporalio.Exceptions;

public class BankingActivities
{
    [Activity]
    public async Task<string> WithdrawAsync(PaymentDetails details)
    {
        //Console.WriteLine($"Withdrawing ${details.Amount} from account {details.SourceAccount}.");
        Console.WriteLine($">> {nameof(WithdrawAsync)}");
        await Task.Delay(2000);

        var bankService = new BankingService("bank1.example.com");

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
        //Console.WriteLine($"Depositing ${details.Amount} into account {details.TargetAccount}.");
        Console.WriteLine($">> {nameof(DepositAsync)}");
        await Task.Delay(2000);

        var bankService = new BankingService("bank2.example.com");

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
        //Console.WriteLine($"Refunding ${details.Amount} to account {details.SourceAccount}.");
        Console.WriteLine($">> {nameof(RefundAsync)}");
        await Task.Delay(2000);

        var bankService = new BankingService("bank1.example.com");

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
        Console.WriteLine($">> {nameof(CurrencyConvertAsync)}");
        await Task.Delay(2000);

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
        //Console.WriteLine($"CurrencyConvertAsync ${details.Amount} to account {details.SourceAccount}.");
        Console.WriteLine($">> {nameof(NotifyAtoAsync)}");
        await Task.Delay(2000);

        var bankService = new BankingService("bank1.example.com");
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
    [Decision]
    public static bool NeedToConvert(PaymentDetails details)
    {
        Console.WriteLine($">> {nameof(NeedToConvert)}?");
        Task.Delay(2000).Wait();
        return (details.Currency != "AUD");
    }

    [Activity]
    [Decision()]
    public static bool IsTFN_Known(PaymentDetails details)
    {
        Console.WriteLine($">> {nameof(IsTFN_Known)}?");
        Task.Delay(2000).Wait();
        return true;
    }

    [Activity]
    public static async Task<string> TakeNonResidentTaxAsync(PaymentDetails details)
    {
        Console.WriteLine($">> {nameof(TakeNonResidentTaxAsync)}");
        await Task.Delay(2000);

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
        return "";
    }
}

