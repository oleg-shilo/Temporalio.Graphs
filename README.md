_The project has been endorsed on Temporal's own [Code-Exchange portal](https://temporal.io/code-exchange/temporalio-graphs)._

---

## Problem Statement

When it comes to the WorkFlow (WF) engines, many of them are based on the concept of [Directed Acyclic Graph](https://en.wikipedia.org/wiki/Directed_acyclic_graph) (DAG). 

The obvious limitation of DAG (inability to support loops) comes with a great benefit - visualizing the whole WF is easy as it is often defined up front as a DSL specification of the complete graph.

Temporal belongs to the DAG-less family of WF engines. Thus, it offers somewhat limited visualization capabilities that are sacrificed for the more flexible architecture.  

Temporal (out of the box) only offers the WF visualization for the already executed steps - Timeline View. This creates a capability gap for the UI scenarios when it is beneficial to see the whole WF regardless of how far the execution progressed. The problem has been detected and even [discussed](https://community.temporal.io/t/see-workflow-as-a-dag/2010) in the Temporal community but without significant progress in addressing it. 

This project is an attempt to fill this gap.

## Solution
_Note: this project specifically targets .NET binding for Temporal. However, the concept used in this solution is simple to implement in any other Temporal SDK (language)._

_Temporalio.Graphs_ is a library (NuGet package) that can be used to generate a complete WF graph by running the WF in the mocked-run mode when WF activities are mocked and instead of being executed the activities simply  trigger logging WF steps that become the specification of the complete WF graph.

To achieve that, you will need to add the `Temporalio.Graphs` NuGet package to your worker project, add a special graph-building interceptor, and register the special activity defined in the `Temporalio.Graphs.GenericActivities` class.

These are the step-by-step instructions:

- Add `GraphBuilder` to your worker as an interceptor and GenericActivities in the Program.cs file:
  
  ```c#
  var workerOptions = new TemporalWorkerOptions(taskQueue: "MONEY_TRANSFER_TASK_QUEUE")
  {
      Interceptors = [new Temporalio.Graphs.GraphBuilder()]
  };
  
  workerOptions
    .AddAllActivities<Temporalio.Graphs.GenericActivities>() // Register graph "assistance" activity 
    .AddAllActivities(activities)                            // Register your activities
    .AddWorkflow<MoneyTransferWorkflow>();                   // Register your workflow

  . . .
  
  using var worker = new TemporalWorker(client, workerOptions);
  ```
  
    That's it. Now your solution is compatible with WF graph generation. Your WF worker is still as normal as it was before the change. The only change is that it is now capable of building the graph of your WF if it is executed in the graph-building mode. Thus in the graph-generation mode, the WF actions are mocked at runtime and instead the graph definition with the action names as graph nodes is generated. 

- This is how you can switch between normal and graph-generation modes based on CLI arguments of your worker process.

  ```c#
  bool isBuildingGraph = args.Contains("-graph");

  if (isBuildingGraph)
  {
      interceptor.Context = new (
          IsBuildingGraph: true,
          ExitAfterBuildingGraph: true,
          GraphOutputFile: typeof(MoneyTransferWorkflow).Assembly.Location.ChangeExtension(".graph"));

      await workerOptions.ExecuteWorkerInMemory(
          (MoneyTransferWorkflow wf) => wf.RunAsync(null));
  }
  else
  {
      // normal execution
      . . .
  ```

  en the graph is generated its definition (see code above) is written in the `*.graph` file next to the worker assembly file.

Note, that you can also generate the graph en you run your worker in the normal mode. You only need to supply `Temporalio.Graphs.GraphBuilingContext` as input for your workflow when you start it. The result of such a workflow will be the graph definition. See the [samples page](https://github.com/oleg-shilo/Temporalio.Graphs/wiki/Samples#moneytransfer-graph-client) for that.

When it comes to the way the WF grap is defined it needs to be simple and easy to work with format/syntax. In _Temporalio.Graph_ the primary syntax for graphs is Mermaid. Below is an example of the graph built for a sample WF:

WF graph definition:

   ````markdown
   ```mermaid
   flowchart LR
   s((Start)) --> Withdraw --> 0{NeedToConvert} -- yes --> CurrencyConvert --> 1{IsTFN_Known} -- yes --> NotifyAto --> Deposit --> e((End))
   1{IsTFN_Known} -- no --> TakeNonResidentTax --> Deposit
   0{NeedToConvert} -- no --> 1{IsTFN_Known}
   ```
   ````

As you can see _Temporalio.Graphs_ can even handle WF Decision nodes that are not natively supported by Temporal. _Temporalio.Graphs_ can also integrate Temporal signals (WaitCondition) even though it is not naturally present in a typical WF graph. See [Architectural Considerations page](https://github.com/oleg-shilo/Temporalio.Graphs/wiki/Architectural-Considerations#decision-nodes).

The same WF graph definition visualization with Mermaid rendered:

   ```mermaid
   flowchart LR
   s((Start)) --> Withdraw --> 0{NeedToConvert} -- yes --> CurrencyConvert --> 1{IsTFN_Known} -- yes --> NotifyAto --> Deposit --> e((End))
   1{IsTFN_Known} -- no --> TakeNonResidentTax --> Deposit
   0{NeedToConvert} -- no --> 1{IsTFN_Known}
   ```  

This repository contains the complete graph generation output [MoneyTransferWorkflow.grap](https://github.com/oleg-shilo/Temporalio.Graphs/blob/main/Samples/MoneyTransferWorker/MoneyTransferWorkflow.graph) that is produced by building the worker project in release mode. This is the same way you may want to integrate the generation of the static WF graph/diagram in your CI. 

Note, that the complete graph generation output contains: 
- Mermaid definition of the graph
- The list of all graph unique execution paths
- The graph validation warnings (e.g. activities defined in the WF but not executed during the run)

You can control what content should be included in the graph output (e.g. limit output to Mermaid content only).

## What you can do with _Temporalio.Graphs_

1. Generate static WF graph definition (Mermaid) file 
2. Generate WF graph definition dynamically on the running instance of Temporal Worker (e.g. in production)  
3. Host WF graph in a web application 
    - with the ability for the user to interact with graph elements (WF steps) and display selected element details   
    - showing the runtime state of the whole WF (e.g. current step, WF input and output)  

The samples of all product features listed above are captured in the samples [described here](https://github.com/oleg-shilo/Temporalio.Graphs/wiki/Samples).
Note, that points #1 and #2 are integral parts of the nuget package. Point #3 is a POC sample available in this repository to assist users of _Temporalio.Graphs_ to utilise WF graphs in their products. 

## Prerequisites

Before running this application, ensure you have the following installed:

* [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or later
* [Temporal CLI](https://learn.temporal.io/getting_started/dotnet/dev_environment/) (a ready-to-go version of Temporal CLI is [included in the repo](https://github.com/oleg-shilo/Temporalio.Graphs/tree/v1.0.0.0/Samples/MoneyTransfer.Graph.Client/temporal.cli))

## Steps to get started

### Exploring _Temporalio.Graphs_ capabilities

- Clone this repository
- Execute _run.cmd_ in the repository root. It will run a simple web application that shows cases all _Temporalio.Graphs_ features. See [sample description](https://github.com/oleg-shilo/Temporalio.Graphs/wiki/Samples#moneytransfer-graph-client). 

### Integrating _Temporalio.Graphs_ in your product

- Add _Temporalio.Graphs_ package from https://www.nuget.org/packages/OlegShilo.Temporalio.Graphs 
- Update your Temporal worker to use _Temporalio.Graphs_ grap-building interceptors as shown in the Solution section above or in the [worker sample in this repository](https://github.com/oleg-shilo/Temporalio.Graphs/blob/main/Samples/MoneyTransferWorker/Program.cs).
