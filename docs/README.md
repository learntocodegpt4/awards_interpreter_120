# Award Interpretation Platform Documentation

This documentation set breaks the Modern Award interpretation platform into testable workstreams. It is intended to guide implementation, automated testing, Manual QA, and status tracking as the Python ETL, Clause Interpreter, Rule Engine, and payroll compliance services mature.

## Workstream Map

| Area | Purpose | Primary Docs |
|---|---|---|
| Clause Interpreter | Convert unstructured FWC allowance and penalty text into structured rule conditions. | `docs/interpreter/README.md`, `docs/interpreter/todos.md`, `docs/interpreter/test-plan.md`, `docs/interpreter/manual-qa.md` |
| Rule Engine | Compile published award semantics and calculate pay from normalized time segments. | `docs/ruleengine/README.md`, `docs/ruleengine/todos.md`, `docs/ruleengine/test-plan.md`, `docs/ruleengine/manual-qa.md` |
| Data Model | Define canonical enums, semantic condition JSON, confidence, status, and provenance. | `docs/data-model/semantic-schema.md` |
| Platform Architecture | Describe Azure services, microservices, events, tenant isolation, and observability. | `docs/platform/architecture.md` |
| Architecture Diagrams | Provide executable Mermaid diagrams for platform, interpreter, CQRS, and rule contracts. | `docs/platform/architecture-diagrams.md` |
| Timesheet Normalization | Define canonical time segments, public holiday splitting, breaks, leave overlays, and fatigue state. | `docs/timesheets/normalization.md` |
| Compliance Edge Cases | Track legal interpretation traps that must be explicitly tested. | `docs/compliance/edge-case-matrix.md` |
| Payroll Integrations | Define payroll output, STP Phase 2 mapping, and integration boundaries. | `docs/integrations/stp-phase2.md` |
| QA Traceability | Map requirements to implementation tasks, automated tests, and Manual QA checks. | `docs/qa/traceability-matrix.md` |

## Status Legend

Use this exact status vocabulary in task tables:

| Status | Meaning |
|---|---|
| TODO | Task is defined and not yet started. |
| IN_PROGRESS | Implementation or validation is actively underway. |
| DONE | Deliverable is complete, tested, reviewed, and linked in the traceability matrix. |
| BLOCKED | Task cannot proceed until a named dependency is resolved. |
| DEFERRED | Task is intentionally moved out of the current delivery increment. |

## Documentation Inventory

| Document | Status | Notes |
|---|---|---|
| `docs/README.md` | DONE | Root index and operating model. |
| `docs/interpreter/README.md` | DONE | Interpreter design and governance. |
| `docs/interpreter/todos.md` | DONE | Interpreter implementation checklist. |
| `docs/interpreter/test-plan.md` | DONE | Automated parser validation plan. |
| `docs/interpreter/manual-qa.md` | DONE | Manual parser QA scenarios. |
| `docs/ruleengine/README.md` | DONE | Rule Engine design and flow. |
| `docs/ruleengine/todos.md` | DONE | Rule Engine implementation checklist. |
| `docs/ruleengine/test-plan.md` | DONE | Automated calculation validation plan. |
| `docs/ruleengine/manual-qa.md` | DONE | Manual calculation QA scenarios. |
| `docs/data-model/semantic-schema.md` | DONE | Semantic schema and enums. |
| `docs/platform/architecture.md` | DONE | Azure architecture and event flow. |
| `docs/platform/architecture-diagrams.md` | DONE | Mermaid platform, interpreter, CQRS, and rule contract diagrams. |
| `docs/timesheets/normalization.md` | DONE | Canonical T&A normalization model. |
| `docs/compliance/edge-case-matrix.md` | DONE | Unknown unknowns and WFM edge case matrix. |
| `docs/integrations/stp-phase2.md` | DONE | Payroll and STP Phase 2 roadmap. |
| `docs/qa/traceability-matrix.md` | DONE | Requirement-to-test traceability. |
| `docs/tests/ros-41-ma000120-poc-parity.md` | DONE | MA000120 POC CLI parity report and fixture governance. |

## Operating Rules

1. Update `todos.md` before starting implementation for a task.
2. Mark a task `IN_PROGRESS` when a code change starts.
3. Mark a task `DONE` only when:
   - the deliverable exists,
   - automated tests pass or the test gap is explicitly recorded,
   - Manual QA has either passed or been marked not applicable,
   - `docs/qa/traceability-matrix.md` links the requirement to evidence.
4. Do not treat a parser output as payroll-authoritative unless it has evidence, confidence, provenance, and review status.
5. Do not let the Rule Engine parse FWC legal text at runtime. It must consume structured, versioned rule snapshots only.

## Current System Anchors

- Python ETL source: `RosteredAI_ETL/src`
- Current semantic parser seed: `RosteredAI_ETL/src/transform/award_semantics.py`
- Current staging and publish columns: `RosteredAI_ETL/migrations/sql/021_update_incremental_load_sp.sql`
- Existing backend microservices: `RosteredAI_Back`
- RuleBuilder service: `RosteredAI_ETL/RuleBuilder`
- RuleEngine service: `RosteredAI_ETL/RuleEngine`

## Implementation Sequence

1. Stabilize the semantic data contract and parser vocabulary.
2. Build deterministic interpreter modules and confidence gates.
3. Add human-in-the-loop review for low-confidence or ambiguous legal text.
4. Compile reviewed semantic output into immutable rule snapshots.
5. Normalize timesheets into payable segments.
6. Execute calculations with traceable rule versions and decimal math.
7. Add edge-case calculators for fatigue breaks, leave loading, public holidays, BOOT, and compounding.
8. Publish payroll-ready outputs and STP Phase 2 mappings.
