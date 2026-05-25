using System.Text.Json;
using AwardInterpretationRulesEngine;

var tests = new (string Name, Action Test)[]
{
    ("publishes approved first-aid condition into immutable snapshot", PublishesApprovedFirstAidSnapshot),
    ("compiled first-aid snapshot calculates through governed runtime", CompiledSnapshotCalculatesFirstAid),
    ("rejects rows that are not approved", RejectsUnapprovedRows),
    ("rejects raw DynamicExpresso expressions in condition_json", RejectsRawExpression),
    ("rejects unsupported triggers", RejectsUnsupportedTrigger),
    ("rejects invalid effective dates", RejectsInvalidEffectiveDates)
};

var failures = new List<string>();
foreach (var (name, test) in tests)
{
    try
    {
        test();
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
Console.WriteLine($"All {tests.Length} rule compiler tests passed.");

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
