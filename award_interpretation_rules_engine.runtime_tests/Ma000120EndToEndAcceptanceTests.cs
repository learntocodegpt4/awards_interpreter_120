using System.Text;
using System.Text.Json;
using AwardInterpretationRulesEngine;
using Xunit;

namespace AwardInterpretationRulesEngine.Tests;

public sealed class Ma000120EndToEndAcceptanceTests
{
    private const decimal MoneyTolerance = 0.005m;
    private static readonly DateTimeOffset AcceptancePublishedAtUtc = DateTimeOffset.Parse("2026-03-01T00:00:00Z");

    [Fact]
    public async Task Ma000120_acceptance_matrix_compiles_and_calculates_traced_outputs()
    {
        var root = WorkspaceRoot();
        var fixture = LoadFixture(root);
        var snapshot = await CompileSnapshotFromParsedSourceAsync(root, fixture);
        var service = BuildRuntime(snapshot);

        Assert.Equal("MA000120", snapshot.AwardCode);
        Assert.Equal("published", snapshot.Status);
        Assert.Equal(GovernedRuleCompiler.CurrentCompilerVersion, snapshot.CompilerVersion);
        Assert.Equal(64, snapshot.SnapshotContentHash.Length);
        Assert.Contains(snapshot.SourceRowIds, id => id.Contains("ORDINARY_TIME", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(snapshot.SourceRowIds, id => id.Contains("BLOCKED_EXPORTS", StringComparison.OrdinalIgnoreCase));

        foreach (var scenario in fixture.Scenarios)
        {
            var result = await CalculateScenarioAsync(root, service, snapshot, scenario);
            AcceptanceAssert.Scenario(result, snapshot, scenario);
        }
    }

    [Fact]
    public async Task Acceptance_failure_diagnostics_include_rule_trace_and_snapshot_version_context()
    {
        var root = WorkspaceRoot();
        var fixture = LoadFixture(root);
        var snapshot = await CompileSnapshotFromParsedSourceAsync(root, fixture);
        var service = BuildRuntime(snapshot);
        var scenario = fixture.Scenarios.Single(s => s.Name == "ordinary-time-with-first-aid-and-laundry-allowances");
        var result = await CalculateScenarioAsync(root, service, snapshot, scenario);

        var exception = Assert.Throws<AcceptanceAssertionException>(() =>
            AcceptanceAssert.Money(999m, result.Calculation.PayrollGross, "payroll gross", scenario, snapshot, result));

        Assert.Contains("RuleSetVersionId:", exception.Message);
        Assert.Contains(snapshot.RuleSetVersionId, exception.Message);
        Assert.Contains("SnapshotContentHash:", exception.Message);
        Assert.Contains("ParserVersion:", exception.Message);
        Assert.Contains("CompilerVersion:", exception.Message);
        Assert.Contains("Rule trace:", exception.Message);
        Assert.Contains("PAY_ORDINARY_HOURS", exception.Message);
    }

    private static async Task<RuleCalculationResult> CalculateScenarioAsync(
        string root,
        IRuntimeRuleEngineService service,
        RuleSetVersion snapshot,
        AcceptanceScenario scenario)
    {
        var payRun = scenario.PayRun ?? PayRunInput.Load(Path.Combine(root, scenario.PayRunPath ?? ""));
        return await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-acceptance",
            CorrelationId = $"acceptance:{scenario.Name}",
            AwardCode = "MA000120",
            RuleSetVersionId = snapshot.RuleSetVersionId,
            PayRun = payRun
        });
    }

    private static RuntimeRuleEngineService BuildRuntime(RuleSetVersion snapshot)
        => new(
            new InMemoryRuleSnapshotStore([snapshot with { TenantId = "tenant-acceptance" }]),
            new RuleEngineRuntimeOptions
            {
                Engine = new EngineOptions
                {
                    IncludeFinalContext = true,
                    BlockPayrollExportOnErrors = true,
                    FixedCalculationTimestampUtc = AcceptancePublishedAtUtc
                }
            });

    private static async Task<RuleSetVersion> CompileSnapshotFromParsedSourceAsync(string root, AcceptanceMatrixFixture fixture)
    {
        var settings = new AppSettings
        {
            OnlineAwards = new OnlineAwardsOptions
            {
                AwardHtmlUrlTemplate = Path.Combine(root, fixture.SourceFixturePath),
                FixedRetrievedAtUtc = AcceptancePublishedAtUtc
            },
            Engine = new EngineOptions
            {
                IncludeFinalContext = true,
                BlockPayrollExportOnErrors = true,
                FixedCalculationTimestampUtc = AcceptancePublishedAtUtc
            }
        };

        var parseProbePayRun = PayRunInput.Load(Path.Combine(root, fixture.SamplePayRunPath));
        var pipeline = await new AwardPipeline(settings).RunAsync(fixture.AwardCode, parseProbePayRun, CancellationToken.None);
        Assert.True(pipeline.ReviewGate.Approved, string.Join(", ", pipeline.ReviewGate.Errors));

        var request = BuildCompilationRequest(pipeline);
        return new GovernedRuleCompiler().CompilePublishedSnapshot(request) with { TenantId = "tenant-acceptance" };
    }

    private static RuleCompilationRequest BuildCompilationRequest(PipelineResult pipeline)
    {
        var sourceRecord = pipeline.Interpretation.SourceRecords.Single();
        return new RuleCompilationRequest
        {
            AwardCode = pipeline.Interpretation.AwardCode,
            PublishedYear = 2026,
            EffectiveFrom = DateOnly.Parse(pipeline.Interpretation.EffectiveFrom),
            SourceSnapshotHash = sourceRecord.ContentSha256,
            ParserVersion = sourceRecord.ParserVersion,
            CompilerVersion = GovernedRuleCompiler.CurrentCompilerVersion,
            ApprovedBy = pipeline.ReviewGate.ApprovedBy,
            PublishedAtUtc = AcceptancePublishedAtUtc,
            SemanticRows = sourceRecord.NormalizedRows.Select(row => ToSemanticRow(sourceRecord, row)).ToList()
        };
    }

    private static SemanticRuleRow ToSemanticRow(SourceRecord sourceRecord, NormalisedSourceRow row) => new()
    {
        RowId = row.RowId,
        AwardReference = new SemanticAwardReference
        {
            Clause = row.ClauseReference,
            Title = row.RuleFamily,
            Url = sourceRecord.SourceUri
        },
        SourceText = row.SourceText,
        ReviewStatus = sourceRecord.ReviewStatus,
        ParseStatus = row.ParseStatus,
        EffectiveFrom = new DateOnly(2026, 3, 1),
        ContentSha256 = sourceRecord.ContentSha256,
        ConditionJson = ToSemanticCondition(row.ConditionJson)
    };

    private static SemanticCondition ToSemanticCondition(ConditionJson condition) => new()
    {
        SchemaVersion = condition.SchemaVersion,
        EntityType = condition.EntityType,
        Days = new SemanticDaysCondition
        {
            Mode = condition.Days.Mode,
            Values = condition.Days.Values.ToList(),
            Source = condition.Days.Source
        },
        DayTypes = condition.DayTypes.ToList(),
        PublicHoliday = condition.PublicHoliday,
        HourType = condition.HourType,
        ShiftType = condition.ShiftType,
        Trigger = condition.Trigger,
        Basis = condition.Basis,
        IsCompounding = condition.IsCompounding,
        BaseRateReference = condition.BaseRateReference,
        PeriodRounding = condition.PeriodRounding,
        StackingPolicy = condition.StackingPolicy,
        DefaultedFields = condition.DefaultedFields.ToList(),
        Evidence = condition.Evidence.Select(e => new SemanticEvidence
        {
            Field = e.Field,
            Text = e.Text,
            Source = e.Source
        }).ToList()
    };

    private static AcceptanceMatrixFixture LoadFixture(string root)
    {
        var path = Path.Combine(root, "award_interpretation_rules_engine.runtime_tests", "fixtures", "ma000120_acceptance_matrix.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AcceptanceMatrixFixture>(json, JsonUtil.Options()) ??
               throw new InvalidOperationException($"Could not read acceptance fixture: {path}");
    }

    private static string WorkspaceRoot()
    {
        var current = Directory.GetCurrentDirectory();
        while (!Directory.Exists(Path.Combine(current, "award_interpretation_rules_engine")) ||
               !Directory.Exists(Path.Combine(current, "award_interpretation_rules_engine.runtime_tests")))
        {
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null)
                throw new InvalidOperationException("Could not locate workspace root.");

            current = parent;
        }

        return current;
    }

    private static class AcceptanceAssert
    {
        public static void Scenario(RuleCalculationResult result, RuleSetVersion snapshot, AcceptanceScenario scenario)
        {
            if (scenario.ExpectedPayrollGross is not null)
                Money(scenario.ExpectedPayrollGross.Value, result.Calculation.PayrollGross, "payroll gross", scenario, snapshot, result);

            if (scenario.ExpectedAwardReferenceGross is not null)
                Money(scenario.ExpectedAwardReferenceGross.Value, result.Calculation.AwardReferenceGross, "award reference gross", scenario, snapshot, result);

            if (scenario.ExpectedBlockedPayrollGross is not null)
                Money(scenario.ExpectedBlockedPayrollGross.Value, result.Calculation.BlockedPayrollGross, "blocked payroll gross", scenario, snapshot, result);

            if (scenario.ExpectedToilAccruedHours is not null)
                Equal(scenario.ExpectedToilAccruedHours.Value, result.Calculation.ToilAccruedHours, "TOIL accrued hours", scenario, snapshot, result);

            ExpectedLines(scenario.ExpectedPayrollLines, result.Calculation.PayrollLines, "payroll line", scenario, snapshot, result);
            ExpectedLines(scenario.ExpectedAwardReferenceLines, result.Calculation.AwardReferenceLines, "award reference line", scenario, snapshot, result);
            ExpectedLines(scenario.ExpectedBlockedPayrollLines, result.Calculation.BlockedPayrollLines, "blocked payroll line", scenario, snapshot, result);

            foreach (var expected in scenario.ExpectedWarnings)
            {
                True(
                    result.Calculation.Warnings.Any(w =>
                        w.RuleId.Equals(expected.RuleId, StringComparison.OrdinalIgnoreCase) &&
                        (expected.BlocksPayrollExport is null || w.BlocksPayrollExport == expected.BlocksPayrollExport)),
                    $"expected warning {expected.RuleId}",
                    scenario,
                    snapshot,
                    result);
            }

            foreach (var ruleId in scenario.ExpectedTraceRuleIds)
            {
                True(
                    result.Calculation.RuleTrace.Any(t => t.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase) && t.Status == "evaluated"),
                    $"expected evaluated trace {ruleId}",
                    scenario,
                    snapshot,
                    result);
            }

            foreach (var line in result.Calculation.PayrollLines.Concat(result.Calculation.AwardReferenceLines).Concat(result.Calculation.BlockedPayrollLines))
            {
                True(!string.IsNullOrWhiteSpace(line.RuleVersion), $"{line.OutputKey} has rule version", scenario, snapshot, result);
                True(!string.IsNullOrWhiteSpace(line.ClauseReference), $"{line.OutputKey} has award clause reference", scenario, snapshot, result);
                True(!string.IsNullOrWhiteSpace(line.Formula), $"{line.OutputKey} has formula trace", scenario, snapshot, result);
                True(
                    result.Calculation.RuleTrace.Any(t =>
                        t.SegmentId == line.SegmentId &&
                        t.RuleId == line.RuleId &&
                        t.Status == "evaluated"),
                    $"{line.OutputKey} has matching rule trace",
                    scenario,
                    snapshot,
                    result);
            }
        }

        public static void Money(
            decimal expected,
            decimal actual,
            string name,
            AcceptanceScenario scenario,
            RuleSetVersion snapshot,
            RuleCalculationResult result)
        {
            if (Math.Abs(expected - actual) > MoneyTolerance)
                throw Failure($"{name}: expected {expected:F2}, actual {actual:F2}.", scenario, snapshot, result);
        }

        private static void ExpectedLines(
            IReadOnlyList<ExpectedLine> expectedLines,
            IReadOnlyList<PayLine> actualLines,
            string lineType,
            AcceptanceScenario scenario,
            RuleSetVersion snapshot,
            RuleCalculationResult result)
        {
            foreach (var expected in expectedLines)
            {
                var actualAmount = actualLines
                    .Where(l => l.OutputKey.Equals(expected.OutputKey, StringComparison.OrdinalIgnoreCase))
                    .Sum(l => l.Amount);

                True(actualAmount > 0m, $"expected {lineType} {expected.OutputKey}", scenario, snapshot, result);

                if (expected.Amount is not null)
                    Money(expected.Amount.Value, actualAmount, $"{lineType} {expected.OutputKey}", scenario, snapshot, result);
            }
        }

        private static void Equal<T>(
            T expected,
            T actual,
            string name,
            AcceptanceScenario scenario,
            RuleSetVersion snapshot,
            RuleCalculationResult result)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw Failure($"{name}: expected {expected}, actual {actual}.", scenario, snapshot, result);
        }

        private static void True(
            bool condition,
            string message,
            AcceptanceScenario scenario,
            RuleSetVersion snapshot,
            RuleCalculationResult result)
        {
            if (!condition)
                throw Failure(message, scenario, snapshot, result);
        }

        private static AcceptanceAssertionException Failure(
            string message,
            AcceptanceScenario scenario,
            RuleSetVersion snapshot,
            RuleCalculationResult result)
        {
            var details = new StringBuilder();
            details.AppendLine($"MA000120 acceptance failure in scenario '{scenario.Name}': {message}");
            details.AppendLine($"RuleSetVersionId: {snapshot.RuleSetVersionId}");
            details.AppendLine($"SnapshotContentHash: {snapshot.SnapshotContentHash}");
            details.AppendLine($"ParserVersion: {snapshot.ParserVersion}");
            details.AppendLine($"CompilerVersion: {snapshot.CompilerVersion}");
            details.AppendLine($"SourceSnapshotHash: {snapshot.SourceSnapshotHash}");
            details.AppendLine($"PayrollGross: {result.Calculation.PayrollGross:F2}");
            details.AppendLine($"AwardReferenceGross: {result.Calculation.AwardReferenceGross:F2}");
            details.AppendLine($"BlockedPayrollGross: {result.Calculation.BlockedPayrollGross:F2}");
            details.AppendLine("Pay lines:");
            foreach (var line in result.Calculation.PayrollLines.Concat(result.Calculation.BlockedPayrollLines).Concat(result.Calculation.AwardReferenceLines).Take(40))
                details.AppendLine($"  {line.SourceBucket} {line.SegmentId} {line.RuleId} v{line.RuleVersion} {line.OutputKey} {line.Amount:F2} clause={line.ClauseReference}");

            details.AppendLine("Warnings:");
            foreach (var warning in result.Calculation.Warnings.Take(40))
                details.AppendLine($"  {warning.SegmentId} {warning.RuleId} severity={warning.Severity} blocks={warning.BlocksPayrollExport} clause={warning.ClauseReference} {warning.Message}");

            details.AppendLine("Rule trace:");
            foreach (var trace in result.Calculation.RuleTrace.Take(80))
                details.AppendLine($"  {trace.SegmentId} {trace.RuleId} v{trace.Version} {trace.Phase} {trace.OutputKey} status={trace.Status} value={trace.Value} error={trace.Error}");

            return new AcceptanceAssertionException(details.ToString());
        }
    }

    private sealed class AcceptanceMatrixFixture
    {
        public string AwardCode { get; set; } = "";
        public string SourceFixturePath { get; set; } = "";
        public string SamplePayRunPath { get; set; } = "";
        public List<AcceptanceScenario> Scenarios { get; set; } = [];
    }

    private sealed class AcceptanceScenario
    {
        public string Name { get; set; } = "";
        public string? PayRunPath { get; set; }
        public PayRunInput? PayRun { get; set; }
        public decimal? ExpectedPayrollGross { get; set; }
        public decimal? ExpectedAwardReferenceGross { get; set; }
        public decimal? ExpectedBlockedPayrollGross { get; set; }
        public decimal? ExpectedToilAccruedHours { get; set; }
        public List<ExpectedLine> ExpectedPayrollLines { get; set; } = [];
        public List<ExpectedLine> ExpectedAwardReferenceLines { get; set; } = [];
        public List<ExpectedLine> ExpectedBlockedPayrollLines { get; set; } = [];
        public List<ExpectedWarning> ExpectedWarnings { get; set; } = [];
        public List<string> ExpectedTraceRuleIds { get; set; } = [];
    }

    private sealed class ExpectedLine
    {
        public string OutputKey { get; set; } = "";
        public decimal? Amount { get; set; }
    }

    private sealed class ExpectedWarning
    {
        public string RuleId { get; set; } = "";
        public bool? BlocksPayrollExport { get; set; }
    }
}

public sealed class AcceptanceAssertionException : Exception
{
    public AcceptanceAssertionException(string message) : base(message)
    {
    }
}
