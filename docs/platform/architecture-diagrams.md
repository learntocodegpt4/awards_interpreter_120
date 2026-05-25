# Architecture Diagrams

This page captures the target platform flows that drive the first interpreter implementation. The diagrams are intentionally executable Markdown so architecture, implementation tasks, automated tests, and Manual QA can stay in one repository.

## End-To-End Platform View

```mermaid
flowchart LR
  FWC["FWC MAPD API"] --> Ingest["FWC ETL ingestion service<br/>Python scheduled worker"]
  Ingest --> Raw["Raw landing tables<br/>tenant neutral source snapshots"]
  Raw --> Stage["Staging tables<br/>allowances, penalties, classifications"]
  Stage --> Interpreter["Clause Interpreter<br/>normalizer, deterministic parser, confidence scorer"]
  Interpreter --> Parsed["Parsed award semantics<br/>flat parse fields plus future condition_json"]
  Interpreter --> Review["Compliance review queue<br/>low confidence and ambiguous clauses"]
  Review --> Parsed
  Parsed --> RuleBuilder["Rule publication service<br/>RuleSetVersion compiler"]
  RuleBuilder --> RuleStore["Immutable rule snapshots<br/>SCD Type 2 effective dating"]

  TenantApi["Tenant APIs<br/>award selection, employee context"] --> RuleEngine["Rule Engine<br/>stateless calculation brain"]
  Clock["Time and attendance events"] --> Timesheet["Timesheet command service<br/>CQRS write model"]
  Timesheet --> Events["Event Hub<br/>timesheet events"]
  Events --> Normalizer["Timesheet normalizer<br/>segments, breaks, public holidays"]
  Normalizer --> RuleEngine
  RuleStore --> RuleEngine
  RuleEngine --> PayEvents["Pay calculation events<br/>pay lines and compliance trace"]
  PayEvents --> ReadModel["Payroll read model<br/>employee, period, tenant aggregates"]
  ReadModel --> STP["STP Phase 2 exporter"]
  ReadModel --> Payroll["Payroll integrations<br/>Xero, MYOB, API consumers"]
```

## Clause Interpreter Pipeline

```mermaid
flowchart TD
  Row["FWC staging row<br/>allowance, clauses, penalty text, frequency"] --> Bundle["Source bundle<br/>normalized source fields and source hash"]
  Bundle --> Normalize["Text normalizer<br/>case, dash, whitespace, punctuation"]
  Normalize --> Entities["Entity extraction<br/>days, basis, time windows, rates, thresholds"]
  Entities --> Deterministic["Deterministic pattern library<br/>first aid, on-call, sleepover, meal, travel, laundry"]
  Deterministic --> Defaults["Domain defaults<br/>only when no restrictive evidence exists"]
  Defaults --> Confidence["Confidence scorer<br/>explicit evidence, defaults, conflicts, gaps"]
  Confidence --> Decision{"Parse status"}
  Decision -->|"complete or complete_defaulted"| Publish["Publish structured fields<br/>Rule Engine consumable semantics"]
  Decision -->|"partial or manual_review"| Queue["Manual QA queue<br/>evidence spans and review notes"]
  Queue --> Publish
  Publish --> Trace["Traceability record<br/>parser version, evidence, source key hash"]
```

## Rule Execution And CQRS Flow

```mermaid
sequenceDiagram
  participant UI as WFM UI or API client
  participant CMD as Timesheet Command Service
  participant BUS as Event Hub
  participant NORM as Timesheet Normalizer
  participant RULE as Rule Engine
  participant STORE as Rule Snapshot Store
  participant READ as Payroll Read Service
  participant QA as Compliance Review

  UI->>CMD: Submit raw clock events, breaks, tags, employee context
  CMD->>BUS: TimesheetSubmitted event
  BUS->>NORM: Consume event
  NORM->>NORM: Split overnight, breaks, part-day public holidays
  NORM->>RULE: Calculate normalized payable segments
  RULE->>STORE: Load tenant award RuleSetVersion for work date
  STORE-->>RULE: Immutable rules and clause provenance
  RULE->>RULE: Evaluate base, penalties, overtime, allowances, stacking
  RULE->>BUS: TimesheetCalculated event with pay line trace
  BUS->>READ: Project pay lines into payroll read model
  RULE-->>QA: ComplianceException event when ambiguity or underpayment risk exists
```

## Interpreter To Rule Engine Contract

```mermaid
flowchart LR
  Text["Unstructured FWC text"] --> Fields["Flat semantic fields<br/>applies_day_type, payment_trigger, payment_basis"]
  Text --> Condition["Future condition_json<br/>structured predicates with provenance"]
  Fields --> Compiler["Rule compiler"]
  Condition --> Compiler
  Compiler --> Snapshot["RuleSetVersion"]
  Snapshot --> Math["Mathematical rule execution<br/>no NLP in calculation path"]
```

Key boundary: NLP and interpretation stay in the ETL and compliance review path. The Rule Engine only consumes versioned, reviewed, structured rule conditions.
