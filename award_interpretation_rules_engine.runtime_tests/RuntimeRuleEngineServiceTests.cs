using AwardInterpretationRulesEngine;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AwardInterpretationRulesEngine.Tests;

public sealed class RuntimeRuleEngineServiceTests
{
    [Fact]
    public async Task CalculateAsync_uses_di_snapshot_store_and_emits_traced_outputs()
    {
        var snapshot = Snapshot("MA000120-2026-v1", new DateOnly(2026, 1, 1), null, 29.52m, "2026.1");
        var provider = new ServiceCollection()
            .AddRuleEngineRuntime([snapshot])
            .BuildServiceProvider();

        var service = provider.GetRequiredService<IRuntimeRuleEngineService>();
        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            CorrelationId = "corr-1",
            AwardCode = "MA000120",
            RuleSetVersionId = snapshot.RuleSetVersionId,
            PayRun = StandardPayRun(new DateOnly(2026, 5, 25))
        });

        Assert.Equal(snapshot.RuleSetVersionId, result.RuleSetVersionId);
        Assert.Equal(snapshot.RuleSetVersionId, result.Calculation.RuleSetVersionId);
        Assert.All(result.Calculation.PayrollLines, line =>
        {
            Assert.Equal("2026.1", line.RuleVersion);
            Assert.False(string.IsNullOrWhiteSpace(line.Formula));
        });
        Assert.Equal(
            result.Calculation.PayrollLines.Count + result.Calculation.BlockedPayrollLines.Count,
            result.PayLineCalculatedEvents.Count);
        Assert.All(result.PayLineCalculatedEvents, evt => Assert.NotEmpty(evt.RuleTrace));
    }

    [Fact]
    public async Task CalculateAsync_uses_injected_runtime_services()
    {
        var snapshot = Snapshot("MA000120-2026-v1", new DateOnly(2026, 1, 1), null, 29.52m, "2026.1");
        var normaliser = new RecordingSegmentNormaliser();
        var calculator = new RecordingRuleCalculator();
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([snapshot]),
            new RuleEngineRuntimeOptions(),
            normaliser,
            calculator);

        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            AwardCode = "MA000120",
            RuleSetVersionId = snapshot.RuleSetVersionId,
            PayRun = StandardPayRun(new DateOnly(2026, 5, 25))
        });

        Assert.Same(snapshot.RulesJson, normaliser.LastLibrary);
        Assert.NotEmpty(normaliser.LastSegments);
        Assert.All(calculator.RuleSetVersionIds, id => Assert.Equal(snapshot.RuleSetVersionId, id));
        Assert.Equal(normaliser.LastSegments.Count, calculator.RuleSetVersionIds.Count);
        Assert.NotEmpty(result.Calculation.RuleTrace);
    }

    [Fact]
    public async Task RecalculateAsync_selects_explicit_historical_rule_version()
    {
        var oldSnapshot = Snapshot("MA000120-2024-v1", new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1), 20m, "2024.1");
        var currentSnapshot = Snapshot("MA000120-2026-v1", new DateOnly(2025, 1, 1), null, 30m, "2026.1");
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([oldSnapshot, currentSnapshot]),
            new RuleEngineRuntimeOptions());

        var result = await service.RecalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            AwardCode = "MA000120",
            RuleSetVersionId = oldSnapshot.RuleSetVersionId,
            RecalculationReason = "back-pay audit",
            PayRun = StandardPayRun(new DateOnly(2024, 7, 1))
        });

        Assert.True(result.IsHistoricalRecalculation);
        Assert.Equal(oldSnapshot.RuleSetVersionId, result.RuleSetVersionId);
        Assert.All(result.Calculation.PayrollLines, line => Assert.Equal("2024.1", line.RuleVersion));
        Assert.Equal(160m, result.Calculation.PayrollGross);
    }

    [Fact]
    public async Task CalculateAsync_requires_explicit_rule_set_version()
    {
        var snapshot = Snapshot("MA000120-2026-v1", new DateOnly(2026, 1, 1), null, 29.52m, "2026.1");
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([snapshot]),
            new RuleEngineRuntimeOptions { RequireExplicitRuleSetVersion = false });

        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            AwardCode = "MA000120",
            PayRun = StandardPayRun(new DateOnly(2026, 5, 25))
        });

        var exception = Assert.Single(result.ComplianceExceptions);
        Assert.Equal("RULE_VERSION_REQUIRED", exception.RuleId);
        Assert.True(exception.BlocksPayrollExport);
        Assert.Empty(result.Calculation.PayrollLines);
    }

    [Fact]
    public void TimesheetNormaliser_splits_cross_midnight_and_part_day_public_holiday_segments()
    {
        var normaliser = new TimesheetNormaliser(BuildLibrary(29.52m, "2026.1"));
        var crossMidnight = new PayRunInput
        {
            EmployeeReference = "EMP-1",
            PayPeriodReference = "P-1",
            Days =
            [
                new PayRunDay
                {
                    Date = new DateOnly(2026, 5, 29),
                    DayType = "weekday",
                    Shifts = [new PayRunShift { Start = "22:00", End = "02:00" }]
                }
            ]
        };

        var midnightSegments = normaliser.BuildSegmentRequests(crossMidnight);

        Assert.Equal(2, midnightSegments.Count);
        Assert.Equal("weekday", midnightSegments[0].Parameters["DayType"]);
        Assert.Equal("saturday", midnightSegments[1].Parameters["DayType"]);
        Assert.Equal(2m, midnightSegments[0].Parameters["PaidHours"]);
        Assert.Equal(2m, midnightSegments[1].Parameters["PaidHours"]);

        var partDayPublicHoliday = StandardPayRun(new DateOnly(2026, 12, 24));
        partDayPublicHoliday.Days[0].PublicHolidayId = "VIC-CHRISTMAS-EVE";
        partDayPublicHoliday.Days[0].PublicHolidayStart = "19:00";
        partDayPublicHoliday.Days[0].PublicHolidayEnd = "00:00";
        partDayPublicHoliday.Days[0].Shifts = [new PayRunShift { Start = "18:00", End = "22:00" }];

        var publicHolidaySegments = normaliser.BuildSegmentRequests(partDayPublicHoliday);

        Assert.Equal(2, publicHolidaySegments.Count);
        Assert.Equal(false, publicHolidaySegments[0].Parameters["IsPublicHolidayFromCalendar"]);
        Assert.Equal(true, publicHolidaySegments[1].Parameters["IsPublicHolidayFromCalendar"]);
        Assert.Equal("public_holiday", publicHolidaySegments[1].Parameters["ResolvedDayType"]);
        Assert.Equal(1m, publicHolidaySegments[0].Parameters["PaidHours"]);
        Assert.Equal(3m, publicHolidaySegments[1].Parameters["PaidHours"]);
    }

    [Fact]
    public async Task CalculateAsync_resolves_highest_of_stacking_policy()
    {
        var snapshot = Snapshot("STACKING-v1", new DateOnly(2026, 1, 1), null, 29.52m, "1.0", BuildStackingLibrary());
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([snapshot]),
            new RuleEngineRuntimeOptions());

        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            AwardCode = "MA000120",
            RuleSetVersionId = snapshot.RuleSetVersionId,
            PayRun = SingleSegmentPayRun(new DateOnly(2026, 5, 25))
        });

        Assert.Single(result.Calculation.PayrollLines);
        Assert.Equal("HIGH_DAY_PENALTY", result.Calculation.PayrollLines[0].RuleId);
        Assert.Equal(20m, result.Calculation.PayrollGross);
        var decision = Assert.Single(result.StackingDecisions);
        Assert.Equal("day_penalty", decision.StackingGroup);
        Assert.Equal("HIGH_DAY_PENALTY", decision.SelectedRuleId);
        Assert.Contains("LOW_DAY_PENALTY", decision.SuppressedRuleIds);
    }

    [Fact]
    public void GovernedAwardRuleEngine_applies_central_rounding_policy_to_pay_line_math()
    {
        var engine = new GovernedAwardRuleEngine(BuildRoundingPolicyLibrary(), new EngineOptions());
        var result = engine.Calculate(new PayRunRequest
        {
            EmployeeReference = "EMP-ROUND",
            PayPeriodReference = "P-ROUND",
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["ClassificationCode"] = "ROUND",
                ["PaidHours"] = 7.125m,
                ["VehicleKm"] = 3.5m,
                ["ShiftCount"] = 1m,
                ["AllPurposeAllowanceHourly"] = 0.335m
            }
        });

        AssertLine("HourlyAmount", 223.25m, 223.247625m);
        AssertLine("DailyAllowanceAmount", 10.01m, 10.005m);
        AssertLine("PerShiftAllowanceAmount", 2.34m, 2.335m);
        AssertLine("PerKmAllowanceAmount", 1.17m, 1.1655m);
        AssertLine("PercentageAllowanceAmount", 12.12m, 12.11544m);
        AssertLine("MultiplierAmount", 262.32m, 262.315959375m);
        AssertLine("AdditiveAmount", 225.63m, 225.6345m);
        Assert.Equal(736.84m, result.PayrollGross);
        Assert.Equal(result.PayrollGross, result.AwardReferenceGross);

        void AssertLine(string outputKey, decimal roundedAmount, decimal rawAmount)
        {
            var line = Assert.Single(result.PayrollLines, l => l.OutputKey == outputKey);
            Assert.Equal(roundedAmount, line.Amount);

            var trace = Assert.Single(result.RuleTrace, t => t.OutputKey == outputKey);
            Assert.Equal(rawAmount, trace.RawValue);
            Assert.Equal(roundedAmount, trace.RoundedValue);
            Assert.Equal(rawAmount, Assert.IsType<decimal>(trace.Value));
        }
    }

    [Fact]
    public async Task CalculateAsync_emits_blocked_warnings_toil_award_references_and_trace()
    {
        var snapshot = Snapshot("MA000120-2026-v1", new DateOnly(2026, 1, 1), null, 29.52m, "2026.1");
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([snapshot]),
            new RuleEngineRuntimeOptions());

        var payRun = StandardPayRun(new DateOnly(2026, 5, 25));
        payRun.Allowances.MealAllowanceRequired = true;
        payRun.Days[0].Shifts[0].End = "18:30";
        payRun.Days[0].Shifts[0].Tag = "toil";
        payRun.Days[0].Shifts[0].EvidenceReference = "TOIL-AGREEMENT-1";

        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            AwardCode = "MA000120",
            RuleSetVersionId = snapshot.RuleSetVersionId,
            PayRun = payRun
        });

        Assert.NotEmpty(result.Calculation.BlockedPayrollLines);
        Assert.NotEmpty(result.Calculation.AwardReferenceLines);
        Assert.NotEmpty(result.Calculation.Warnings);
        Assert.NotEmpty(result.Calculation.ToilMovements);
        Assert.NotEmpty(result.Calculation.RuleTrace);
        Assert.True(result.Calculation.BlockedPayrollGross > 0m);
        Assert.Contains(result.Calculation.Warnings, w => w.BlocksPayrollExport);
        Assert.Contains(result.Calculation.ToilMovements, m => m.RuleId == "TOIL_ACCRUAL_HOURS");
    }

    [Fact]
    public async Task CalculateAsync_does_not_inject_undeclared_segment_metadata_into_dynamic_expresso()
    {
        var library = BuildLibrary(29.52m, "2026.1");
        library.Orchestration = new Orchestration { EvaluationOrder = ["PAY_LINE_CALCULATION"] };
        library.Rules =
        [
            new RuleDefinition
            {
                RuleId = "UNDECLARED_METADATA_PROBE",
                Version = "2026.1",
                EffectiveFrom = "2026-01-01",
                EvaluationPhase = "PAY_LINE_CALCULATION",
                Precedence = 1,
                ClauseReference = "governance",
                Description = "Probe that must not access segment metadata.",
                Expression = "SourceShiftStartLocal == \"\" ? 0m : 10m",
                OutputKey = "MetadataProbeAmount",
                OutputType = "decimal",
                Action = "payroll_line"
            }
        ];
        library.PayCategoryMapping = [new PayCategoryMap { OutputKey = "MetadataProbeAmount", DefaultPayCategory = "Metadata probe" }];

        var snapshot = Snapshot("MA000120-2026-probe", new DateOnly(2026, 1, 1), null, 29.52m, "2026.1", library);
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([snapshot]),
            new RuleEngineRuntimeOptions());

        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            AwardCode = "MA000120",
            RuleSetVersionId = snapshot.RuleSetVersionId,
            PayRun = StandardPayRun(new DateOnly(2026, 5, 25))
        });

        Assert.Empty(result.Calculation.PayrollLines);
        Assert.Contains(result.Calculation.RuleTrace, t => t.RuleId == "UNDECLARED_METADATA_PROBE" && t.Status == "failed");
        Assert.Contains(result.Calculation.Warnings, w => w.RuleId == "UNDECLARED_METADATA_PROBE" && w.BlocksPayrollExport);
    }

    [Fact]
    public async Task CalculateAsync_rejects_unapproved_or_incomplete_snapshots_as_compliance_exception()
    {
        var draftSnapshot = Snapshot("MA000120-draft", new DateOnly(2026, 1, 1), null, 29.52m, "draft") with { Status = "draft" };
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([draftSnapshot]),
            new RuleEngineRuntimeOptions());

        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-a",
            AwardCode = "MA000120",
            RuleSetVersionId = draftSnapshot.RuleSetVersionId,
            PayRun = StandardPayRun(new DateOnly(2026, 5, 25))
        });

        var exception = Assert.Single(result.ComplianceExceptions);
        Assert.Equal("RULE_SNAPSHOT_REJECTED", exception.RuleId);
        Assert.True(exception.BlocksPayrollExport);
        Assert.Empty(result.Calculation.PayrollLines);
    }

    private static RuleSetVersion Snapshot(
        string id,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        decimal hourlyRate,
        string ruleVersion,
        GovernedExpressionLibrary? library = null)
    {
        library ??= BuildLibrary(hourlyRate, ruleVersion);

        return new RuleSetVersion
        {
            TenantId = "tenant-a",
            RuleSetVersionId = id,
            AwardCode = "MA000120",
            PublishedYear = effectiveFrom.Year,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            SourceSnapshotHash = $"sha256:{id}",
            ParserVersion = "parser-test",
            CompilerVersion = "compiler-test",
            Status = "approved",
            PublishedAt = new DateTimeOffset(effectiveFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            RulesJson = library
        };
    }

    private static GovernedExpressionLibrary BuildLibrary(decimal hourlyRate, string ruleVersion)
    {
        var interpretation = new AwardInterpretation
        {
            AwardCode = "MA000120",
            AwardName = "Children's Services Award 2010",
            EffectiveFrom = "2026-01-01",
            Classifications =
            [
                new ClassificationRate
                {
                    Code = "CSE_L3",
                    Name = "CSE Level 3 - Qualified Educator",
                    Weekly = hourlyRate * 38,
                    Hourly = hourlyRate,
                    ClauseReference = "14.1(b)"
                }
            ],
            Allowances = new AllowanceReference { StandardRateWeekly = hourlyRate * 38 }
        };

        var library = Ma000120InterpretationBuilder.BuildLibrary(interpretation);
        foreach (var rule in library.Rules)
            rule.Version = ruleVersion;

        return library;
    }

    private static GovernedExpressionLibrary BuildStackingLibrary() => new()
    {
        LibraryId = "STACKING_TEST_LIBRARY",
        LibraryName = "Stacking Test Library",
        AwardCode = "MA000120",
        AwardName = "Children's Services Award 2010",
        EffectiveFrom = "2026-01-01",
        Orchestration = new Orchestration { EvaluationOrder = ["PAY_LINE_CALCULATION"] },
        ReferenceData = new ReferenceData
        {
            Classifications =
            [
                new ClassificationRate
                {
                    Code = "CSE_L3",
                    Name = "CSE Level 3 - Qualified Educator",
                    Weekly = 1121.80m,
                    Hourly = 29.52m,
                    ClauseReference = "14.1(b)"
                }
            ],
            Allowances = new AllowanceReference { StandardRateWeekly = 1121.80m }
        },
        Parameters = [new ParameterDefinition { Name = "ClassificationCode", Required = true, Type = "string" }],
        Rules =
        [
            new RuleDefinition
            {
                RuleId = "LOW_DAY_PENALTY",
                Version = "1.0",
                EffectiveFrom = "2026-01-01",
                EvaluationPhase = "PAY_LINE_CALCULATION",
                Precedence = 1,
                ClauseReference = "23.5",
                Description = "Lower day penalty candidate.",
                Expression = "10",
                OutputKey = "LowDayPenaltyAmount",
                OutputType = "decimal",
                Action = "payroll_line",
                StackingGroup = "day_penalty",
                StackingPolicy = "highest_of"
            },
            new RuleDefinition
            {
                RuleId = "HIGH_DAY_PENALTY",
                Version = "1.0",
                EffectiveFrom = "2026-01-01",
                EvaluationPhase = "PAY_LINE_CALCULATION",
                Precedence = 2,
                ClauseReference = "27",
                Description = "Higher day penalty candidate.",
                Expression = "20",
                OutputKey = "HighDayPenaltyAmount",
                OutputType = "decimal",
                Action = "payroll_line",
                StackingGroup = "day_penalty",
                StackingPolicy = "highest_of"
            }
        ],
        PayCategoryMapping =
        [
            new PayCategoryMap { OutputKey = "LowDayPenaltyAmount", DefaultPayCategory = "Low penalty" },
            new PayCategoryMap { OutputKey = "HighDayPenaltyAmount", DefaultPayCategory = "High penalty" }
        ]
    };

    private static GovernedExpressionLibrary BuildRoundingPolicyLibrary() => new()
    {
        LibraryId = "ROUNDING_POLICY_TEST_LIBRARY",
        LibraryName = "Rounding Policy Test Library",
        AwardCode = "MA000120",
        AwardName = "Children's Services Award 2010",
        EffectiveFrom = "2026-01-01",
        Orchestration = new Orchestration { EvaluationOrder = ["PAY_LINE_CALCULATION"] },
        ReferenceData = new ReferenceData
        {
            Classifications =
            [
                new ClassificationRate
                {
                    Code = "ROUND",
                    Name = "Rounding policy test classification",
                    Weekly = 1190.654m,
                    Hourly = 31.333m,
                    ClauseReference = "rounding-policy-test"
                }
            ],
            Allowances = new AllowanceReference { StandardRateWeekly = 1121.80m }
        },
        Parameters = [new ParameterDefinition { Name = "ClassificationCode", Required = true, Type = "string" }],
        Rules =
        [
            RoundingRule("ROUND_HOURLY", "Hourly amount.", "PaidHours * BaseRate", "HourlyAmount"),
            RoundingRule("ROUND_DAILY", "Daily allowance amount.", "10.005m", "DailyAllowanceAmount"),
            RoundingRule("ROUND_PER_SHIFT", "Per-shift allowance amount.", "ShiftCount * 2.335m", "PerShiftAllowanceAmount"),
            RoundingRule("ROUND_PER_KM", "Per-km allowance amount.", "VehicleKm * 0.333m", "PerKmAllowanceAmount"),
            RoundingRule("ROUND_PERCENTAGE", "Percentage allowance amount.", "StandardRateWeekly * 0.0108m", "PercentageAllowanceAmount"),
            RoundingRule("ROUND_MULTIPLIER", "Multiplier amount.", "PaidHours * BaseRate * 1.175m", "MultiplierAmount"),
            RoundingRule("ROUND_ADDITIVE", "Additive amount.", "(BaseRate + AllPurposeAllowanceHourly) * PaidHours", "AdditiveAmount")
        ],
        PayCategoryMapping =
        [
            new PayCategoryMap { OutputKey = "HourlyAmount", DefaultPayCategory = "Hourly" },
            new PayCategoryMap { OutputKey = "DailyAllowanceAmount", DefaultPayCategory = "Daily" },
            new PayCategoryMap { OutputKey = "PerShiftAllowanceAmount", DefaultPayCategory = "Per shift" },
            new PayCategoryMap { OutputKey = "PerKmAllowanceAmount", DefaultPayCategory = "Per km" },
            new PayCategoryMap { OutputKey = "PercentageAllowanceAmount", DefaultPayCategory = "Percentage" },
            new PayCategoryMap { OutputKey = "MultiplierAmount", DefaultPayCategory = "Multiplier" },
            new PayCategoryMap { OutputKey = "AdditiveAmount", DefaultPayCategory = "Additive" }
        ]
    };

    private static RuleDefinition RoundingRule(string id, string description, string expression, string outputKey) => new()
    {
        RuleId = id,
        Version = "1.0",
        EffectiveFrom = "2026-01-01",
        EvaluationPhase = "PAY_LINE_CALCULATION",
        ClauseReference = "rounding-policy-test",
        Description = description,
        Expression = expression,
        OutputKey = outputKey,
        OutputType = "decimal",
        Action = "payroll_line"
    };

    private static PayRunInput StandardPayRun(DateOnly date) => new()
    {
        EmployeeReference = "EMP-0001",
        PayPeriodReference = "P-0001",
        Region = "VIC",
        Employee = new EmployeeInput
        {
            ClassificationCode = "CSE_L3",
            EmploymentCategory = "full_time",
            EmploymentProfileCode = "FULL_TIME",
            ContractedWeeklyHours = 38
        },
        Days =
        [
            new PayRunDay
            {
                Date = date,
                DayType = "weekday",
                Shifts =
                [
                    new PayRunShift
                    {
                        Start = "08:00",
                        End = "16:30",
                        Breaks =
                        [
                            new PayRunBreak
                            {
                                Start = "12:00",
                                End = "12:30",
                                Type = "meal",
                                Paid = false
                            }
                        ]
                    }
                ]
            }
        ]
    };

    private static PayRunInput SingleSegmentPayRun(DateOnly date)
    {
        var input = StandardPayRun(date);
        input.Days[0].Shifts[0].Start = "08:00";
        input.Days[0].Shifts[0].End = "16:00";
        input.Days[0].Shifts[0].Breaks.Clear();
        return input;
    }

    private sealed class RecordingSegmentNormaliser : ITimesheetSegmentNormaliser
    {
        private readonly TimesheetSegmentNormaliser _inner = new();

        public GovernedExpressionLibrary? LastLibrary { get; private set; }
        public IReadOnlyList<PayRunRequest> LastSegments { get; private set; } = [];

        public IReadOnlyList<PayRunRequest> BuildSegmentRequests(PayRunInput input, GovernedExpressionLibrary library)
        {
            LastLibrary = library;
            LastSegments = _inner.BuildSegmentRequests(input, library);
            return LastSegments;
        }
    }

    private sealed class RecordingRuleCalculator : IGovernedRuleCalculator
    {
        private readonly DynamicExpressoRuleCalculator _inner = new();

        public List<string> RuleSetVersionIds { get; } = [];

        public PayRunResult Calculate(GovernedExpressionLibrary library, EngineOptions options, string ruleSetVersionId, PayRunRequest request)
        {
            RuleSetVersionIds.Add(ruleSetVersionId);
            return _inner.Calculate(library, options, ruleSetVersionId, request);
        }
    }
}
