# Platform Architecture

The platform is a cloud-native, multi-tenant SaaS system for Australian FWC Modern Award interpretation, time and attendance normalization, rule execution, compliance reporting, and payroll integration.

See [architecture-diagrams.md](architecture-diagrams.md) for executable Mermaid diagrams covering the end-to-end platform, Clause Interpreter pipeline, CQRS calculation flow, and interpreter-to-rule-engine contract.

## Service Map

| Service | Responsibility |
|---|---|
| FWC ETL Service | Extract FWC MAPD API data, document-view clauses, raw snapshots, and staging records. |
| Clause Interpreter | Enrich staging rows with semantic rule conditions, confidence, provenance, and review state. |
| HITL Review Service | Let compliance officers review, edit, approve, or reject low-confidence parser output. |
| Award Catalog / RuleBuilder | Expose published award, classification, rate, allowance, penalty, and canonical rule-set reads. |
| Rule Engine | Execute deterministic calculations from versioned rule snapshots and normalized time segments. |
| Timesheet Normalization | Convert clock, schedule, break, leave, on-call, recall, and sleepover inputs into payable segments. |
| Tenant Configuration | Manage tenant awards, classifications, enabled allowances, and compliant overrides. |
| Compliance Service | Detect ambiguity, underpayment risk, invalid overrides, and calculation exceptions. |
| BOOT Comparison Service | Compare EBA/salary outcomes against base award outcomes and produce top-ups. |
| Payroll Output | Produce payroll-ready pay lines, exports, and STP Phase 2 mappings. |
| API Gateway | Route public APIs, enforce auth, rate limits, and tenant context. |

## Azure Reference Architecture

Recommended Azure services:

- Azure Container Apps or AKS for microservices.
- Azure SQL Database for operational award, tenant, rule, and audit data.
- Azure Service Bus or Event Hubs for domain events and high-volume timesheet events.
- Azure Blob Storage or Data Lake for raw FWC snapshots, source HTML/PDF, and golden corpora.
- Azure Key Vault for secrets and certificates.
- Azure App Configuration for feature flags and parser thresholds.
- Azure Monitor, Application Insights, and Log Analytics for telemetry.
- Azure API Management or existing gateway pattern for public API governance.
- Private endpoints for SQL, Storage, and Key Vault.

## Event Flow

```text
FWC MAPD API
  -> FWC ETL Service
  -> Raw Snapshot Tables and Blob Storage
  -> Staging Tables
  -> Clause Interpreter
  -> Parsed Semantic Conditions
  -> Manual Review if required
  -> Published Award Tables
  -> RuleSetVersion Snapshot
  -> Rule Engine
  -> Pay Lines and Compliance Exceptions
  -> Payroll Output and Reporting
```

Core events:

- `FwcIngestionStarted`
- `FwcIngestionCompleted`
- `ClauseParsed`
- `ClauseParseReviewRequired`
- `RuleSetCompiled`
- `RuleSetPublished`
- `TimesheetSubmitted`
- `TimesheetNormalized`
- `RuleCalculationRequested`
- `PayLineCalculated`
- `ComplianceExceptionRaised`
- `PayrollExportRequested`
- `PayrollExportCompleted`

## CQRS Guidance

- Commands mutate state and emit events.
- Queries read optimized projections.
- Rule snapshots are immutable read models for calculation.
- Timesheet state changes should be evented and replayable.
- Calculation traces should be append-only audit records.

## Tenant Isolation

Tenant isolation must be enforced at multiple layers:

- authenticated tenant context at gateway and service boundaries,
- tenant ID on all tenant-owned records,
- query filters in repositories,
- no cross-tenant cache keys,
- tenant-scoped encryption and secrets where appropriate,
- audit logs containing tenant ID, actor ID, correlation ID, and source system.

## Observability

Minimum platform metrics:

- FWC ingestion success/failure counts,
- parser complete/defaulted/manual review rates,
- low-confidence row counts by award,
- rule compilation duration and failures,
- calculation request volume and latency,
- compliance exception counts by type,
- payroll export success/failure counts,
- tenant override approval/rejection counts.

## Deployment Boundaries

The first delivery increment should keep the Clause Interpreter inside the Python ETL process to reduce distributed-system complexity. Extract it into a separate worker service only after:

- parser schema is stable,
- review loop exists,
- golden corpus is established,
- rule compiler consumes `condition_json`,
- operational metrics are available.

## Noisy Versus Strict Boundary

The ETL and interpreter side handles noisy inputs, confidence, manual review, and NLP uncertainty. The Rule Engine side handles strict deterministic calculation. No low-confidence or unreviewed parser output should cross the boundary into payable rule execution.
