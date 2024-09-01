## Problem Statement

When it comes to the WorkFlow (WF) engines, many of them based on the concept of [Directed Acyclic Graph](https://en.wikipedia.org/wiki/Directed_acyclic_graph) (DAG). 

The obvious limitation of DAG (inability to support loops) comes with the great benefit - visualizing the whole WF is easy as it is often defined up front as a DSL specification of the complete graph.

Temporal belongs to the DAG-less family of WF engines. Thus, of the box, it offers somewhat limited visualization capabilities that are sacrificed for the more flexible architecture.  

Temporal (out of the box) only offers the WF visualization for the already executed steps - Timeline View. This creates a capability gap for the UI scenarios when it is beneficial to see the whole WH regardless how far the execution progressed.

This project is an attempt to feel this gap.

## Solution

`Temporalio.Graphs` is a library (NuGet package) that can be used to generate a complete WF graph by running the WF in the mocked-run mode when all WF activities are mocked and only log the graph step during the execution.

In order to achieve that you will need to add `Temporalio.Graphs` NuGet package to your worker project and then do the following steps:

- Add `GraphBuilder` to your worker as an interceptor in the Program.cs file:
  ```c#
   var workerOptions = new TemporalWorkerOptions(taskQueue: "MONEY_TRANSFER_TASK_QUEUE")
   {
       Interceptors = [new Temporalio.Graphs.GraphBuilder()]
   };
   . . .
   using var worker = new TemporalWorker(client, workerOptions);
  ```

- Add an extra parameter `ExecutionContext` to your WF definition. The WF client application will supply this parameter to indicate that the graph needs to be generated. 
  ```C#
  [Workflow]
  public class MoneyTransferWorkflow
  {
      [WorkflowRun]
      public async Task<string> RunAsync(PaymentDetails details, ExecutionContext context)
      {
         . . .
  ```

That's it. Now you can run your WF either as normal or in a graph-generation mode when all WF actions are replaced at runtime with mocks and the graph definition is generated. 

This is how you can do it from the client application.

```c#
var context = new Temporalio.Graphs.ExecutionContext(
    IsBuildingGraph: true // e.g. read it from CLI args
);

var handle = await client.StartWorkflowAsync(
    (MoneyTransferWorkflow wf) => wf.RunAsync(details, context),
    new(id: workflowId, taskQueue: "MONEY_TRANSFER_TASK_QUEUE"));
```

When the graph is generated its definition (see section below) is printed in the console window. Alternatively you can redirect it to the file (UNC or relative to the worker location). Use `ExecutionContext.GraphOutputFile` for that.  

Note WF decision is a special type of a WF action (step) that requires special way of declaring it. See "Decision Action" section further in the text.

### Graphs Output

When the graph is generated the result is either printed in the console output or to the file. The result is a text that consist of three sections as on the screenshot below:

![image](https://github.com/user-attachments/assets/2ec48cfb-18b0-4a5c-9460-1ec1368dcbce)

1. The first section is the actual WF graph. This is the primary graph generation result. In the section text each line represents a graph unique path. If there is no decision node in the WF graph then there is only one path possible. The path definition is captured in this simple format:

   ```
   Start > step1_name > ... > stepN_name > End
   ```

   The decision nodes are just as ordinary nodes (steps) but since decisions have richer execution context their names include the decision id and the result (yes or no):
   
   ```
   id{Name}:result
   ```

   The decision result defines the execution outcome - a single graph path. The amount of possible WF path is the amount of all permutations of the decisions int the WF. Thus if there are two decisions ro be made at runtime then there are four possible execution paths (graph paths). Thus the graph section will have four lines in total. 

   ```txt
   Start > Withdraw > 0{NeedToConvert}:yes > CurrencyConvert > 1{IsTFN_Known}:yes > NotifyAto > Deposit > End
   Start > Withdraw > 0{NeedToConvert}:yes > CurrencyConvert > 1{IsTFN_Known}:no > TakeNonResidentTax > Deposit > End
   Start > Withdraw > 0{NeedToConvert}:no > 1{IsTFN_Known}:yes > NotifyAto > Deposit > End
   Start > Withdraw > 0{NeedToConvert}:no > 1{IsTFN_Known}:no > TakeNonResidentTax > Deposit > End
   ```

   You can use the graph definition to visualize WF in front-end app. Parsing/interpreting the definition is quite easy due to the very simple syntax.

2. The second section is... well, secondary. It contains an alternative syntax of the WF definition -  Mermaid syntax. It is a great way to verify the accuracy of the generated graph. Just paste the section content in any Mermaid rendering host. IE GitHub markdown document renders Mermaid diagrams natively. Below is the Mermaid specification from the screenshot above that is rendered by Github: 

   ````markdown
   ```mermaid
   flowchart LR
   s((Start)) --> Withdraw --> 0{NeedToConvert} -- yes --> CurrencyConvert --> 1{IsTFN_Known} -- yes --> NotifyAto --> Deposit --> e((End))
   1{IsTFN_Known} -- no --> TakeNonResidentTax --> Deposit
   0{NeedToConvert} -- no --> 1{IsTFN_Known}
   ```
   ````

   ```mermaid
   flowchart LR
   s((Start)) --> Withdraw --> 0{NeedToConvert} -- yes --> CurrencyConvert --> 1{IsTFN_Known} -- yes --> NotifyAto --> Deposit --> e((End))
   1{IsTFN_Known} -- no --> TakeNonResidentTax --> Deposit
   0{NeedToConvert} -- no --> 1{IsTFN_Known}
   ```

3. The third section contains validation result. The validation is performed at the end of the graph generation. The validation is a simple technique of verifying that all the Temporal Actions defined in the assembly are captured in the graph. If not, then it may mean that you made a mistake in your WF definition or just have some not needed WF actions defined.

   ```
   WARNING: the following activities are not present in the full WF graph:
   Temporalio.MoneyTransferProject.MoneyTransferWorker.BankingActivities.RefundAsync,
   Temporalio.MoneyTransferProject.MoneyTransferWorker.BankingActivities.DeliberatelyAbandonedActivityAsync
   ```

## How it works

This section is still under construction

```mermaid
%%{init: {"sequence": {"mirrorActors": false}} }%%

sequenceDiagram
   actor client as WF Client
   participant worker as Worker
   participant wf-int as WF Interceptor
   participant wf as WF
   participant act-int as Activity Interceptor
   participant act as Activities
   participant graph as Graph Generator

   %% ---------------------------------

   worker ->>+ worker: Setup Interceptor
   
   Note over worker,graph: Production  execution
   client ->> worker: Start WF 
   
   worker ->> wf-int: WF entry-point 
   activate wf-int
   wf-int ->> wf: Run WF
   activate wf
   loop Every WF activity
      wf ->> act-int: Activity 
      activate act-int
      act-int ->> act: Activity 
   end
   deactivate act-int
   deactivate wf
   deactivate wf-int

   %% ---------------------------------

   Note over worker,graph: Building a graph
   client ->> worker: Start WF (IsBuildingGraph: true)

   worker ->> wf-int: WF entry-point 
   activate wf-int
   wf-int ->> wf: Run WF
   activate wf
   loop Every WF activity
      wf ->> act-int: Activity 
      activate act-int
      act-int ->> graph: Add graph step 
   end
   deactivate act-int
   deactivate wf
   deactivate wf-int
```

## Prerequisites

Before running this application, ensure you have the following installed:

* [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or later
* [Temporal CLI](https://learn.temporal.io/getting_started/dotnet/dev_environment/)

## Steps to get started

1. _**Build the solution**_
2. _**Start the Temporal Server**_
   `temporal server start-dev`
3. _**Start the WF worker**_
   `MoneyTransferWorker.exe`
4. _**Start the WF in build graph mode**_
   `MoneyTransferClient.exe -graph`

The WF worker will print the unique execution graphs for the WF executed in the mocked mod. It will also print the Mermaid diagram representing the whole DAG as well as the WF graphs validation result.
