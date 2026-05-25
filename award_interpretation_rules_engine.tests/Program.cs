using System.Text.Json;
using AwardInterpretationRulesEngine;

var tests = new (string Name, Func<Task> Test)[]
{
    ("parser unit extracts MA000120 clauses from award HTML", ParserExtractsKnownClausesAsync),
    ("parser unit reports warning when no clauses are extracted", ParserReportsEmptyCorpusWarningAsync),
    ("golden corpus matches approved parser output", GoldenCorpusMatchesApprovedOutputAsync),
    ("publishes approved first-aid condition into immutable snapshot", () => RunSync(PublishesApprovedFirstAidSnapshot)),
    ("compiled first-aid snapshot calculates through governed runtime", () => RunSync(CompiledSnapshotCalculatesFirstAid)),
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
