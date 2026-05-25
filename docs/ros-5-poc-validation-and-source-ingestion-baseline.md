# ROS-5 POC validation and source ingestion baseline

## Purpose

This document captures the baseline requested by ROS-5 for productionizing the
`award_interpretation_rules_engine` proof of concept (POC). It gives future
agents a shared view of:

- What the POC is understood to deliver.
- What remains as a production gap.
- The source ingestion contract for Fair Work data.
- The guardrail that public award HTML can be read and parsed for provenance,
  review, and fallback purposes, but must not become an ungoverned payroll
  execution source.

## Repository validation note

At the time this baseline was written, this repository checkout contains only
`README.md` and `LICENSE`; no executable POC implementation is present to run or
inspect. The capability confirmation below is therefore based on the ROS-5
Linear issue and project context, and each row records that source explicitly.
If the POC source is later added to this repository, update the matrix with
code-level evidence and tests.

## Capability matrix

| Area | POC capability understood from ROS-5 context | Current validation status | Production implication |
| --- | --- | --- | --- |
| Fair Work API configuration | POC supports Fair Work API configuration rather than hard-coded endpoints. | Confirmed from issue/project context; not code-verified in this checkout. | Keep API base URLs, endpoint paths/templates, authentication, subscription details, timeouts, and feature flags in configuration. Do not bake developer-portal-only endpoint details into code. |
| Public award HTML reader/parser | POC can read and parse public award HTML. | Confirmed from issue/project context; not code-verified in this checkout. | Preserve as a fallback and reader path that emits provenance-rich candidate source records only. It cannot directly drive payroll execution. |
| MA000120 interpretation builder | POC includes an MA000120 interpretation builder. | Confirmed from project baseline; not code-verified in this checkout. | Use MA000120 as the acceptance baseline for production ingestion, review, compilation, and execution. |
| Governed expression library | POC includes a governed expression library. | Confirmed from project baseline; not code-verified in this checkout. | Production compiler should permit only approved expression functions/operators and reject arbitrary runtime expressions. |
| DynamicExpresso runtime | POC uses DynamicExpresso for rule evaluation. | Confirmed from project baseline; not code-verified in this checkout. | Backend execution should evaluate only approved immutable snapshots compiled from reviewed source facts. |
| Timesheet normalization | POC includes a timesheet normalizer. | Confirmed from project baseline; not code-verified in this checkout. | Production inputs must be normalized before rule execution so calculations are deterministic and traceable. |
| Payroll calculation | POC includes a payroll calculator. | Confirmed from project baseline; not code-verified in this checkout. | Production calculations need deterministic payroll lines, warnings, blocked lines, TOIL movements, award references, and rule traces. |
| CLI output | POC emits CLI output. | Confirmed from project baseline; not code-verified in this checkout. | CLI output is useful for demos and diagnostics, but production services need API/storage contracts and audit trails. |

## Production gap matrix

| Gap | Why it matters | Required production baseline |
| --- | --- | --- |
| No code-level POC evidence in this checkout | Agents cannot run or inspect the implementation from the current repository state. | Add POC source or attach implementation references, then update this document with file/test evidence. |
| ETL/runtime boundary not encoded | The architecture requires noisy Fair Work parsing and review to remain outside payroll execution. | Keep ingestion, provenance, review, and compiler governance in `RosteredAI_ETL`; execute only approved snapshots from the backend rule-engine boundary in `RosteredAI_Back` or `RosteredAI_ETL/RuleEngine`. |
| Source provenance is not formalized | Award text can change and parsers can misread clauses. | Store source type, configured endpoint key, source URI where public, retrieval time, effective dates, parser version, checksum, raw payload reference, normalized rows, warnings, and review state. |
| Review/gate workflow is not formalized | Candidate parsed rules must not silently become payroll rules. | Require human or governed automated approval before compiling source facts into a rule snapshot. |
| Immutable rule snapshots are not formalized | Payroll runs must be reproducible after source or parser changes. | Publish versioned, immutable snapshots with content hashes, compiler version, approved source record IDs, award code, and effective dates. |
| Public HTML fallback could be overused | Public HTML is useful but can be structurally unstable. | Treat HTML output as candidate source records only. It may support review, comparison, and fallback reading; it must not be an unreviewed execution source. |
| API endpoint details are unavailable to public agents | Fair Work API is behind developer portal login/subscription. | Use configuration keys and logical endpoint names in code/docs. Keep actual URLs and subscription keys in environment-specific configuration/secrets. |
| Acceptance outputs are not contract-tested | The MA000120 baseline requires deterministic behavior. | Add tests for source rows to `condition_json`, review/gate states, snapshot compilation, payroll lines, warnings, blocked lines, TOIL movements, award references, and rule trace. |

## Source ingestion principles

1. **Configuration owns Fair Work API endpoints.** Code should reference logical
   endpoint keys such as `fair_work.awards` or `fair_work.award_details`, not
   hard-coded portal URLs.
2. **Secrets stay out of source.** Subscription keys and tokens must be read
   from secret references or environment-specific configuration.
3. **Every ingested source produces a candidate record.** API and HTML reads
   both produce source records with provenance, checksums, parser metadata, and
   warnings.
4. **Public HTML is a reader/fallback path.** Parsed HTML can help review,
   compare, recover, or bootstrap source facts. It cannot directly execute
   payroll rules.
5. **Review gates compilation.** Only reviewed and approved candidate source
   records can be compiled into immutable rule snapshots.
6. **Execution consumes snapshots only.** Payroll calculation must use approved
   snapshots, not live API responses, raw HTML, or ad hoc parsed source rows.
7. **Traceability is mandatory.** Payroll lines must be traceable back to the
   rule snapshot, approved source records, award clauses, and source payload
   hashes.

## Configuration contract

Use environment-specific configuration with logical endpoint names. The exact
Fair Work API paths are intentionally not specified here because access is
behind the Fair Work developer portal and subscription.

```yaml
fair_work:
  api:
    enabled: true
    base_url: ${FAIR_WORK_API_BASE_URL}
    subscription_key_secret_ref: ${FAIR_WORK_API_SUBSCRIPTION_KEY_SECRET_REF}
    timeout_seconds: ${FAIR_WORK_API_TIMEOUT_SECONDS}
    endpoints:
      awards:
        method: GET
        path_template: ${FAIR_WORK_API_AWARDS_PATH_TEMPLATE}
      award_details:
        method: GET
        path_template: ${FAIR_WORK_API_AWARD_DETAILS_PATH_TEMPLATE}
      classifications:
        method: GET
        path_template: ${FAIR_WORK_API_CLASSIFICATIONS_PATH_TEMPLATE}
  public_html:
    enabled: true
    fallback_only: true
    base_url: ${FAIR_WORK_PUBLIC_AWARD_BASE_URL}
```

Minimum rules:

- The endpoint registry must be injectable per environment.
- `fallback_only: true` is the default and expected setting for public HTML.
- A production service must fail closed if API configuration is missing for an
  API ingestion job.
- A production service must block payroll execution if the only available input
  is an unreviewed HTML parse.

## Source record contract

The machine-readable schema for candidate source records is maintained in
[`../schemas/fair-work-source-ingestion.schema.json`](../schemas/fair-work-source-ingestion.schema.json).

Required source record fields:

- `sourceRecordId`: Stable identifier for the candidate source record.
- `awardCode`: Award code, for example `MA000120`.
- `sourceType`: One of `fair_work_api`, `public_award_html`, or
  `manual_review_adjustment`.
- `retrievedAt`: UTC timestamp for the source retrieval or manual adjustment.
- `parserVersion`: Version of the ingestion/parser component.
- `contentSha256`: SHA-256 hash of the raw source payload or canonicalized
  manual adjustment body.
- `rawPayloadRef`: Storage reference for the raw payload or adjustment record.
- `reviewStatus`: Current gate state.
- `normalizedRows`: Candidate interpreted rows, including award references and
  `conditionJson`.

Review states:

- `candidate`: Created by ingestion and not yet reviewed.
- `needs_review`: Parser warnings or changes require review.
- `approved`: May be compiled into a rule snapshot.
- `rejected`: Must not be compiled or executed.
- `superseded`: Replaced by a newer approved source record.

## Rule snapshot contract

Compiled rule snapshots must include:

- Snapshot ID and semantic or monotonic version.
- Award code and effective date range.
- Source record IDs and source checksums used to compile the snapshot.
- Compiler version and governed expression library version.
- Generated rules and expression identifiers.
- Approval metadata.
- Snapshot content hash.

Payroll execution must record the snapshot ID and hash with each calculation
run. If the snapshot hash is unavailable or does not match the approved
snapshot, execution should be blocked.

## MA000120 acceptance baseline

The first production-ready path should prove MA000120 end to end:

1. Ingest Fair Work source through configured API endpoints when available.
2. Parse public award HTML only as a fallback/reader path.
3. Produce candidate source records with `conditionJson`, provenance, warnings,
   and award references.
4. Review and approve candidate records.
5. Compile approved records into immutable governed rule snapshots.
6. Run deterministic payroll calculations from approved snapshots.
7. Emit payroll lines, warnings, blocked lines, TOIL movements, award
   references, and full rule trace.

## Agent checklist

Before building production services, verify:

- [ ] The POC source is present or linked and this matrix has code-level
      evidence.
- [ ] Fair Work API access is represented only by configuration and secrets.
- [ ] Public HTML parsing is flagged as fallback/reader-only.
- [ ] Source records conform to the JSON Schema in this repository.
- [ ] Review gates are enforced before snapshot compilation.
- [ ] Backend payroll execution accepts approved immutable snapshots only.
- [ ] MA000120 has deterministic acceptance fixtures and trace assertions.
