using System.Runtime.CompilerServices;
using Temporalio.Graphs;

namespace Temporalio.Graphs.Tests;
public class MermaidGenerationTests
{
    [Fact]
    public void SingleDecisionDag()
    {
        var mermaid = new GraphGenerator();
        mermaid.Scenarios
            .AddGraph("Start,Withdraw,d{NeedToConvert}:no,Deposit,End")
            .AddGraph("Start,Withdraw,d{NeedToConvert}:yes,CurrencyConvert,Deposit,End");

        var diagram = mermaid.ToMermaidSyntax();

        Assert.Equal(
            """
            ```mermaid
            flowchart LR
            s((Start)) --> Withdraw --> d{NeedToConvert} -- yes --> CurrencyConvert --> Deposit --> e((End))
            d{NeedToConvert} -- no --> Deposit
            ```
            """,
            diagram);
    }

    [Fact]
    public void TwoDecisionDag()
    {
        var mermaid = new GraphGenerator();
        mermaid.Scenarios
            .AddGraph("Start,StepA,d1{IsCondition1}:no,StepB,End")
            .AddGraph("Start,StepA,d1{IsCondition1}:yes,StepC,d2{IsCondition2}:no,End")
            .AddGraph("Start,StepA,d1{IsCondition1}:yes,StepC,d2{IsCondition2}:yes,StepD,End");

        var diagram = mermaid.ToMermaidSyntax();

        Assert.Equal(
            """
            ```mermaid
            flowchart LR
            s((Start)) --> StepA --> d1{IsCondition1} -- yes --> StepC --> d2{IsCondition2} -- yes --> StepD --> e((End))
            d2{IsCondition2} -- no --> e((End))
            d1{IsCondition1} -- no --> StepB --> e((End))
            ```
            """,
            diagram);
    }


}

static class TestExtensions
{
    public static List<List<string>> AddGraph(this List<List<string>> scenarios, string elementsList)
    {
        scenarios.Add(elementsList.Split(',').ToList());
        return scenarios;
    }
}