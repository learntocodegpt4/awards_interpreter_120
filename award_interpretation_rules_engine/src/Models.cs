using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AwardInterpretationRulesEngine;

public sealed class AppSettings
{
    public FairWorkApiOptions FairWorkApi { get; set; } = new();
    public OnlineAwardsOptions OnlineAwards { get; set; } = new();
    public EngineOptions Engine { get; set; } = new();

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path)) return new AppSettings();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonUtil.Options()) ?? new AppSettings();
    }
}

public sealed class FairWorkApiOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://api.fwc.gov.au";
    public string SubscriptionKey { get; set; } = "";
    public string SubscriptionHeaderName { get; set; } = "Ocp-Apim-Subscription-Key";
    public string AwardByCodePath { get; set; } = "/modern-awards/pay-database/awards/{awardCode}";
    public string RatesByAwardPath { get; set; } = "/modern-awards/pay-database/awards/{awardCode}/rates";
}

public sealed class OnlineAwardsOptions
{
    public string AwardHtmlUrlTemplate { get; set; } = "https://awards.fairwork.gov.au/{awardCode}.html";
    public DateTimeOffset? FixedRetrievedAtUtc { get; set; }
}

public sealed class EngineOptions
{
    public bool IncludeFinalContext { get; set; } = true;
    public bool BlockPayrollExportOnErrors { get; set; } = true;
    public DateTimeOffset? FixedCalculationTimestampUtc { get; set; }
}

public sealed class CliOptions
{
    public string AwardCode { get; set; } = "MA000120";
    public string InputPath { get; set; } = "samples/sample-payrun-ma000120.json";
    public string ConfigPath { get; set; } = "appsettings.example.json";
    public string OutputDirectory { get; set; } = "output";
    public bool RunAcceptance { get; set; }
    public bool ShowHelp { get; set; }

    public static string HelpText => """
    Usage:
      dotnet run -- --award MA000120 --input samples/sample-payrun-ma000120.json --config appsettings.example.json --out output
      dotnet run -- --acceptance

    Options:
      --award   Award code, e.g. MA000120
      --input   Pay run input JSON
      --config  App settings JSON
      --out     Output directory
      --acceptance  Run deterministic MA000120 acceptance tests
      --help    Show help
    """;

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--award":
                    options.AwardCode = args[++i];
                    break;
                case "--input":
                    options.InputPath = args[++i];
                    break;
                case "--config":
                    options.ConfigPath = args[++i];
                    break;
                case "--out":
                    options.OutputDirectory = args[++i];
                    break;
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--acceptance":
                    options.RunAcceptance = true;
                    break;
            }
        }
        return options;
    }
}

public sealed class PipelineResult
{
    public AwardInterpretation Interpretation { get; set; } = new();
    public ReviewGateResult ReviewGate { get; set; } = new();
    public GovernedExpressionLibrary Library { get; set; } = new();
    public AggregatePayRunResult Calculation { get; set; } = new();
}

public sealed class AwardSourceSnapshot
{
    public string AwardCode { get; set; } = "";
    public string OnlineUrl { get; set; } = "";
    public string Html { get; set; } = "";
    public string? ApiAwardJson { get; set; }
    public string? ApiRatesJson { get; set; }
    public string SourceRecordId { get; set; } = "";
    public string ContentSha256 { get; set; } = "";
    public DateTimeOffset RetrievedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ParsedAwardDocument
{
    public string AwardCode { get; set; } = "";
    public string AwardTitle { get; set; } = "";
    public string ConsolidationSummary { get; set; } = "";
    public List<AwardClause> Clauses { get; set; } = [];
    public string PlainText { get; set; } = "";
    public List<string> ParseWarnings { get; set; } = [];
}

public sealed class AwardClause
{
    public string ClauseNumber { get; set; } = "";
    public string Heading { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class AwardInterpretation
{
    public string AwardCode { get; set; } = "";
    public string AwardName { get; set; } = "";
    public string EffectiveFrom { get; set; } = "";
    public string InterpretationStatus { get; set; } = "draft_requires_payroll_legal_validation";
    public List<SourceReference> Sources { get; set; } = [];
    public List<AwardClause> Clauses { get; set; } = [];
    public List<ClassificationRate> Classifications { get; set; } = [];
    public AllowanceReference Allowances { get; set; } = new();
    public List<StructuredRuleSummary> StructuredRules { get; set; } = [];
    public List<SourceRecord> SourceRecords { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class SourceRecord
{
    public string SourceRecordId { get; set; } = "";
    public string AwardCode { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string SourceUri { get; set; } = "";
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public string ParserVersion { get; set; } = "ma000120-template-parser/1.0.0";
    public string ContentSha256 { get; set; } = "";
    public string RawPayloadRef { get; set; } = "";
    public string ReviewStatus { get; set; } = "candidate";
    public List<NormalisedSourceRow> NormalizedRows { get; set; } = [];
}

public sealed class NormalisedSourceRow
{
    public string RowId { get; set; } = "";
    public string ClauseReference { get; set; } = "";
    public string RuleFamily { get; set; } = "";
    public string SourceText { get; set; } = "";
    public string ParseStatus { get; set; } = "complete_defaulted";
    public decimal Confidence { get; set; } = 1m;
    public ConditionJson ConditionJson { get; set; } = new();
}

public sealed class ConditionJson
{
    public string SchemaVersion { get; set; } = "1.0";
    public string EntityType { get; set; } = "pay_rule";
    public ConditionDays Days { get; set; } = new();
    public List<string> DayTypes { get; set; } = [];
    public bool PublicHoliday { get; set; }
    public string HourType { get; set; } = "";
    public string? TimeWindow { get; set; }
    public string? ShiftType { get; set; }
    public string Trigger { get; set; } = "";
    public string Basis { get; set; } = "";
    public bool IsCompounding { get; set; }
    public string BaseRateReference { get; set; } = "ordinary_rate";
    public string? PeriodRounding { get; set; }
    public string StackingPolicy { get; set; } = "exclusive";
    public List<string> DefaultedFields { get; set; } = [];
    public List<ConditionEvidence> Evidence { get; set; } = [];
}

public sealed class ConditionDays
{
    public string Mode { get; set; } = "include";
    public List<string> Values { get; set; } = [];
    public string Source { get; set; } = "explicit_text";
}

public sealed class ConditionEvidence
{
    public string Field { get; set; } = "";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed class ReviewGateResult
{
    public string Status { get; set; } = "not_evaluated";
    public bool Approved { get; set; }
    public string ApprovalId { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public List<string> Checks { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}

public sealed class SourceReference
{
    public string SourceId { get; set; } = "";
    public string Url { get; set; } = "";
    public string SourceType { get; set; } = "";
    public DateTimeOffset RetrievedAtUtc { get; set; }
}

public sealed class StructuredRuleSummary
{
    public string RuleId { get; set; } = "";
    public string ClauseReference { get; set; } = "";
    public string Description { get; set; } = "";
    public string Output { get; set; } = "";
    public string Governance { get; set; } = "";
}

public sealed class PayRunInput
{
    public string EmployeeReference { get; set; } = "";
    public string PayPeriodReference { get; set; } = "";
    public string Region { get; set; } = "VIC";
    public EmployeeInput Employee { get; set; } = new();
    public List<PayRunDay> Days { get; set; } = [];
    public AllowanceInput Allowances { get; set; } = new();

    public static PayRunInput Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PayRunInput>(json, JsonUtil.Options()) ?? new PayRunInput();
    }
}

public sealed class EmployeeInput
{
    public string ClassificationCode { get; set; } = "CSE_L3";
    public string EmploymentCategory { get; set; } = "full_time";
    public string EmploymentProfileCode { get; set; } = "FULL_TIME";
    public decimal ContractedWeeklyHours { get; set; } = 38m;
    public decimal AnnualSalary { get; set; }
    public decimal AllPurposeAllowanceHourly { get; set; }
    public decimal OpeningToilBalanceHours { get; set; }
}

public sealed class PayRunDay
{
    public DateOnly Date { get; set; }
    public string DayType { get; set; } = "weekday";
    public bool ActualPublicHoliday { get; set; }
    public bool SubstitutedPublicHoliday { get; set; }
    public string PublicHolidayId { get; set; } = "";
    public string PublicHolidayStart { get; set; } = "";
    public string PublicHolidayEnd { get; set; } = "";
    public string PublicHolidayElectionEvidence { get; set; } = "";
    public string LeaveType { get; set; } = "";
    public decimal LeaveHours { get; set; }
    public decimal LeavePenaltyMultiplier { get; set; } = 1m;
    public string RegularStart { get; set; } = "";
    public string RegularEnd { get; set; } = "";
    public string RosterContext { get; set; } = "";
    public List<PayRunShift> Shifts { get; set; } = [];
}

public sealed class PayRunShift
{
    public string Start { get; set; } = "08:00";
    public string End { get; set; } = "16:30";
    public string SegmentKind { get; set; } = "clock";
    public string Tag { get; set; } = "none";
    public string EvidenceReference { get; set; } = "";
    public string Team { get; set; } = "";
    public string RosterStart { get; set; } = "";
    public string RosterEnd { get; set; } = "";
    public string RosterContext { get; set; } = "";
    public bool HigherDutiesEnabled { get; set; }
    public string HigherDutiesClassificationCode { get; set; } = "";
    public string HigherDutiesStart { get; set; } = "";
    public string HigherDutiesEnd { get; set; } = "";
    public List<PayRunBreak> Breaks { get; set; } = [];
}

public sealed class PayRunBreak
{
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public string Type { get; set; } = "meal";
    public bool Paid { get; set; }
    public bool Interrupted { get; set; }
    public bool RequiredToRemainOnPremises { get; set; }
}

public sealed class AllowanceInput
{
    public bool FirstAidRequired { get; set; }
    public bool IsOshc { get; set; }
    public bool LaundryRequired { get; set; }
    public bool LaundryRequiresIroning { get; set; }
    public bool MealAllowanceRequired { get; set; }
    public bool ExcessFaresRequired { get; set; }
    public string VehicleType { get; set; } = "none";
    public decimal VehicleKm { get; set; }
    public decimal EducationalLeaderDaysPerWeek { get; set; }
}

public sealed class PayRunRequest
{
    public string EmployeeReference { get; set; } = "";
    public string PayPeriodReference { get; set; } = "";
    public Dictionary<string, object?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> DerivedParameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class TimesheetNormalisationResult
{
    public List<NormalisedTimesheetSegment> Segments { get; set; } = [];
    public List<PayRunRequest> SegmentRequests { get; set; } = [];
    public List<RuleWarning> Warnings { get; set; } = [];
    public bool BlocksPayrollExport => Warnings.Any(w => w.BlocksPayrollExport);
}

public sealed class NormalisedTimesheetSegment
{
    public string SegmentId { get; set; } = "";
    public string SegmentKind { get; set; } = "clock";
    public DateOnly WorkDate { get; set; }
    public DateTime? SourceShiftStartLocal { get; set; }
    public DateTime? SourceShiftEndLocal { get; set; }
    public DateTime SegmentStartLocal { get; set; }
    public DateTime SegmentEndLocal { get; set; }
    public decimal RawShiftHours { get; set; }
    public decimal WorkedHours { get; set; }
    public decimal PaidHours { get; set; }
    public decimal TotalPaidBreakMinutes { get; set; }
    public decimal TotalUnpaidBreakMinutes { get; set; }
    public decimal TotalUnpaidMealBreakMinutes { get; set; }
    public decimal TotalPaidMealBreakMinutes { get; set; }
    public decimal SegmentPaidBreakMinutes { get; set; }
    public decimal SegmentUnpaidBreakMinutes { get; set; }
    public bool IsWorkedSegment { get; set; }
    public bool IsBreakSegment { get; set; }
    public bool IsPaidBreakSegment { get; set; }
    public bool IsUnpaidBreakSegment { get; set; }
    public bool IsLeave { get; set; }
    public string LeaveType { get; set; } = "";
    public decimal LeaveHours { get; set; }
    public bool IsOnCall { get; set; }
    public bool IsRecall { get; set; }
    public bool IsOvernight { get; set; }
    public string ShiftTag { get; set; } = "none";
    public string RosterContext { get; set; } = "";
    public DateTime? RosterStartLocal { get; set; }
    public DateTime? RosterEndLocal { get; set; }
    public decimal RosterStartMinutes { get; set; }
    public decimal RosterEndMinutes { get; set; }
    public decimal RosterVarianceMinutes { get; set; }
    public string DayType { get; set; } = "weekday";
    public string ResolvedDayType { get; set; } = "weekday";
    public bool IsPublicHolidayFromCalendar { get; set; }
    public bool IsActualPublicHoliday { get; set; }
    public bool IsSubstitutedPublicHoliday { get; set; }
    public bool IsPartDayPublicHoliday { get; set; }
    public string PublicHolidayId { get; set; } = "";
    public DateTime? PublicHolidayStartLocal { get; set; }
    public DateTime? PublicHolidayEndLocal { get; set; }
}

public sealed class AggregatePayRunResult
{
    public string RuleSetVersionId { get; set; } = "";
    public string AwardCode { get; set; } = "";
    public string EmployeeReference { get; set; } = "";
    public string PayPeriodReference { get; set; } = "";
    public decimal PayrollGross { get; set; }
    public decimal AwardReferenceGross { get; set; }
    public decimal BlockedPayrollGross { get; set; }
    public decimal ToilAccruedHours { get; set; }
    public decimal ClosingToilBalanceHours { get; set; }
    public List<PayLine> PayrollLines { get; set; } = [];
    public List<PayLine> AwardReferenceLines { get; set; } = [];
    public List<PayLine> BlockedPayrollLines { get; set; } = [];
    public List<ToilMovement> ToilMovements { get; set; } = [];
    public List<RuleWarning> Warnings { get; set; } = [];
    public List<RuleTrace> RuleTrace { get; set; } = [];
    public List<Dictionary<string, object?>> SegmentContexts { get; set; } = [];
}

public sealed class PayRunResult
{
    public string RuleSetVersionId { get; set; } = "";
    public string LibraryId { get; set; } = "";
    public string AwardCode { get; set; } = "";
    public string AwardName { get; set; } = "";
    public string EffectiveFrom { get; set; } = "";
    public string EmployeeReference { get; set; } = "";
    public string PayPeriodReference { get; set; } = "";
    public string ClassificationCode { get; set; } = "";
    public string ClassificationName { get; set; } = "";
    public DateTimeOffset CalculationTimestampUtc { get; set; }
    public decimal PayrollGross { get; set; }
    public decimal AwardReferenceGross { get; set; }
    public decimal BlockedPayrollGross { get; set; }
    public decimal ToilAccruedHours { get; set; }
    public decimal ClosingToilBalanceHours { get; set; }
    public List<PayLine> PayrollLines { get; set; } = [];
    public List<PayLine> AwardReferenceLines { get; set; } = [];
    public List<PayLine> BlockedPayrollLines { get; set; } = [];
    public List<ToilMovement> ToilMovements { get; set; } = [];
    public List<RuleWarning> Warnings { get; set; } = [];
    public List<RuleTrace> RuleTrace { get; set; } = [];
    public Dictionary<string, object?> FinalContext { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record PayLine
{
    public string SourceBucket { get; init; } = "award_reference_lines";
    public string SegmentId { get; init; } = "";
    public string RuleId { get; init; } = "";
    public string RuleVersion { get; init; } = "";
    public string OutputKey { get; init; } = "";
    public string ClauseReference { get; init; } = "";
    public string PayCategory { get; init; } = "";
    public string Description { get; init; } = "";
    public string Formula { get; init; } = "";
    public string StackingGroup { get; init; } = "";
    public string StackingPolicy { get; init; } = "cumulative";
    public decimal Amount { get; init; }
    public bool Exportable { get; init; }
    public bool RequiresReview { get; init; }
}

public sealed class ToilMovement
{
    public string Type { get; set; } = "accrual";
    public string SegmentId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string ClauseReference { get; set; } = "";
    public decimal Hours { get; set; }
    public string Description { get; set; } = "";
    public List<string> EvidenceRequired { get; set; } = [];
}

public sealed class RuleWarning
{
    public string SegmentId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string ClauseReference { get; set; } = "";
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = "";
    public string ManualReviewPolicy { get; set; } = "";
    public List<string> EvidenceRequirements { get; set; } = [];
    public bool BlocksPayrollExport { get; set; }
}

public sealed class RuleTrace
{
    public string SegmentId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string Version { get; set; } = "";
    public string Phase { get; set; } = "";
    public decimal Precedence { get; set; }
    public string ClauseReference { get; set; } = "";
    public string Expression { get; set; } = "";
    public string OutputKey { get; set; } = "";
    public string Action { get; set; } = "";
    public object? Value { get; set; }
    public decimal? RawValue { get; set; }
    public decimal? RoundedValue { get; set; }
    public string Status { get; set; } = "evaluated";
    public string Error { get; set; } = "";
}

public sealed class GovernedExpressionLibrary
{
    public string LibraryId { get; set; } = "";
    public string LibraryName { get; set; } = "";
    public string SchemaVersion { get; set; } = "1.0.0";
    public string SnapshotId { get; set; } = "";
    public string SnapshotVersion { get; set; } = "1.0.0";
    public string AwardCode { get; set; } = "";
    public string AwardName { get; set; } = "";
    public string EffectiveFrom { get; set; } = "";
    public List<string> SourceRecordIds { get; set; } = [];
    public List<string> SourceContentSha256 { get; set; } = [];
    public RuleSnapshotApproval Approval { get; set; } = new();
    public Orchestration Orchestration { get; set; } = new();
    public ReferenceData ReferenceData { get; set; } = new();
    public List<ParameterDefinition> Parameters { get; set; } = [];
    public List<RuleDefinition> Rules { get; set; } = [];
    public List<PayCategoryMap> PayCategoryMapping { get; set; } = [];
}

public sealed class RuleSnapshotApproval
{
    public string GateStatus { get; set; } = "";
    public string ApprovalId { get; set; } = "";
    public string ApprovedBy { get; set; } = "";
    public DateTimeOffset? ApprovedAtUtc { get; set; }
}

public sealed class Orchestration
{
    public List<string> EvaluationOrder { get; set; } = [];
}

public sealed class ReferenceData
{
    public List<ClassificationRate> Classifications { get; set; } = [];
    public AllowanceReference Allowances { get; set; } = new();
}

public sealed class ClassificationRate
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Weekly { get; set; }
    public decimal Hourly { get; set; }
    public string ClauseReference { get; set; } = "";
}

public sealed class AllowanceReference
{
    public decimal StandardRateWeekly { get; set; }
    public decimal BrokenShiftPercentOfStandardRate { get; set; }
    public decimal LaundryRequiresIroningPerDay { get; set; }
    public decimal LaundryNoIroningPerDay { get; set; }
    public decimal ExcessFaresPerDay { get; set; }
    public decimal MealAllowance { get; set; }
    public decimal VehicleCarPerKm { get; set; }
    public decimal VehicleMotorcyclePerKm { get; set; }
    public decimal EducationalLeaderAnnual { get; set; }
}

public sealed class ParameterDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public object? Default { get; set; }
    public string Description { get; set; } = "";
    public List<string>? AllowedValues { get; set; }
}

public sealed class RuleDefinition
{
    public string RuleId { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string EffectiveFrom { get; set; } = "";
    public string EvaluationPhase { get; set; } = "";
    public decimal Precedence { get; set; }
    public string SourceId { get; set; } = "";
    public string ClauseReference { get; set; } = "";
    public string Description { get; set; } = "";
    public string Expression { get; set; } = "0";
    public string OutputKey { get; set; } = "";
    public string OutputType { get; set; } = "";
    public string Action { get; set; } = "set_value";
    public string StackingGroup { get; set; } = "";
    public string StackingPolicy { get; set; } = "cumulative";
    public string ManualReviewPolicy { get; set; } = "auto";
    public List<string> EvidenceRequirements { get; set; } = [];
}

public sealed class PayCategoryMap
{
    public string OutputKey { get; set; } = "";
    public string DefaultPayCategory { get; set; } = "";
}

public static class JsonUtil
{
    public static JsonSerializerOptions Options(bool indented = false) => new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = indented,
        Converters =
        {
            new JsonStringEnumConverter(),
            new DateOnlyJsonConverter(),
            new ObjectToInferredTypesConverter()
        }
    };

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Options(true));
}

public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateOnly.Parse(reader.GetString() ?? "", CultureInfo.InvariantCulture);

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}

public sealed class ObjectToInferredTypesConverter : JsonConverter<object>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return ReadElement(doc.RootElement);
    }

    private static object? ReadElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetDecimal(out var d) ? d : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(ReadElement).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ReadElement(p.Value), StringComparer.OrdinalIgnoreCase),
        _ => element.GetRawText()
    };

    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
