# AGENTS.md

## Cursor Cloud specific instructions

This repository is an **Australian Fair Work Commission Modern Award Interpretation** platform, specifically implementing the Children's Services Award 2010 (MA000120). It contains two .NET 8 console applications.

### Current state

- **Language/Framework**: C# / .NET 8
- **Package manager**: NuGet (via `dotnet restore`)
- **Services**: Two console apps (no long-running servers)
- **Tests**: No automated test suite yet
- **Lint**: No linter configured

### Projects

| Project | Path | Description |
|---------|------|-------------|
| MA000120RuleEngine | `MA000120RuleEngine/` | Interactive console app with DynamicExpresso-powered pay calculations, demo scenarios, and rule evaluation |
| AwardInterpretationRulesEngine | `award_interpretation_rules_engine/` | CLI pipeline: fetches award data, parses clauses, builds governed rule library, calculates pay from timesheet input |

### Build & Run

```bash
# Restore and build both projects
dotnet restore MA000120RuleEngine/MA000120RuleEngine.csproj
dotnet restore award_interpretation_rules_engine/AwardInterpretationRulesEngine.csproj
dotnet build MA000120RuleEngine/MA000120RuleEngine.csproj
dotnet build award_interpretation_rules_engine/AwardInterpretationRulesEngine.csproj

# Run the interactive rule engine (requires a TTY for Console.ReadKey)
dotnet run --project MA000120RuleEngine/

# Run the CLI pipeline with sample data
cp award_interpretation_rules_engine/appsettings.example.json award_interpretation_rules_engine/appsettings.json
dotnet run --project award_interpretation_rules_engine/ -- --award MA000120 --input award_interpretation_rules_engine/samples/sample-payrun-ma000120.json --output output
```

### Non-obvious gotchas

- **MA000120RuleEngine is interactive**: It uses `Console.ReadKey()` which throws when stdin is piped. To test non-interactively, pipe menu choices (e.g. `echo -e "3\n1\n0" | dotnet run`) and accept the `InvalidOperationException` at the "Press any key" pause.
- **award_interpretation_rules_engine requires appsettings.json**: Copy `appsettings.example.json` to `appsettings.json` before running. The FWC API is disabled by default (falls back to public HTML parsing).
- **Git submodules** (`RosteredAI_Back`, `RosteredAI_ETL`, `DynamicExpresso`) are referenced but not fetchable without private credentials. The core projects build and run without them.
- **No automated tests exist yet** — validation is done via the demo scenarios and CLI pipeline output.
