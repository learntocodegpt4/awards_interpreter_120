using System.Globalization;
using System.Text.Json;
using AwardInterpretationRulesEngine;
using Xunit;

namespace AwardInterpretationRulesEngine.Tests;

public sealed class EdgeCaseMatrixTests
{
    private const decimal MoneyTolerance = 0.005m;

    private static readonly string[] RequiredEdgeCases =
    [
        "cross-midnight",
        "weekend",
        "public-holiday-overlap",
        "part-day-public-holiday",
        "insufficient-rest",
        "meal-breaks",
        "sleepover",
        "travel",
        "laundry",
        "higher-duties",
        "leave-loading",
        "salary-top-up",
        "recall",
        "on-call"
    ];

    [Fact]
    public void Fixture_covers_ros40_edge_case_acceptance_matrix()
    {
        var cases = LoadCases();
        var covered = cases.SelectMany(c => c.EdgeCases).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var edgeCase in RequiredEdgeCases)
            Assert.Contains(edgeCase, covered);

        foreach (var testCase in cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(testCase.Id));
            Assert.False(string.IsNullOrWhiteSpace(testCase.Title));
            Assert.NotEmpty(testCase.AwardEvidence);
            Assert.NotEmpty(testCase.Expected.TraceRuleIds);

            foreach (var evidence in testCase.AwardEvidence)
            {
                Assert.False(string.IsNullOrWhiteSpace(evidence.Clause));
                Assert.False(string.IsNullOrWhiteSpace(evidence.SourceText));
            }

            var hasExpectedOutcome = testCase.Expected.PayrollLines.Count > 0 ||
                                     testCase.Expected.AwardReferenceLines.Count > 0 ||
                                     testCase.Expected.BlockedPayrollLines.Count > 0 ||
                                     testCase.Expected.Warnings.Count > 0;
            Assert.True(hasExpectedOutcome, $"{testCase.Id} must assert pay lines, warnings, or blocked lines.");
        }
    }

    [Theory]
    [MemberData(nameof(MatrixCases))]
    public async Task Runtime_regression_matrix_matches_expected_pay_warnings_blocks_and_traces(EdgeCaseMatrixCase testCase)
    {
        var service = new RuntimeRuleEngineService(
            new InMemoryRuleSnapshotStore([BuildSnapshot()]),
            new RuleEngineRuntimeOptions
            {
                Engine = new EngineOptions
                {
                    IncludeFinalContext = true,
                    BlockPayrollExportOnErrors = true,
                    FixedCalculationTimestampUtc = DateTimeOffset.Parse("2026-03-01T00:00:00Z", CultureInfo.InvariantCulture)
                }
            });

        var result = await service.CalculateAsync(new RuleCalculationRequest
        {
            TenantId = "tenant-edge-matrix",
            CorrelationId = testCase.Id,
            AwardCode = "MA000120",
            RuleSetVersionId = "MA000120-2026-edge-matrix",
            PayRun = testCase.PayRun
        });

        AssertMoney(testCase.Expected.PayrollGross, result.Calculation.PayrollGross, $"{testCase.Id} payroll gross");
        AssertMoney(testCase.Expected.AwardReferenceGross, result.Calculation.AwardReferenceGross, $"{testCase.Id} award reference gross");
        AssertMoney(testCase.Expected.BlockedPayrollGross, result.Calculation.BlockedPayrollGross, $"{testCase.Id} blocked payroll gross");

        AssertLines(testCase.Id, "payroll_lines", testCase.Expected.PayrollLines, result.Calculation.PayrollLines);
        AssertLines(testCase.Id, "award_reference_lines", testCase.Expected.AwardReferenceLines, result.Calculation.AwardReferenceLines);
        AssertLines(testCase.Id, "blocked_payroll_lines", testCase.Expected.BlockedPayrollLines, result.Calculation.BlockedPayrollLines);
        AssertWarnings(testCase.Id, testCase.Expected.Warnings, result.Calculation.Warnings);
        AssertSegmentContexts(testCase.Id, testCase.Expected.SegmentContexts, result.Calculation.SegmentContexts);
        AssertTraceExpectations(testCase, result);
        AssertPayLineEventsCarryTrace(testCase.Id, result);
    }

    public static IEnumerable<object[]> MatrixCases()
        => LoadCases().Select(c => new object[] { c });

    private static IReadOnlyList<EdgeCaseMatrixCase> LoadCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "ma000120-edge-case-matrix.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<EdgeCaseMatrixCase>>(json, JsonUtil.Options()) ??
               throw new InvalidOperationException($"Unable to load edge-case matrix fixture at {path}.");
    }

    private static RuleSetVersion BuildSnapshot()
    {
        var source = new AwardSourceSnapshot
        {
            AwardCode = "MA000120",
            OnlineUrl = "fixture://ma000120/edge-case-matrix",
            SourceRecordId = "MA000120-QA-EDGE-MATRIX",
            ContentSha256 = new string('c', 64),
            RetrievedAtUtc = DateTimeOffset.Parse("2026-03-01T00:00:00Z", CultureInfo.InvariantCulture)
        };
        var document = new ParsedAwardDocument
        {
            AwardCode = "MA000120",
            AwardTitle = "Children's Services Award 2010"
        };
        var interpretation = Ma000120InterpretationBuilder.BuildInterpretation(source, document);
        var library = Ma000120InterpretationBuilder.BuildLibrary(interpretation);

        return new RuleSetVersion
        {
            TenantId = "tenant-edge-matrix",
            RuleSetVersionId = "MA000120-2026-edge-matrix",
            AwardCode = "MA000120",
            PublishedYear = 2026,
            EffectiveFrom = new DateOnly(2026, 3, 1),
            SourceSnapshotHash = "sha256:ma000120-edge-case-matrix",
            ParserVersion = "parser-edge-matrix-test",
            CompilerVersion = "compiler-edge-matrix-test",
            Status = "approved",
            PublishedAt = DateTimeOffset.Parse("2026-03-01T00:00:00Z", CultureInfo.InvariantCulture),
            RulesJson = library
        };
    }

    private static void AssertLines(string caseId, string bucket, IReadOnlyList<ExpectedPayLine> expected, IReadOnlyList<PayLine> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        foreach (var line in expected)
        {
            var match = actual.SingleOrDefault(actualLine =>
                actualLine.SegmentId == line.SegmentId &&
                actualLine.RuleId == line.RuleId &&
                actualLine.OutputKey == line.OutputKey &&
                Math.Abs(actualLine.Amount - line.Amount) <= MoneyTolerance);

            Assert.NotNull(match);
            Assert.False(string.IsNullOrWhiteSpace(match!.ClauseReference), $"{caseId} {bucket} {line.OutputKey} must retain a clause reference.");
            Assert.False(string.IsNullOrWhiteSpace(match.Formula), $"{caseId} {bucket} {line.OutputKey} must retain the governed expression.");

            if (line.RequiresReview is not null)
                Assert.Equal(line.RequiresReview.Value, match.RequiresReview);
        }
    }

    private static void AssertWarnings(string caseId, IReadOnlyList<ExpectedWarning> expected, IReadOnlyList<RuleWarning> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        foreach (var warning in expected)
        {
            var match = actual.SingleOrDefault(actualWarning =>
                actualWarning.SegmentId == warning.SegmentId &&
                actualWarning.RuleId == warning.RuleId &&
                actualWarning.Severity.Equals(warning.Severity, StringComparison.OrdinalIgnoreCase) &&
                actualWarning.BlocksPayrollExport == warning.BlocksPayrollExport);

            Assert.NotNull(match);
            Assert.False(string.IsNullOrWhiteSpace(match!.ClauseReference), $"{caseId} warning {warning.RuleId} must retain a clause reference.");
            Assert.False(string.IsNullOrWhiteSpace(match.ManualReviewPolicy), $"{caseId} warning {warning.RuleId} must retain the review policy.");
        }
    }

    private static void AssertSegmentContexts(string caseId, IReadOnlyList<ExpectedSegmentContext> expected, IReadOnlyList<Dictionary<string, object?>> actual)
    {
        foreach (var segment in expected)
        {
            var context = actual.SingleOrDefault(c => Convert.ToString(c["SegmentId"], CultureInfo.InvariantCulture) == segment.SegmentId);
            Assert.NotNull(context);
            Assert.Equal(segment.ResolvedDayType, Convert.ToString(context!["ResolvedDayType"], CultureInfo.InvariantCulture));
            AssertMoney(segment.PaidHours, ToDecimal(context["PaidHours"]), $"{caseId} {segment.SegmentId} paid hours");
        }
    }

    private static void AssertTraceExpectations(EdgeCaseMatrixCase testCase, RuleCalculationResult result)
    {
        foreach (var ruleId in testCase.Expected.TraceRuleIds)
        {
            var traces = result.Calculation.RuleTrace
                .Where(t => t.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.NotEmpty(traces);
            Assert.All(traces, trace =>
            {
                Assert.Equal("evaluated", trace.Status);
                Assert.False(string.IsNullOrWhiteSpace(trace.ClauseReference), $"{testCase.Id} trace {ruleId} must retain a clause reference.");
                Assert.False(string.IsNullOrWhiteSpace(trace.Expression), $"{testCase.Id} trace {ruleId} must retain the governed expression.");
            });
        }
    }

    private static void AssertPayLineEventsCarryTrace(string caseId, RuleCalculationResult result)
    {
        Assert.Equal(
            result.Calculation.PayrollLines.Count + result.Calculation.BlockedPayrollLines.Count,
            result.PayLineCalculatedEvents.Count);

        foreach (var evt in result.PayLineCalculatedEvents)
        {
            Assert.Equal(caseId, evt.CorrelationId);
            Assert.NotEmpty(evt.RuleTrace);
            Assert.All(evt.RuleTrace, trace => Assert.Equal(evt.PayLine.RuleId, trace.RuleId));
        }
    }

    private static void AssertMoney(decimal expected, decimal actual, string name)
    {
        if (Math.Abs(expected - actual) > MoneyTolerance)
            throw new InvalidOperationException($"{name}: expected {expected:F2}, actual {actual:F2}");
    }

    private static decimal ToDecimal(object? value)
        => Convert.ToDecimal(value, CultureInfo.InvariantCulture);

    public sealed class EdgeCaseMatrixCase
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public List<string> EdgeCases { get; set; } = [];
        public List<AwardEvidence> AwardEvidence { get; set; } = [];
        public PayRunInput PayRun { get; set; } = new();
        public ExpectedOutcome Expected { get; set; } = new();

        public override string ToString() => $"{Id}: {Title}";
    }

    public sealed class AwardEvidence
    {
        public string Clause { get; set; } = "";
        public string SourceText { get; set; } = "";
    }

    public sealed class ExpectedOutcome
    {
        public decimal PayrollGross { get; set; }
        public decimal AwardReferenceGross { get; set; }
        public decimal BlockedPayrollGross { get; set; }
        public List<ExpectedPayLine> PayrollLines { get; set; } = [];
        public List<ExpectedPayLine> AwardReferenceLines { get; set; } = [];
        public List<ExpectedPayLine> BlockedPayrollLines { get; set; } = [];
        public List<ExpectedWarning> Warnings { get; set; } = [];
        public List<string> TraceRuleIds { get; set; } = [];
        public List<ExpectedSegmentContext> SegmentContexts { get; set; } = [];
    }

    public sealed class ExpectedPayLine
    {
        public string SegmentId { get; set; } = "";
        public string RuleId { get; set; } = "";
        public string OutputKey { get; set; } = "";
        public decimal Amount { get; set; }
        public bool? RequiresReview { get; set; }
    }

    public sealed class ExpectedWarning
    {
        public string SegmentId { get; set; } = "";
        public string RuleId { get; set; } = "";
        public string Severity { get; set; } = "";
        public bool BlocksPayrollExport { get; set; }
    }

    public sealed class ExpectedSegmentContext
    {
        public string SegmentId { get; set; } = "";
        public string ResolvedDayType { get; set; } = "";
        public decimal PaidHours { get; set; }
    }
}
