// Ignore Spelling: Workflow

using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Temporalio.Activities;

namespace Temporalio.Graphs.Tests;
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
            .AddGraph("Start,d1{Second}:yes,d2{NeedToConvert}:yes,End")
            .AddGraph("Start,d1{Second}:yes,d2{NeedToConvert}:no,End")
            .AddGraph("Start,d1{Second}:no,d3{NeedToRefund}:yes,End")
            .AddGraph("Start,d1{Second}:no,d3{NeedToRefund}:no,End");

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
            .AddGraph("Start,StepA,d1{IsCondition1}:no,StepB,NeedToRefund,End")
            .AddGraph("Start,StepA,d1{IsCondition1}:yes,StepC,NeedToConvert,StepD,End");

        string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();

        Assert.NotEmpty(errorMessage);
        Assert.Contains($"{typeof(TestWorkflowDecisions)}", errorMessage);
        Assert.Contains($"{typeof(TestWorkflowDecisions)}.Second]", errorMessage);
    }
    [Fact]
    public void DagValidation_MissingActivity()
    {
        var mermaid = new DagGenerator();
        mermaid.Scenarios
            .AddGraph("Start,StepA,d1{IsCondition1}:no,StepB,End")
            .AddGraph("Start,StepA,d1{IsCondition1}:yes,needStepC,End");

        string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();

        Assert.NotEmpty(errorMessage);
        Assert.Contains($"full DAG: [{typeof(TestWorkflowActivities)}.StepDAsync]", errorMessage);
    }

    [Fact]
    public void ActivityDefinition_ActivityRawName()
    {
        var definition = ActivityDefinition.CreateAll(new TestActivityDecisions()).First();

        var method = definition.GetActivityMethod();

        Assert.Equal("NeedToConvertAsync", method.Name);
    }

    [Fact]
    public void Decisions_SetupPermutations()
    {
        dynamic t = 1;
        t = "";

        var decisions = Assembly.GetExecutingAssembly().GetDecisions();

        var permutations = decisions.SetupPermutations();


        Assert.Equal(2, decisions.Count());
        Assert.Equal(2, permutations.Count());

        Assert.Equal("NeedToConvertAsync", decisions[0].Name);
        Assert.Equal("NeedToRefundAsync", decisions[1].Name);

        Assert.False(permutations[decisions[0].Name].Plan.Pop());
        Assert.True(permutations[decisions[0].Name].Plan.Pop());

        Assert.False(permutations[decisions[1].Name].Plan.Pop());
        Assert.True(permutations[decisions[1].Name].Plan.Pop());

        Assert.Empty(permutations[decisions[0].Name].Plan);
        Assert.Empty(permutations[decisions[1].Name].Plan);
    }
}
class TestWorkflowDecisions : DecisionsBase
{
    [Decision]
    public string First => base.Id();
    [Decision]
    public string Second => base.Id();
}

public class TestActivityDecisions
{
    [Decision]
    [Activity]
    public async Task<bool> NeedToConvertAsync() => true;

    [Decision]
    [Activity]
    public async Task<bool> NeedToRefundAsync() => true;

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
