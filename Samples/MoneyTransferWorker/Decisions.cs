using System.Runtime.CompilerServices;
using Temporalio.Graphs;

namespace Temporalio.MoneyTransferProject.MoneyTransferWorker;
// @@@SNIPEND

class Decisions : DecisionsBase
{
    public static Action[] Profiles = // may need to generate them programmatically (e.g. nested loops) if too many decisions
    {
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => false, IsTFN_Known = x => true,  DepositFailed = true },
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => false, IsTFN_Known = x => false, DepositFailed = true },
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => true,  IsTFN_Known = x => true,  DepositFailed = true },
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => true,  IsTFN_Known = x => false, DepositFailed = true },
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => false, IsTFN_Known = x => true,  DepositFailed = false },
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => false, IsTFN_Known = x => false, DepositFailed = false },
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => true,  IsTFN_Known = x => true,  DepositFailed = false },
        ()=> ActiveProfile = new Decisions { NeedToConvert = x => true,  IsTFN_Known = x => false, DepositFailed = false },
    };

    public static Decisions ActiveProfile = new();

    // -------------------------------------------------------------------
    [Decision]
    public bool AbandonedTestDecision { set; get; }

    // -------------------------------------------------------------------
    [Decision]
    public bool DepositFailed
    {
        set { depositFailed = value; }

        get
        {
            if (Dag.IsBuildingGraph)
                Dag.ActiveGraph.AddDecision(Id(), depositFailed);

            return depositFailed;
        }
    }
    bool depositFailed = false;

    // -------------------------------------------------------------------
    [Decision]
    public Func<PaymentDetails, bool> IsTFN_Known
    {
        set { isTFN_Known = value; }

        get
        {
            if (Dag.IsBuildingGraph)
                Dag.ActiveGraph.AddDecision(Id(), isTFN_Known(default));

            return isTFN_Known;
        }
    }
    Func<PaymentDetails, bool> isTFN_Known = BankingService.IsTaxFileNumberKnown;
    // -------------------------------------------------------------------
    [Decision]
    public Func<PaymentDetails, bool> NeedToConvert
    {
        set { needToConvert = value; }

        get
        {
            if (Dag.IsBuildingGraph)
                Dag.ActiveGraph.AddDecision(Id(), needToConvert(default));

            return needToConvert;
        }
    }
    Func<PaymentDetails, bool> needToConvert = BankingService.NeedToConvert;
}