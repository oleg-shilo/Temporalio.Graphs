namespace Temporalio.MoneyTransferProject.BankingService.Exceptions;

public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(string message) : base(message) { }
}