// Ignore Spelling: Workflow
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CA1822 // Mark members as static
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Temporalio.Activities;

namespace Temporalio.Graphs.Tests;
public class GenericTests
{
    static Expression<Func<Task<string>>> GetExpression(Expression<Func<Task<string>>> activityCall)
    {
        return activityCall;
    }

    [Fact]
    public void Workflow_Events()
    {
        var embedCode = "ImRldGFpbHM/LkN1cnJlbmN5ICE9IFx1MDAyMkFVRFx1MDAyMiI=";
        byte[] decodedBytes = Convert.FromBase64String(embedCode);
        string decodedText = Encoding.UTF8.GetString(decodedBytes);
    }

    [Fact]
    public void Workflow_Decision()
    {
        System.Type ExpressionUtil = typeof(Temporalio.Common.RetryPolicy).GetTypeInfo().Assembly.GetType("Temporalio.Common.ExpressionUtil");

        var activityCall = GetExpression(() => TestWorkflowActivities.StepAAsync());
        Expression<Func<Task<string>>> activityCall2 = () => TestWorkflowActivities.StepAAsync();

        var methods = ExpressionUtil.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.GetParameters().Count() == 1);
        var rr = methods.Invoke(null, new object[] { activityCall });

        var result = ExpressionUtil.InvokeMember("ExtractCall", BindingFlags.InvokeMethod, null, null, new object[] { activityCall });
    }

    [Fact]
    public void Graps_ActivityLongNames()
    {
        var fullName = "Test.Activities.WithdrawAsync";

        var simpleName = fullName.ToSimpleNodeName();

        Assert.Equal("Withdraw", simpleName);
    }

    [Fact]
    public void Graps_DecisionLongNames()
    {
        var fullName = "d1{Test.Activities.NeedToConvertAsync}:yes";

        var simpleName = fullName.ToSimpleNodeName();

        Assert.Equal("d1{NeedToConvert}:yes", simpleName);
    }

    [Fact]
    public void DecisionIdGeneration()
    {
        Assert.Fail("not implemented");
        //var decisions = new TestWorkflowDecisions();
        //var propId1 = decisions.First;
        //var propId2 = decisions.Second;

        //Assert.Equal("decision0", propId1);
        //Assert.Equal("decision1", propId2);
    }

    [Fact]
    public void Decisions_Permutations()
    {
    }

    [Fact]
    public void DagValidation_AllOK()
    {
        var type = typeof(TestWorkflowActivities);

        var stepA = $"{type}.StepAAsync";
        var stepB = $"{type}.StepBAsync";
        var stepC = $"{type}.StepCAsync";
        var stepD = $"{type}.StepDAsync";

        var needToConvert = $"{type}.NeedToConvertAsync";
        var needToRefund = $"{type}.NeedToRefundAsync";

        var mermaid = new GraphGenerator();

        mermaid.Scenarios
            .AddGraph($"Start,{stepA},d0{{{needToConvert}}}:no,{stepB},End")
            .AddGraph($"Start,{stepA},d0{{{needToConvert}}}:yes,{stepC},{stepD},End")
            .AddGraph($"Start,d2{{{needToRefund}}}:yes,End")
            .AddGraph($"Start,d2{{{needToRefund}}}:no,{stepA}");

        string errorMessage = mermaid.ValidateGraphAgainst(typeof(TestWorkflowActivities).Assembly);

        Assert.Empty(errorMessage);
    }

    [Fact]
    public void DagValidation_MissingPermutation()
    {
        Assert.Fail("not implemented");
        //var mermaid = new DagGenerator();
        //mermaid.Scenarios
        //    .AddGraph("Start,StepA,d0{First}:no,StepB,End")
        //    .AddGraph("Start,StepA,d0{First}:yes,StepC,StepD,End")
        //    .AddGraph("Start,d1{Second}:no,End");

        //string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();

        //Assert.NotEmpty(errorMessage);
        //Assert.Contains($"full DAG: [{typeof(TestWorkflowDecisions)}.Second]", errorMessage);
    }

    [Fact]
    public void DagValidation_MissingDecision()
    {
        Assert.Fail("not implemented");
        //var mermaid = new DagGenerator();
        //mermaid.Scenarios
        //    .AddGraph("Start,StepA,d1{IsCondition1}:no,StepB,NeedToRefund,End")
        //    .AddGraph("Start,StepA,d1{IsCondition1}:yes,StepC,NeedToConvert,StepD,End");

        //string errorMessage = mermaid.ValidateAgainst<TestWorkflowActivities>();

        //Assert.NotEmpty(errorMessage);
        //Assert.Contains($"{typeof(TestWorkflowDecisions)}", errorMessage);
        //Assert.Contains($"{typeof(TestWorkflowDecisions)}.Second]", errorMessage);
    }
    [Fact]
    public void Decision_GenericDecision()
    {
        Assert.Fail("not implemented");
        //var result = new CodeDecisions();
        //GenericDecisionActivity.TakeDecision(() => result.DecisionResult1, "fff");
    }

    [Fact]
    public void DagValidation_MissingActivity()
    {
        var type = typeof(TestWorkflowActivities);

        var stepA = $"{type}.StepAAsync";
        var stepB = $"{type}.StepBAsync";
        var stepC = $"{type}.StepCAsync";

        var mermaid = new GraphGenerator();
        mermaid.Scenarios
            .AddGraph($"Start,{stepA},d1{{IsCondition1}}:no,{stepB},End")
            .AddGraph($"Start,{stepA},d1{{IsCondition1}}:yes,{stepC},End");

        string errorMessage = mermaid.ValidateGraphAgainst(typeof(TestWorkflowActivities).Assembly);

        Assert.NotEmpty(errorMessage);
        Assert.Contains($"{typeof(TestWorkflowActivities)}.StepDAsync", errorMessage);
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
        Assert.Fail("not implemented");

        //var decisions = Assembly.GetExecutingAssembly().GetDecisions();
        //var permutations = new List<Dictionary<(string Name, int Index), bool>>();
        //permutations.GeneratePermutationsFor(decisions);

        //Assert.Equal(2, decisions.Count());
        //Assert.Equal(2, permutations.Count());

        //Assert.Equal("NeedToConvertAsync", decisions[0].Name);
        //Assert.Equal("NeedToRefundAsync", decisions[1].Name);

        //Assert.False(permutations[decisions[0].Name].Plan.Pop());
        //Assert.True(permutations[decisions[0].Name].Plan.Pop());

        //Assert.False(permutations[decisions[1].Name].Plan.Pop());
        //Assert.True(permutations[decisions[1].Name].Plan.Pop());

        //Assert.Empty(permutations[decisions[0].Name].Plan);
        //Assert.Empty(permutations[decisions[1].Name].Plan);
    }
}

public class CodeDecisions
{
    public bool DecisionResult1 => true;
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
