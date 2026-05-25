# MA000120 – Children's Services Award 2010 Rule Engine

## Overview
A .NET 8 console application that implements the **Fair Work Ombudsman Children's Services Award (MA000120)** as a rule engine using **DynamicExpresso** for expression evaluation.

**Pay Guide Effective: 1 March 2026** (Published 31 March 2026)
**Source:** [Fair Work Ombudsman – fairwork.gov.au](https://www.fairwork.gov.au)

---

## Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- NuGet access to restore `DynamicExpresso.Core` (v2.16.1)

---

## Getting Started

```bash
# Restore dependencies
dotnet restore

# Run the application
dotnet run

# Publish a self-contained executable
dotnet publish -c Release -r win-x64 --self-contained
```

---

## Project Structure

```
MA000120RuleEngine/
├── Program.cs                      # Entry point
├── MA000120RuleEngine.csproj       # Project file (DynamicExpresso dependency)
├── Models/
│   └── Models.cs                   # All domain models (Classification, PayRate, ShiftInput, etc.)
├── Data/
│   ├── PayRateData.cs              # Pay rate tables (all classifications & allowances)
│   └── AwardRules.cs               # All award rules as DynamicExpresso expression strings
└── Engine/
    ├── RuleEngine.cs               # Core engine – evaluates DynamicExpresso conditions
    ├── PayCalculator.cs            # Computes gross shift pay using rates + rules
    └── ConsoleUI.cs                # Interactive console menu + demo scenarios
```

---

## Award Coverage

### Classifications
| Stream | Levels |
|--------|--------|
| Children's Services Employee (CSE) | Level 1 (Introductory educator) → Level 8 (Director) |
| Support Worker (CSSE) | Level 1.1, 2.1, 2.2, 3.1 |
| Apprentice | Adult; Junior 16 yrs & under, 17 yrs, 18 yrs |
| Junior CSE | 16 yrs & under, 17 yrs, 18 yrs (Levels 1–2 only) |

### Rules Implemented (50+ rules across 9 categories)
| Category | Rules |
|----------|-------|
| Coverage | COV_001 – COV_003 |
| Employment Type | EMP_001 – EMP_007 |
| Ordinary Hours / Span | ORD_001 – ORD_005 |
| Penalty Rates | PEN_001 – PEN_005 |
| Overtime | OVT_001 – OVT_008 |
| Rest Breaks | BRK_001 – BRK_003 |
| Allowances | ALL_001 – ALL_015 |
| Apprentice | APP_001 – APP_003 |
| Leave | LVE_001 – LVE_006 |
| Cooks (ECEC) | COOK_001 |
| Superannuation | SUP_001 |
| Notice / Redundancy | NOT_001 – NOT_005 |

### Key Pay Rules
| Scenario | Rate |
|----------|------|
| Ordinary weekday | 100% |
| Saturday (non-shiftworker) | 100% (no penalty) |
| Saturday (shiftworker) | 150% |
| Sunday | 200% |
| Public holiday | 250% |
| Overtime Mon–Fri first 2h | 150% |
| Overtime Mon–Fri after 2h | 200% |
| Casual loading | +25% on base |
| Annual leave loading | +17.5% |

### Allowances (all included)
- Broken shift: $20.42/day
- Excess fares: $16.86/day
- First aid (non-OSHC): $12.12/day | First aid (OSHC): $1.57/hr
- Meal (overtime): $15.48
- Laundry & ironing: FT $9.49/wk | PT/CAS $1.90/day
- Educational leader: $913.46–$4,567.31/year (pro-rata by days/week)
- Vehicle: $0.99/km (car), $0.33/km (motorcycle)
- Apprentice training fees & travel: reimbursement

---

## How DynamicExpresso Is Used

Each award rule stores its eligibility logic as a plain C# boolean expression string, e.g.:

```csharp
// Rule PEN_003 – Sunday penalty
Condition = "DayType == \"Sunday\""
Outcome   = "Sunday rate applies: 200% of base hourly rate."
ValueExpr = "BaseHourlyRate * 2.00m"
```

At runtime the engine resolves variables from a `RuleContext` object and calls:

```csharp
var interpreter = new Interpreter();
bool triggered = (bool)interpreter.Eval(rule.Condition, parameters);
decimal value  = (decimal)interpreter.Eval(rule.ValueExpression, parameters);
```

This keeps the rule definitions data-driven and easy to extend without recompiling.

---

## Disclaimer
This tool is provided for **reference and educational purposes only**.  
Pay rates, allowances, and rules are effective from 1 March 2026 and are based on the Fair Work Ombudsman's published pay guide.  
Always verify entitlements against the current award at [fwc.gov.au](https://www.fwc.gov.au) or use the [Pay and Conditions Tool (PACT)](https://calculate.fairwork.gov.au).
