using System.Text.Json;
using AwardInterpretationRulesEngine;

var tests = new (string Name, Func<Task> Test)[]
{
    ("parser unit extracts MA000120 clauses from award HTML", ParserExtractsKnownClausesAsync),
    ("parser unit reports warning when no clauses are extracted", ParserReportsEmptyCorpusWarningAsync),
    ("golden corpus matches approved parser output", GoldenCorpusMatchesApprovedOutputAsync),
    ("publishes approved first-aid condition into immutable snapshot", () => RunSync(PublishesApprovedFirstAidSnapshot)),
    ("compiled first-aid snapshot calculates through governed runtime", () => RunSync(CompiledSnapshotCalculatesFirstAid)),
    ("publishes repeatable MA000120 compiler baseline snapshot", () => RunSync(PublishesMa000120CompilerBaselineSnapshot)),
    ("compiled MA000120 baseline calculates POC 2 acceptance scenarios", () => RunSync(CompiledMa000120BaselineCalculatesPoc2Scenarios)),
    ("rejects rows that are not approved", () => RunSync(RejectsUnapprovedRows)),
    ("rejects raw DynamicExpresso expressions in condition_json", () => RunSync(RejectsRawExpression)),
    ("rejects unsupported triggers", () => RunSync(RejectsUnsupportedTrigger)),
    ("rejects invalid effective dates", () => RunSync(RejectsInvalidEffectiveDates))
};

var failures = new List<string>();
foreach (var (name, test) in tests)
{
    try
    {
        await test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex);
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("Failures:");
    foreach (var failure in failures)
        Console.WriteLine($"- {failure}");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"All {tests.Length} parser, golden corpus, and rule compiler tests passed.");

static async Task ParserExtractsKnownClausesAsync()
{
    var document = await ParseHtmlAsync(
        """
        <html>
          <head><title>Fallback title</title></head>
          <body>
            <h1>Children's Services Award 2010 [MA000120]</h1>
            <p>This Fair Work Commission consolidated modern award incorporates all amendments up to and including 1 March 2026.</p>
            <h2>14. Minimum rates</h2>
            <p>Minimum rates include CSE Level 3 at $1121.80 per week and $29.52 per hour.</p>
            <h2>15. Allowances</h2>
            <p>Allowances include first aid and laundry allowances.</p>
            <h2>23. Overtime and penalty rates</h2>
            <p>Overtime is paid at time and a half for the first two hours.</p>
            <h2>27. Public holidays</h2>
            <p>Public holidays are paid at public holiday rates.</p>
          </body>
        </html>
        """);

    AssertEqual("Children's Services Award 2010 [MA000120]", document.AwardTitle, "parser title");
    AssertTrue(document.ConsolidationSummary.Contains("1 March 2026", StringComparison.Ordinal), "parser consolidation summary");
    AssertTrue(document.ParseWarnings.Count == 0, "parser should not warn when clauses are present");

    var clause14 = document.Clauses.Single(c => c.ClauseNumber == "14");
    AssertEqual("Minimum rates", clause14.Heading, "clause 14 heading");
    AssertTrue(clause14.Text.Contains("CSE Level 3", StringComparison.Ordinal), "clause 14 body");
    AssertTrue(document.Clauses.Any(c => c.ClauseNumber == "15"), "clause 15 extracted");
    AssertTrue(document.Clauses.Any(c => c.ClauseNumber == "23"), "clause 23 extracted");
    AssertTrue(document.Clauses.Any(c => c.ClauseNumber == "27"), "clause 27 extracted");
}

static async Task ParserReportsEmptyCorpusWarningAsync()
{
    var document = await ParseHtmlAsync(
        """
        <html>
          <body>
            <h1>Children's Services Award 2010 [MA000120]</h1>
            <p>No numbered clauses are present in this malformed fixture.</p>
          </body>
        </html>
        """);

    AssertTrue(document.Clauses.Count == 0, "malformed fixture should not produce clauses");
    AssertTrue(document.ParseWarnings.Contains("No clauses were extracted. Check public award page markup and parser rules."), "missing clause warning");
}

static async Task GoldenCorpusMatchesApprovedOutputAsync()
{
    var projectRoot = ProjectRoot();
    var repoRoot = Directory.GetParent(projectRoot)?.FullName
        ?? throw new InvalidOperationException("Could not locate repository root.");
    var sourcePath = Path.Combine(repoRoot, "award_interpretation_rules_engine", "fixtures", "MA000120.acceptance.html");
    var expectedPath = Path.Combine(projectRoot, "fixtures", "golden-corpus", "MA000120.acceptance.expected.json");

    var document = await ParseHtmlAsync(await File.ReadAllTextAsync(sourcePath));
    var actual = ParserGoldenOutput.From(document);
    var expected = JsonSerializer.Deserialize<ParserGoldenOutput>(await File.ReadAllTextAsync(expectedPath), JsonUtil.Options())
        ?? throw new InvalidOperationException($"Could not load golden corpus fixture {expectedPath}.");

    var actualJson = JsonSerializer.Serialize(actual, JsonUtil.Options(true));
    var expectedJson = JsonSerializer.Serialize(expected, JsonUtil.Options(true));

    AssertEqual(expectedJson, actualJson, "golden parser output changed; update the approved fixture and apply fixture approval");
}

static async Task<ParsedAwardDocument> ParseHtmlAsync(string html)
{
    var snapshot = new AwardSourceSnapshot
    {
        AwardCode = "MA000120",
        OnlineUrl = "test-fixture://MA000120",
        Html = html,
        RetrievedAtUtc = DateTimeOffset.Parse("2026-03-01T00:00:00Z")
    };

    return await new OnlineAwardHtmlParser().ParseAsync(snapshot, CancellationToken.None);
}

static Task RunSync(Action test)
{
    test();
    return Task.CompletedTask;
}

static void PublishesApprovedFirstAidSnapshot()
{
    var snapshot = Compile(ValidRequest());
    AssertEqual("published", snapshot.Status, "snapshot status");
    AssertEqual("MA000120", snapshot.AwardCode, "award code");
    AssertTrue(snapshot.RuleSetVersionId.StartsWith("MA000120-ruleset-20260301-", StringComparison.Ordinal), "rule_set_version_id includes award/date/hash");
    AssertEqual(64, snapshot.SnapshotContentHash.Length, "content hash length");
    AssertTrue(snapshot.SourceRowIds.SequenceEqual(["row-first-aid"]), "source row id trace");

    var rule = snapshot.RulesJson.Rules.Single();
    AssertEqual("ALLOW_FIRST_AID_AMOUNT", rule.RuleId, "compiled rule id");
    AssertEqual("row-first-aid", rule.SourceId, "compiled rule source id");
    AssertEqual("15.5", rule.ClauseReference, "compiled rule clause");
    AssertEqual("!IsLeave && IsFirstPayableSegmentForDay && FirstAidRequired ? (IsOSHC ? PayableDayWorkedHours * StandardRateWeekly * 0.0014m : StandardRateWeekly * 0.0108m) : 0m", rule.Expression, "baseline expression");
}

static void CompiledSnapshotCalculatesFirstAid()
{
    var snapshot = Compile(ValidRequest());
    var engine = new GovernedAwardRuleEngine(snapshot.RulesJson, new EngineOptions());
    var result = engine.Calculate(new PayRunRequest
    {
        EmployeeReference = "E-100",
        PayPeriodReference = "P-2026-03-01",
        Parameters = BuildCompleteParameters(snapshot.RulesJson)
    });

    var line = result.PayrollLines.Single(l => l.OutputKey == "FirstAidAllowanceAmount");
    AssertEqual(12.12m, line.Amount, "first-aid allowance amount");
    AssertEqual("evaluated", result.RuleTrace.Single(t => t.RuleId == "ALLOW_FIRST_AID_AMOUNT").Status, "runtime trace status");
    AssertTrue(result.Warnings.All(w => w.Severity != "error"), "runtime should not emit compiler-related errors");
}

static void PublishesMa000120CompilerBaselineSnapshot()
{
    var request = LoadBaselineRequest();
    var snapshot = Compile(request);
    var repeatedSnapshot = Compile(LoadBaselineRequest());

    AssertEqual("published", snapshot.Status, "baseline snapshot status");
    AssertEqual("MA000120", snapshot.AwardCode, "baseline award code");
    AssertEqual(new DateOnly(2026, 3, 1), snapshot.EffectiveFrom, "baseline effective_from");
    AssertEqual("afd49d15dce3743067575bfc4fa1ee9d1163f9ea2fa0167fa3027b73ab25ea67", snapshot.SourceSnapshotHash, "baseline source hash");
    AssertEqual("ma000120-template-parser/1.0.0", snapshot.ParserVersion, "baseline parser version");
    AssertEqual(GovernedRuleCompiler.CurrentCompilerVersion, snapshot.CompilerVersion, "baseline compiler version");
    AssertEqual(8, snapshot.SourceRowIds.Count, "baseline semantic row count");
    AssertEqual(8, snapshot.SourceContentHashes.Count, "baseline source row hash count");
    AssertEqual(64, snapshot.SnapshotContentHash.Length, "baseline content hash length");
    AssertEqual(snapshot.SnapshotContentHash, repeatedSnapshot.SnapshotContentHash, "baseline content hash is repeatable");
    AssertEqual(snapshot.RuleSetVersionId, repeatedSnapshot.RuleSetVersionId, "baseline rule version id is repeatable");

    AssertRule(snapshot, "PAY_ORDINARY_HOURS", "21", "MA000120-COMPILER-BASELINE-ORDINARY_TIME");
    AssertRule(snapshot, "PAY_OVERTIME_FIRST_TWO_AMOUNT", "23.2", "MA000120-COMPILER-BASELINE-OVERTIME");
    AssertRule(snapshot, "ALLOW_FIRST_AID_AMOUNT", "15", "MA000120-COMPILER-BASELINE-ALLOWANCES");
    AssertRule(snapshot, "PAY_PUBLIC_HOLIDAY_AMOUNT", "27", "MA000120-COMPILER-BASELINE-PUBLIC_HOLIDAY");
    AssertRule(snapshot, "TOIL_ACCRUAL_HOURS", "23.8", "MA000120-COMPILER-BASELINE-TOIL");
    AssertRule(snapshot, "OT_INSUFFICIENT_REST_TRIGGER", "22.3", "MA000120-COMPILER-BASELINE-REST_FATIGUE");
    AssertRule(snapshot, "LEAVE_ANNUAL_LOADING_AMOUNT", "25", "MA000120-COMPILER-BASELINE-LEAVE_LOADING");
    AssertRule(snapshot, "PRE_EVIDENCE_REQUIRED_FOR_AGREEMENT_TAGS", "Governance", "MA000120-COMPILER-BASELINE-BLOCKED_EXPORTS");
}

static void CompiledMa000120BaselineCalculatesPoc2Scenarios()
{
    var snapshot = Compile(LoadBaselineRequest());

    var sample = Calculate(snapshot, PayRunInput.Load(Path.Combine(EngineProjectRoot(), "samples", "sample-payrun-ma000120.json")));
    AssertRuntimeContracts(sample);
    AssertMoney(1247.22m, sample.Calculation.PayrollGross, "compiled sample payroll gross");
    AssertMoney(1247.22m, sample.Calculation.AwardReferenceGross, "compiled sample award reference gross");
    AssertEqual(0m, sample.Calculation.BlockedPayrollGross, "compiled sample blocked gross");
    AssertAnyLine(sample.Calculation.PayrollLines, "OrdinaryPayAmount", "compiled sample ordinary line");
    AssertAnyLine(sample.Calculation.PayrollLines, "OvertimeFirstTwoAmount", "compiled sample overtime first two line");
    AssertAnyLine(sample.Calculation.PayrollLines, "OvertimeAfterTwoAmount", "compiled sample overtime after two line");

    var allowances = Calculate(snapshot, BaseInput(
        "EMP-ALLOW",
        "2026-W22-ALLOW",
        [Weekday("2026-05-25", Shift("08:00", "16:30", breaks: [MealBreak("12:00", "12:30")]))],
        new AllowanceInput { FirstAidRequired = true, LaundryRequired = true, LaundryRequiresIroning = true }));
    AssertRuntimeContracts(allowances);
    AssertMoney(236.16m, SumLines(allowances.Calculation.PayrollLines, "OrdinaryPayAmount"), "compiled allowance ordinary amount");
    AssertMoney(12.12m, SumLines(allowances.Calculation.PayrollLines, "FirstAidAllowanceAmount"), "compiled first aid allowance amount");
    AssertMoney(1.90m, SumLines(allowances.Calculation.PayrollLines, "LaundryAllowanceAmount"), "compiled laundry allowance amount");

    var publicHoliday = Calculate(snapshot, BaseInput(
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
        ]));
    AssertRuntimeContracts(publicHoliday);
    AssertMoney(295.20m, SumLines(publicHoliday.Calculation.PayrollLines, "PublicHolidayAmount"), "compiled public holiday minimum payment");

    var toil = Calculate(snapshot, BaseInput(
        "EMP-TOIL",
        "2026-W22-TOIL",
        [Weekday("2026-05-25", Shift("08:00", "18:30", "toil", "TOIL-AGREE-001", [MealBreak("12:00", "12:30")]))]));
    AssertRuntimeContracts(toil);
    AssertMoney(236.16m, toil.Calculation.PayrollGross, "compiled TOIL suppresses overtime payroll lines");
    AssertMoney(2m, toil.Calculation.ToilAccruedHours, "compiled TOIL accrued hours");
    AssertEqual(0m, SumLines(toil.Calculation.PayrollLines, "OvertimeFirstTwoAmount"), "compiled TOIL payroll overtime amount");
    AssertMoney(88.56m, SumLines(toil.Calculation.AwardReferenceLines, "OvertimeFirstTwoAmount"), "compiled TOIL award reference overtime amount");

    var fatigue = Calculate(snapshot, BaseInput(
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
        ]));
    AssertRuntimeContracts(fatigue);
    AssertTrue(fatigue.Calculation.Warnings.Any(w => w.RuleId == "OT_INSUFFICIENT_REST_TRIGGER"), "compiled fatigue warning exists");
    AssertTrue(fatigue.Calculation.RuleTrace.Any(t => t.RuleId == "OT_INSUFFICIENT_REST_HOURS" && Convert.ToDecimal(t.Value) > 0m), "compiled fatigue overtime trace exists");

    var leave = Calculate(snapshot, BaseInput(
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
        ]));
    AssertRuntimeContracts(leave);
    AssertMoney(224.35m, SumLines(leave.Calculation.PayrollLines, "AnnualLeaveBaseAmount"), "compiled annual leave base amount");
    AssertMoney(224.35m, SumLines(leave.Calculation.PayrollLines, "AnnualLeaveLoadingAmount"), "compiled annual leave loading amount");
    AssertEqual(0m, SumLines(leave.Calculation.PayrollLines, "SundayAmount"), "compiled leave does not create Sunday worked line");

    var blocked = Calculate(snapshot, BaseInput(
        "EMP-BLOCK",
        "2026-W22-BLOCK",
        [Weekday("2026-05-25", Shift("08:00", "16:30", breaks: [MealBreak("12:00", "12:30")]))],
        new AllowanceInput { MealAllowanceRequired = true }));
    AssertRuntimeContracts(blocked);
    AssertMoney(0m, blocked.Calculation.PayrollGross, "compiled blocked export payroll gross");
    AssertMoney(236.16m, blocked.Calculation.BlockedPayrollGross, "compiled blocked ordinary payroll amount");
    AssertTrue(blocked.Calculation.Warnings.Any(w => w.RuleId == "ALLOW_MEAL_AMOUNT" && w.BlocksPayrollExport), "compiled manual meal allowance blocks export");
    AssertAnyLine(blocked.Calculation.BlockedPayrollLines, "OrdinaryPayAmount", "compiled blocked ordinary line");
}

static void RejectsUnapprovedRows()
{
    var request = ValidRequest();
    request.SemanticRows[0].ReviewStatus = "candidate";
    var exception = AssertCompilationFails(request);
    AssertDiagnostic(exception, "ROW_NOT_APPROVED");
}

static void RejectsRawExpression()
{
    var request = ValidRequest();
    request.SemanticRows[0].ConditionJson = JsonSerializer.Deserialize<SemanticCondition>(
        """
        {
          "schema_version": "1.0",
          "entity_type": "allowance",
          "trigger": "first_aid",
          "basis": "per_week",
          "expression": "System.Environment.Exit(1)",
          "evidence": [
            { "field": "trigger", "text": "First aid allowance", "source": "allowance" }
          ]
        }
        """,
        JsonUtil.Options())!;

    var exception = AssertCompilationFails(request);
    AssertDiagnostic(exception, "RAW_EXPRESSION_FORBIDDEN");
}

static void RejectsUnsupportedTrigger()
{
    var request = ValidRequest();
    request.SemanticRows[0].ConditionJson.Trigger = "on_call";
    var exception = AssertCompilationFails(request);
    AssertDiagnostic(exception, "CONDITION_UNSUPPORTED");
}

static void RejectsInvalidEffectiveDates()
{
    var request = ValidRequest();
    request.EffectiveTo = request.EffectiveFrom;
    var exception = AssertCompilationFails(request);
    AssertDiagnostic(exception, "INVALID_EFFECTIVE_RANGE");
}

static RuleSetVersion Compile(RuleCompilationRequest request)
    => new GovernedRuleCompiler().CompilePublishedSnapshot(request);

static RuleCompilationException AssertCompilationFails(RuleCompilationRequest request)
{
    try
    {
        Compile(request);
    }
    catch (RuleCompilationException ex)
    {
        return ex;
    }

    throw new InvalidOperationException("Expected compilation to fail.");
}

static void AssertDiagnostic(RuleCompilationException exception, string code)
    => AssertTrue(exception.Diagnostics.Any(d => d.Code == code), $"Expected diagnostic {code}. Actual: {string.Join(", ", exception.Diagnostics.Select(d => d.Code))}");

static RuleCompilationRequest ValidRequest() => new()
{
    AwardCode = "MA000120",
    PublishedYear = 2026,
    EffectiveFrom = new DateOnly(2026, 3, 1),
    SourceSnapshotHash = new string('a', 64),
    ParserVersion = "clause-interpreter.test.v1",
    ApprovedBy = "compiler-test-reviewer",
    PublishedAtUtc = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero),
    SemanticRows =
    [
        new SemanticRuleRow
        {
            RowId = "row-first-aid",
            AwardReference = new SemanticAwardReference { Clause = "15.5", Title = "First aid allowance" },
            SourceText = "An employee appointed by the employer to perform first aid duty is paid the first aid allowance.",
            ReviewStatus = "approved",
            ParseStatus = "complete",
            EffectiveFrom = new DateOnly(2026, 3, 1),
            ContentSha256 = new string('b', 64),
            ConditionJson = new SemanticCondition
            {
                SchemaVersion = "1.0",
                EntityType = "allowance",
                Trigger = "first_aid",
                Basis = "per_week",
                DayTypes = ["everyday"],
                Evidence =
                [
                    new SemanticEvidence { Field = "trigger", Text = "first aid allowance", Source = "allowance" },
                    new SemanticEvidence { Field = "basis", Text = "weekly allowance", Source = "payment_frequency" }
                ]
            }
        }
    ]
};

static RuleCompilationRequest LoadBaselineRequest()
{
    var path = Path.Combine(EngineProjectRoot(), "fixtures", "MA000120.compiler-baseline.semantic-rows.json");
    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<RuleCompilationRequest>(json, JsonUtil.Options())
        ?? throw new InvalidOperationException($"Could not load compiler baseline fixture at {path}.");
}

static RuleCalculationResult Calculate(RuleSetVersion snapshot, PayRunInput payRun)
{
    var service = new RuntimeRuleEngineService(
        new InMemoryRuleSnapshotStore([snapshot]),
        new RuleEngineRuntimeOptions
        {
            Engine = new EngineOptions
            {
                IncludeFinalContext = true,
                BlockPayrollExportOnErrors = true,
                FixedCalculationTimestampUtc = DateTimeOffset.Parse("2026-03-01T00:00:00Z")
            }
        });

    return service.CalculateAsync(new RuleCalculationRequest
    {
        TenantId = "tenant-baseline",
        CorrelationId = $"compiler-baseline-{payRun.PayPeriodReference}",
        AwardCode = "MA000120",
        RuleSetVersionId = snapshot.RuleSetVersionId,
        PayRun = payRun
    }).GetAwaiter().GetResult();
}

static PayRunInput BaseInput(string employeeReference, string periodReference, List<PayRunDay> days, AllowanceInput? allowances = null) => new()
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

static PayRunDay Weekday(string date, PayRunShift shift) => new()
{
    Date = DateOnly.Parse(date),
    DayType = "weekday",
    Shifts = [shift]
};

static PayRunShift Shift(string start, string end, string tag = "none", string evidence = "", List<PayRunBreak>? breaks = null) => new()
{
    Start = start,
    End = end,
    Tag = tag,
    EvidenceReference = evidence,
    Breaks = breaks ?? []
};

static PayRunBreak MealBreak(string start, string end) => new()
{
    Start = start,
    End = end,
    Type = "meal",
    Paid = false
};

static Dictionary<string, object?> BuildCompleteParameters(GovernedExpressionLibrary library)
{
    var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    foreach (var parameter in library.Parameters)
        parameters[parameter.Name] = SampleValue(parameter.Type, parameter.Name);

    parameters["ClassificationCode"] = "CSE_L3";
    parameters["EmploymentCategory"] = "full_time";
    parameters["EmploymentProfileCode"] = "FULL_TIME";
    parameters["DayType"] = "weekday";
    parameters["ResolvedDayType"] = "weekday";
    parameters["ShiftTag"] = "none";
    parameters["HasEvidenceReference"] = true;
    parameters["WorkedHours"] = 8m;
    parameters["RawShiftHours"] = 8m;
    parameters["PaidHours"] = 8m;
    parameters["PaidHoursBeforeSegmentInShift"] = 0m;
    parameters["WeeklyHoursBeforeShift"] = 0m;
    parameters["ContractedWeeklyHours"] = 38m;
    parameters["ShiftStartMinutes"] = 480m;
    parameters["ShiftEndMinutes"] = 960m;
    parameters["IsShiftworker"] = false;
    parameters["IsPermanentNightShift"] = false;
    parameters["IsLeave"] = false;
    parameters["IsFirstPayableSegmentForDay"] = true;
    parameters["PayableDayWorkedHours"] = 8m;
    parameters["LeaveType"] = "";
    parameters["LeaveHours"] = 0m;
    parameters["LeavePenaltyMultiplier"] = 1m;
    parameters["IsPublicHolidayFromCalendar"] = false;
    parameters["IsActualPublicHoliday"] = false;
    parameters["IsSubstitutedPublicHoliday"] = false;
    parameters["HasPublicHolidayElectionEvidence"] = false;
    parameters["UnpaidMealBreakMinutes"] = 30m;
    parameters["PaidMealBreakMinutes"] = 0m;
    parameters["MealBreakInterrupted"] = false;
    parameters["RequiredToRemainOnPremises"] = false;
    parameters["PaidRestPauseCount"] = 0m;
    parameters["RestHoursSincePreviousShift"] = 12m;
    parameters["BrokenShiftCount"] = 1m;
    parameters["BrokenShiftSpreadHours"] = 8m;
    parameters["WorkedHoursBeyondBrokenSpreadCap"] = 0m;
    parameters["OutsideOrdinarySpanHours"] = 0m;
    parameters["PartTimeOutsideRegularPatternHours"] = 0m;
    parameters["MissedMealPenaltyHours"] = 0m;
    parameters["HigherDutiesHours"] = 0m;
    parameters["HigherDutiesRate"] = 0m;
    parameters["OpeningToilBalanceHours"] = 0m;
    parameters["ToilTakenHours"] = 0m;
    parameters["ForceToilPayoutHours"] = 0m;
    parameters["AnnualSalary"] = 0m;
    parameters["VehicleKm"] = 0m;
    parameters["VehicleType"] = "none";
    parameters["FirstAidRequired"] = true;
    parameters["IsOSHC"] = false;
    parameters["LaundryRequired"] = false;
    parameters["LaundryRequiresIroning"] = false;
    parameters["MealAllowanceRequired"] = false;
    parameters["ExcessFaresRequired"] = false;
    parameters["EducationalLeaderDaysPerWeek"] = 0m;
    return parameters;
}

static object SampleValue(string type, string name)
{
    if (type.Equals("bool", StringComparison.OrdinalIgnoreCase)) return false;
    if (type.Equals("string", StringComparison.OrdinalIgnoreCase)) return name.Contains("Type", StringComparison.OrdinalIgnoreCase) ? "weekday" : "";
    return 0m;
}

static void AssertRule(RuleSetVersion snapshot, string ruleId, string clauseReference, string sourceId)
{
    var rule = snapshot.RulesJson.Rules.SingleOrDefault(r => r.RuleId == ruleId)
        ?? throw new InvalidOperationException($"Expected compiled rule {ruleId}.");

    AssertEqual(clauseReference, rule.ClauseReference, $"{ruleId} clause reference");
    AssertEqual(sourceId, rule.SourceId, $"{ruleId} source id");
    AssertTrue(rule.EvidenceRequirements.Count > 0, $"{ruleId} evidence requirements");
}

static void AssertRuntimeContracts(RuleCalculationResult result)
{
    AssertTrue(result.ComplianceExceptions.All(e => e.RuleId != "RULE_SNAPSHOT_REJECTED"), "compiled snapshot accepted by runtime service");

    var failedTrace = result.Calculation.RuleTrace.Where(t => t.Status != "evaluated").ToList();
    AssertTrue(failedTrace.Count == 0, $"all compiled rules evaluate. Failures: {string.Join(", ", failedTrace.Select(t => $"{t.RuleId}:{t.Error}"))}");

    var traced = result.Calculation.RuleTrace
        .Where(t => t.Status == "evaluated")
        .Select(t => (t.SegmentId, t.RuleId))
        .ToHashSet();

    foreach (var line in result.Calculation.PayrollLines.Concat(result.Calculation.AwardReferenceLines).Concat(result.Calculation.BlockedPayrollLines))
        AssertTrue(traced.Contains((line.SegmentId, line.RuleId)), $"line {line.OutputKey} has rule trace");
}

static decimal SumLines(IEnumerable<PayLine> lines, string outputKey)
    => lines.Where(l => l.OutputKey == outputKey).Sum(l => l.Amount);

static void AssertAnyLine(IEnumerable<PayLine> lines, string outputKey, string name)
    => AssertTrue(lines.Any(l => l.OutputKey == outputKey), name);

static void AssertMoney(decimal expected, decimal actual, string name)
{
    if (Math.Abs(expected - actual) > 0.005m)
        throw new InvalidOperationException($"{name}: expected {expected:F2}, actual {actual:F2}");
}

static string EngineProjectRoot()
{
    var current = Directory.GetCurrentDirectory();
    while (!File.Exists(Path.Combine(current, "AwardInterpretationRulesEngine.csproj")))
    {
        var childProject = Path.Combine(current, "award_interpretation_rules_engine", "AwardInterpretationRulesEngine.csproj");
        if (File.Exists(childProject))
            return Path.Combine(current, "award_interpretation_rules_engine");

        var parent = Directory.GetParent(current)?.FullName;
        if (parent is null)
            throw new InvalidOperationException("Could not locate AwardInterpretationRulesEngine.csproj.");
        current = parent;
    }

    return current;
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', actual '{actual}'.");
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string ProjectRoot()
{
    var current = Directory.GetCurrentDirectory();
    var nestedProject = Path.Combine(current, "award_interpretation_rules_engine.tests", "AwardInterpretationRulesEngine.Tests.csproj");
    if (File.Exists(nestedProject))
        return Path.GetDirectoryName(nestedProject)
            ?? throw new InvalidOperationException("Could not resolve test project directory.");

    while (!File.Exists(Path.Combine(current, "AwardInterpretationRulesEngine.Tests.csproj")))
    {
        var parent = Directory.GetParent(current)?.FullName;
        if (parent is null)
            throw new InvalidOperationException("Could not locate AwardInterpretationRulesEngine.Tests.csproj.");
        current = parent;
    }

    return current;
}

sealed record ParserGoldenOutput(
    string AwardCode,
    string AwardTitle,
    string ConsolidationSummary,
    IReadOnlyList<ParserGoldenClause> Clauses,
    IReadOnlyList<string> ParseWarnings)
{
    public static ParserGoldenOutput From(ParsedAwardDocument document) => new(
        document.AwardCode,
        document.AwardTitle,
        document.ConsolidationSummary,
        document.Clauses
            .Select(c => new ParserGoldenClause(c.ClauseNumber, c.Heading, c.Text))
            .ToArray(),
        document.ParseWarnings.Order(StringComparer.OrdinalIgnoreCase).ToArray());
}

sealed record ParserGoldenClause(string ClauseNumber, string Heading, string Text);
