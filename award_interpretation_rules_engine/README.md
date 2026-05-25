# Award Interpretation + Governed Rules Engine

This project is an end-to-end .NET 8 prototype that:

1. Reads award data from a configurable Fair Work Commission Modern Awards Pay Database API client.
2. Falls back to the public online award page, such as `https://awards.fairwork.gov.au/MA000120.html`.
3. Parses the online award into structured clauses and source metadata.
4. Creates a governed interpretation output.
5. Builds a Dynamic Expresso expression library.
6. Calculates pay from supplied timesheet/employee parameters.
7. Emits:
   - `*.interpretation.json`
   - `*.governed-library.json`
   - `*.calculation.json`

## Important

The Fair Work Commission Modern Awards API is subscription/login based. This project therefore uses a **configuration-driven API client**. Replace the endpoint templates in `appsettings.example.json` with the exact endpoints from your FWC developer portal subscription.

The public award page parser is a fallback and governance aid. It is not a substitute for legal/payroll validation.

## Install

```bash
dotnet restore
```

## Run with the sample MA000120 input

```bash
dotnet run -- --award MA000120 --input samples/sample-payrun-ma000120.json --config appsettings.example.json --out output
```

## Run the MA000120 acceptance baseline

```bash
dotnet run -- --acceptance
```

The acceptance baseline uses a deterministic local MA000120 source fixture and
asserts parse -> `condition_json` -> review gate -> governed snapshot ->
calculation coverage for sample parity, ordinary time, overtime, allowances,
public holidays, TOIL, rest/fatigue, leave loading, blocked exports, award
references, and rule traces.

## Configuration

Copy `appsettings.example.json` and set:

```json
{
  "fairWorkApi": {
    "enabled": true,
    "baseUrl": "https://api.fwc.gov.au",
    "subscriptionKey": "YOUR_KEY",
    "subscriptionHeaderName": "Ocp-Apim-Subscription-Key",
    "awardByCodePath": "/REPLACE_WITH_PORTAL_ENDPOINT/{awardCode}",
    "ratesByAwardPath": "/REPLACE_WITH_PORTAL_ENDPOINT/{awardCode}/rates"
  }
}
```

If the API is disabled or unavailable, the pipeline still fetches the public award HTML page and creates the governed interpretation using deterministic MA000120 templates plus parsed clause text.

## Why Dynamic Expresso?

Dynamic Expresso evaluates a subset of C# expressions with injected variables/parameters. The rule library stores atomic expressions, and the engine evaluates them by phase and precedence.

## Production hardening checklist

- Use official API endpoint definitions from the FWC developer portal.
- Persist award snapshots and parsed documents with content hashes.
- Require workflow approvals for generated rules.
- Add payroll/legal sign-off.
- Add unit tests for every clause and test fixture.
- Add state/territory public holiday data from a governed source.
- Add enterprise agreement / above-award overlays.
- Validate against real interpreted payroll/Tanda output before go-live.
