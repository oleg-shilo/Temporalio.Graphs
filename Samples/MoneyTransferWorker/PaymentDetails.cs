// @@@SNIPSTART money-transfer-project-template-dotnet-shared
namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;
public record PaymentDetails(
    string SourceAccount,
    string TargetAccount,
    int Amount,
    string Currency,
    string ReferenceId);



// @@@SNIPEND