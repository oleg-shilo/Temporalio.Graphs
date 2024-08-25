// Ignore Spelling: Workflow

using Temporalio.Activities;

namespace Temporalio.Graph.Tests;
public class GenericTests
{
    [Fact]
    public void DecisionIdGeneration()
    {
        var decisions = new TestWorkflowDecisions();
        var propId1 = decisions.First;
        var propId2 = decisions.Second;

        Assert.Equal("decision0", propId1);
        Assert.Equal("decision1", propId2);
    }

    [Fact]
    public void DagValidation_AllOK()
    {
        var mermaid = new DagGenerator();
        mermaid.Scenarios
            .AddGraph("Start,StepA,d0{First}:no,StepB,End")
            .AddGraph("Start,StepA,d0{First}:yes,StepC,StepD,End")
            .AddGraph("Start,d1{Second}:yes,End")
            .AddGraph("Start,d1{Second}:no,End");

        string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();
        Assert.Empty(errorMessage);
    }

    [Fact]
    public void DagValidation_MissingPermutation()
    {
        var mermaid = new DagGenerator();
        mermaid.Scenarios
            .AddGraph("Start,StepA,d0{First}:no,StepB,End")
            .AddGraph("Start,StepA,d0{First}:yes,StepC,StepD,End")
            .AddGraph("Start,d1{Second}:no,End");

        string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();

        Assert.NotEmpty(errorMessage);
        Assert.Contains($"full DAG: [{typeof(TestWorkflowDecisions)}.Second]", errorMessage);
    }

    [Fact]
    public void DagValidation_MissingDecision()
    {
        var mermaid = new DagGenerator();
        mermaid.Scenarios
            .AddGraph("Start,StepA,d1{IsCondition1}:no,StepB,End")
            .AddGraph("Start,StepA,d1{IsCondition1}:yes,StepC,StepD,End");

        string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();

        Assert.NotEmpty(errorMessage);
        Assert.Contains($"full DAG: [{typeof(TestWorkflowDecisions)}.First, {typeof(TestWorkflowDecisions)}.Second]", errorMessage);
    }
    [Fact]
    public void DagValidation_MissingActivity()
    {
        var mermaid = new DagGenerator();
        mermaid.Scenarios
            .AddGraph("Start,StepA,d1{IsCondition1}:no,StepB,End")
            .AddGraph("Start,StepA,d1{IsCondition1}:yes,StepC,End");

        string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();

        Assert.NotEmpty(errorMessage);
        Assert.Contains($"full DAG: [{typeof(TestWorkflowActivities)}.StepDAsync]", errorMessage);
    }
}
class TestWorkflowDecisions : DecisionsBase
{
    [Decision]
    public string First => base.Id();
    [Decision]
    public string Second => base.Id();
}

public class TestWorkflowActivities
{
    [Activity]
    public static async Task<string> StepAAsync() => "";
    [Activity]
    public static async Task<string> StepBAsync() => "";
    [Activity]
    public static async Task<string> StepDAsync() => "";
}
