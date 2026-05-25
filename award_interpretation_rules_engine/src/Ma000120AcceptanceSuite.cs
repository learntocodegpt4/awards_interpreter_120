namespace AwardInterpretationRulesEngine;

public static class Ma000120AcceptanceSuite
{
    private const decimal MoneyTolerance = 0.005m;

    public static async Task RunAsync(CancellationToken cancellationToken)
    {
        var settings = BuildSettings();

        await AssertSampleParityAsync(settings, cancellationToken);
        await AssertAllowanceCoverageAsync(settings, cancellationToken);
        await AssertPublicHolidayCoverageAsync(settings, cancellationToken);
        await AssertToilCoverageAsync(settings, cancellationToken);
        await AssertRestFatigueCoverageAsync(settings, cancellationToken);
        await AssertLeaveLoadingCoverageAsync(settings, cancellationToken);
        await AssertBlockedExportCoverageAsync(settings, cancellationToken);

        Console.WriteLine("MA000120 acceptance suite passed.");
        Console.WriteLine("Verified: parse -> condition_json -> review gate -> governed snapshot -> deterministic calculation.");
        Console.WriteLine("Coverage: sample parity, ordinary time, overtime, allowances, public holidays, TOIL, rest/fatigue, leave loading, blocked exports, award references and rule traces.");
    }

    private static async Task AssertSampleParityAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var samplePath = Path.Combine(ProjectRoot(), "samples", "sample-payrun-ma000120.json");
        var result = await RunPipelineAsync(settings, PayRunInput.Load(samplePath), cancellationToken);

        AssertPipelineContracts(result);
        AssertMoney(1247.22m, result.Calculation.PayrollGross, "sample payroll gross");
        AssertMoney(1247.22m, result.Calculation.AwardReferenceGross, "sample award reference gross");
        AssertEqual(0m, result.Calculation.BlockedPayrollGross, "sample blocked payroll gross");
        AssertAnyLine(result.Calculation.PayrollLines, "OrdinaryPayAmount", "sample ordinary pay line");
        AssertAnyLine(result.Calculation.PayrollLines, "OvertimeFirstTwoAmount", "sample overtime first two line");
        AssertAnyLine(result.Calculation.PayrollLines, "OvertimeAfterTwoAmount", "sample overtime after two line");
    }

    private static async Task AssertAllowanceCoverageAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var result = await RunPipelineAsync(settings, BaseInput(
            "EMP-ALLOW",
            "2026-W22-ALLOW",
            [
                Weekday("2026-05-25", Shift("08:00", "16:30", breaks: [MealBreak("12:00", "12:30")]))
            ],
            new AllowanceInput { FirstAidRequired = true, LaundryRequired = true, LaundryRequiresIroning = true }), cancellationToken);

        AssertPipelineContracts(result);
        AssertMoney(236.16m, SumLines(result.Calculation.PayrollLines, "OrdinaryPayAmount"), "allowance ordinary amount");
        AssertMoney(12.12m, SumLines(result.Calculation.PayrollLines, "FirstAidAllowanceAmount"), "first aid allowance amount");
        AssertMoney(1.90m, SumLines(result.Calculation.PayrollLines, "LaundryAllowanceAmount"), "laundry allowance amount");
        AssertTraceForOutput(result.Calculation, "FirstAidAllowanceAmount");
        AssertTraceForOutput(result.Calculation, "LaundryAllowanceAmount");
    }

    private static async Task AssertPublicHolidayCoverageAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var result = await RunPipelineAsync(settings, BaseInput(
            "EMP-PH",
            "2026-W22-PH",
            [
                new PayRunDay
                {
                    Date = DateOnly.Parse("2026-05-25"),
                    DayType = "public_holiday",
                    ActualPublicHoliday = true,
                    Shifts = [Shift("09:00", "11:00")]
                }
            ]), cancellationToken);

        AssertPipelineContracts(result);
        AssertMoney(295.20m, SumLines(result.Calculation.PayrollLines, "PublicHolidayAmount"), "public holiday minimum payment");
        AssertTraceForOutput(result.Calculation, "PublicHolidayAmount");
    }

    private static async Task AssertToilCoverageAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var result = await RunPipelineAsync(settings, BaseInput(
            "EMP-TOIL",
            "2026-W22-TOIL",
            [
                Weekday("2026-05-25", Shift("08:00", "18:30", "toil", "TOIL-AGREE-001", [MealBreak("12:00", "12:30")]))
            ]), cancellationToken);

        AssertPipelineContracts(result);
        AssertMoney(236.16m, result.Calculation.PayrollGross, "TOIL suppresses overtime payroll lines");
        AssertMoney(2m, result.Calculation.ToilAccruedHours, "TOIL accrued hours");
        AssertEqual(0m, SumLines(result.Calculation.PayrollLines, "OvertimeFirstTwoAmount"), "TOIL payroll overtime amount");
        AssertMoney(88.56m, SumLines(result.Calculation.AwardReferenceLines, "OvertimeFirstTwoAmount"), "TOIL award reference overtime amount");
        AssertTrue(result.Calculation.ToilMovements.Any(m => m.RuleId == "TOIL_ACCRUAL_HOURS" && m.Hours == 2m), "TOIL movement trace");
    }

    private static async Task AssertRestFatigueCoverageAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var result = await RunPipelineAsync(settings, BaseInput(
            "EMP-FATIGUE",
            "2026-W22-FATIGUE",
            [
                new PayRunDay
                {
                    Date = DateOnly.Parse("2026-05-25"),
                    DayType = "weekday",
                    Shifts =
                    [
                        Shift("08:00", "16:30", breaks: [MealBreak("12:00", "12:30")]),
                        Shift("22:00", "06:30", breaks: [MealBreak("02:00", "02:30")])
                    ]
                }
            ]), cancellationToken);

        AssertPipelineContracts(result);
        AssertTrue(result.Calculation.Warnings.Any(w => w.RuleId == "OT_INSUFFICIENT_REST_TRIGGER"), "fatigue warning exists");
        AssertTrue(result.Calculation.RuleTrace.Any(t => t.RuleId == "OT_INSUFFICIENT_REST_HOURS" && Convert.ToDecimal(t.Value) > 0m), "fatigue overtime trace exists");
    }

    private static async Task AssertLeaveLoadingCoverageAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var result = await RunPipelineAsync(settings, BaseInput(
            "EMP-LEAVE",
            "2026-W22-LEAVE",
            [
                new PayRunDay
                {
                    Date = DateOnly.Parse("2026-05-31"),
                    DayType = "sunday",
                    LeaveType = "annual",
                    LeaveHours = 7.6m,
                    LeavePenaltyMultiplier = 2.0m
                }
            ]), cancellationToken);

        AssertPipelineContracts(result);
        AssertMoney(224.35m, SumLines(result.Calculation.PayrollLines, "AnnualLeaveBaseAmount"), "annual leave base amount");
        AssertMoney(224.35m, SumLines(result.Calculation.PayrollLines, "AnnualLeaveLoadingAmount"), "annual leave higher-of loading amount");
        AssertEqual(0m, SumLines(result.Calculation.PayrollLines, "SundayAmount"), "leave does not also create Sunday worked line");
        AssertTraceForOutput(result.Calculation, "AnnualLeaveLoadingAmount");
    }

    private static async Task AssertBlockedExportCoverageAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var result = await RunPipelineAsync(settings, BaseInput(
            "EMP-BLOCK",
            "2026-W22-BLOCK",
            [
                Weekday("2026-05-25", Shift("08:00", "16:30", breaks: [MealBreak("12:00", "12:30")]))
            ],
            new AllowanceInput { MealAllowanceRequired = true }), cancellationToken);

        AssertPipelineContracts(result);
        AssertMoney(0m, result.Calculation.PayrollGross, "blocked export payroll gross");
        AssertMoney(236.16m, result.Calculation.BlockedPayrollGross, "blocked ordinary payroll amount");
        AssertTrue(result.Calculation.Warnings.Any(w => w.RuleId == "ALLOW_MEAL_AMOUNT" && w.BlocksPayrollExport), "manual meal allowance blocks export");
        AssertAnyLine(result.Calculation.BlockedPayrollLines, "OrdinaryPayAmount", "blocked ordinary line");
    }

    private static async Task<PipelineResult> RunPipelineAsync(AppSettings settings, PayRunInput input, CancellationToken cancellationToken)
    {
        var result = await new AwardPipeline(settings).RunAsync("MA000120", input, cancellationToken);
        var failedTrace = result.Calculation.RuleTrace.Where(t => t.Status != "evaluated").ToList();
        if (failedTrace.Count > 0)
            throw new InvalidOperationException($"Expected all rules to evaluate. Failures: {string.Join(", ", failedTrace.Select(t => $"{t.RuleId}:{t.Error}"))}");

        return result;
    }

    private static void AssertPipelineContracts(PipelineResult result)
    {
        AssertTrue(result.ReviewGate.Approved, "review gate approved");
        AssertEqual("approved_for_compilation", result.Interpretation.InterpretationStatus, "interpretation gate status");
        AssertTrue(result.Interpretation.SourceRecords.All(r => r.ReviewStatus == "approved"), "source records approved by gate");
        AssertTrue(result.Interpretation.SourceRecords.SelectMany(r => r.NormalizedRows).Any(r => r.ConditionJson.Trigger == "overtime"), "condition_json source rows include overtime");
        AssertTrue(result.Library.SnapshotId == "MA000120-2026-03-01-ACCEPTANCE", "governed snapshot id");
        AssertTrue(result.Library.Approval.GateStatus == "approved_for_compilation", "snapshot approval metadata");

        var traced = result.Calculation.RuleTrace
            .Where(t => t.Status == "evaluated")
            .Select(t => (t.SegmentId, t.RuleId))
            .ToHashSet();

        foreach (var line in result.Calculation.PayrollLines.Concat(result.Calculation.AwardReferenceLines).Concat(result.Calculation.BlockedPayrollLines))
            AssertTrue(traced.Contains((line.SegmentId, line.RuleId)), $"line {line.OutputKey} has rule trace");
    }

    private static AppSettings BuildSettings()
    {
        var fixturePath = Path.Combine(ProjectRoot(), "fixtures", "MA000120.acceptance.html");
        return new AppSettings
        {
            OnlineAwards = new OnlineAwardsOptions
            {
                AwardHtmlUrlTemplate = fixturePath,
                FixedRetrievedAtUtc = DateTimeOffset.Parse("2026-03-01T00:00:00Z")
            },
            Engine = new EngineOptions
            {
                IncludeFinalContext = true,
                BlockPayrollExportOnErrors = true,
                FixedCalculationTimestampUtc = DateTimeOffset.Parse("2026-03-01T00:00:00Z")
            }
        };
    }

    private static PayRunInput BaseInput(string employeeReference, string periodReference, List<PayRunDay> days, AllowanceInput? allowances = null) => new()
    {
        EmployeeReference = employeeReference,
        PayPeriodReference = periodReference,
        Region = "VIC",
        Employee = new EmployeeInput
        {
            ClassificationCode = "CSE_L3",
            EmploymentCategory = "full_time",
            EmploymentProfileCode = "FULL_TIME",
            ContractedWeeklyHours = 38m
        },
        Days = days,
        Allowances = allowances ?? new AllowanceInput()
    };

    private static PayRunDay Weekday(string date, PayRunShift shift) => new()
    {
        Date = DateOnly.Parse(date),
        DayType = "weekday",
        Shifts = [shift]
    };

    private static PayRunShift Shift(string start, string end, string tag = "none", string evidence = "", List<PayRunBreak>? breaks = null) => new()
    {
        Start = start,
        End = end,
        Tag = tag,
        EvidenceReference = evidence,
        Breaks = breaks ?? []
    };

    private static PayRunBreak MealBreak(string start, string end) => new()
    {
        Start = start,
        End = end,
        Type = "meal",
        Paid = false
    };

    private static decimal SumLines(IEnumerable<PayLine> lines, string outputKey)
        => lines.Where(l => l.OutputKey == outputKey).Sum(l => l.Amount);

    private static void AssertTraceForOutput(AggregatePayRunResult result, string outputKey)
    {
        var ruleIds = result.AwardReferenceLines
            .Concat(result.PayrollLines)
            .Concat(result.BlockedPayrollLines)
            .Where(l => l.OutputKey == outputKey)
            .Select(l => l.RuleId)
            .Distinct()
            .ToList();

        AssertTrue(ruleIds.Count > 0, $"{outputKey} line exists");
        foreach (var ruleId in ruleIds)
            AssertTrue(result.RuleTrace.Any(t => t.RuleId == ruleId && t.Status == "evaluated"), $"{outputKey} has trace");
    }

    private static void AssertAnyLine(IEnumerable<PayLine> lines, string outputKey, string name)
        => AssertTrue(lines.Any(l => l.OutputKey == outputKey), name);

    private static void AssertMoney(decimal expected, decimal actual, string name)
    {
        if (Math.Abs(expected - actual) > MoneyTolerance)
            throw new InvalidOperationException($"{name}: expected {expected:F2}, actual {actual:F2}");
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}");
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Assertion failed: {name}");
    }

    private static string ProjectRoot()
    {
        var current = Directory.GetCurrentDirectory();
        var nestedProject = Path.Combine(current, "award_interpretation_rules_engine", "AwardInterpretationRulesEngine.csproj");
        if (File.Exists(nestedProject))
            return Path.GetDirectoryName(nestedProject)
                ?? throw new InvalidOperationException("Could not resolve AwardInterpretationRulesEngine project directory.");

        while (!File.Exists(Path.Combine(current, "AwardInterpretationRulesEngine.csproj")))
        {
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null)
                throw new InvalidOperationException("Could not locate AwardInterpretationRulesEngine.csproj.");
            current = parent;
        }

        return current;
    }
}
