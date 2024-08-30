## Problem Statement

When it comes to the WorkFlow (WF) engines. Many of them flw

## Solution

### Graphs

The 

### The whole DAG

```mermaid
%%{init: {'securityLevel': 'loose'}}%%
flowchart LR
  A[This is an <b>important</b> <a href='https://google.com'>link</a>]
s((Start)) --> Withdraw --> decision2{NeedToConvert} -- yes --> CurrencyConvert --> decision1{IsTFN_Known} -- yes --> NotifyAto --> Deposit --> e((End))
decision1{IsTFN_Known} -- no --> TakeNonResidentTax --> Deposit
decision2{NeedToConvert} -- no --> decision1{IsTFN_Known}
```

### Graph Validation
```
WARNING: the following activities are not present in the full DAG: [Temporalio.MoneyTransferProject.MoneyTransferWorker.BankingActivities.DeliberatelyAbandonedActivityAsync]
This is either because the activity was not run during the WF execution or because the activity does not have the following statement as the first line in the implementation block:
    if (Dag.IsBuildingGraph)
        return Dag.ActiveGraph.AddStep();
WARNING: the following decisions are not present in the full DAG: [Temporalio.MoneyTransferProject.MoneyTransferWorker.Decisions.AbandonedTestDecision]
This is either because the decision or its all permutations were not evaluated during the WF execution or because it was not implemented correctly.
See Temporalio.Graphs samples.
```

## How it works


```mermaid
%%{init: {"sequence": {"mirrorActors": false}} }%%

sequenceDiagram
    actor client as WF Client
    participant worker as WF Worker
    participant dec as WF Decisions
    participant act as WF Activities
    participant bank as Bank Service 
    participant dag as DAG Builder <br>(GraphGenerator,<br> MermaidGenerator)

    %% ---------------------------------

    Note over worker,dag: Production  execution
    client ->> worker: Start WF (worker.exe)
    
    worker ->>+ act: Activity 
    act ->>- bank: Business Action 

    worker ->>+ dec: Decision Check
    dec -->>- worker: Decision
    
    worker ->>+ act: Activity 
    act ->>- bank: Business Action 

    %% ---------------------------------

    Note over worker,dag: Building DAG
    client ->> worker: Start WF (worker.exe -graph)

    worker ->>+ act: Activity 
    act ->>- dag: Add step to the graph 

    worker ->>+ dec: Decision Check
    dec ->> dag: Add decision to the graph 
    dec -->>- worker: Decision
    
    worker ->>+ act: Activity 
    act ->>- dag: Add step to the graph 

    worker ->> dag: Generate graph
    dag -->> worker:  Execution graphs, Mermaid diagram 
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