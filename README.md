Running worker...
```
Start > Withdraw > decision1{NeedToConvert}:no > decision0{IsTFN_Known}:yes > NotifyAto > Deposit > End
Start > Withdraw > decision1{NeedToConvert}:no > decision0{IsTFN_Known}:no > TakeNonResidentTax > Deposit > End
Start > Withdraw > decision1{NeedToConvert}:yes > CurrencyConvert > decision0{IsTFN_Known}:yes > NotifyAto > Deposit > End
Start > Withdraw > decision1{NeedToConvert}:yes > CurrencyConvert > decision0{IsTFN_Known}:no > TakeNonResidentTax > Deposit > End
```

```mermaid
flowchart LR
s((Start)) --> Withdraw --> decision2{NeedToConvert} -- yes --> CurrencyConvert --> decision1{IsTFN_Known} -- yes --> NotifyAto --> Deposit --> e((End))
decision1{IsTFN_Known} -- no --> TakeNonResidentTax --> Deposit
decision2{NeedToConvert} -- no --> decision1{IsTFN_Known}
```



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

```mermaid
%%{init: {"flowchart": {"htmlLabels": false}} }%%
flowchart LR
  A["`**parameterChoiceUI** Indicator Selection`"]
  A --> C(Modules)
  B[sliderUI] --> C(Modules)
  C --> D{Plots}
```