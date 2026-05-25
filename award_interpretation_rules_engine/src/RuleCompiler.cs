using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DynamicExpresso;

namespace AwardInterpretationRulesEngine;

public sealed class RuleCompilationRequest
{
    public string AwardCode { get; set; } = "MA000120";
    public int PublishedYear { get; set; } = 2026;
    public DateOnly EffectiveFrom { get; set; } = new(2026, 3, 1);
    public DateOnly? EffectiveTo { get; set; }
    public string SourceSnapshotHash { get; set; } = "";
    public string ParserVersion { get; set; } = "";
    public string CompilerVersion { get; set; } = GovernedRuleCompiler.CurrentCompilerVersion;
    public string ApprovedBy { get; set; } = "";
    public DateTimeOffset PublishedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<SemanticRuleRow> SemanticRows { get; set; } = [];
}

public sealed class SemanticRuleRow
{
    public string RowId { get; set; } = "";
    public SemanticAwardReference AwardReference { get; set; } = new();
    public string SourceText { get; set; } = "";
    public string ReviewStatus { get; set; } = "";
    public string ParseStatus { get; set; } = "";
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string ContentSha256 { get; set; } = "";
    public SemanticCondition ConditionJson { get; set; } = new();
}

public sealed class SemanticAwardReference
{
    public string Clause { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
}

public sealed class SemanticCondition
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "";

    [JsonPropertyName("entity_type")]
    public string EntityType { get; set; } = "";

    public SemanticDaysCondition? Days { get; set; }

    [JsonPropertyName("day_types")]
    public List<string> DayTypes { get; set; } = [];

    [JsonPropertyName("public_holiday")]
    public bool? PublicHoliday { get; set; }

    [JsonPropertyName("hour_type")]
    public string? HourType { get; set; }

    [JsonPropertyName("time_window")]
    public JsonElement? TimeWindow { get; set; }

    [JsonPropertyName("shift_type")]
    public string? ShiftType { get; set; }

    public string Trigger { get; set; } = "";
    public string Basis { get; set; } = "";

    [JsonPropertyName("is_compounding")]
    public bool? IsCompounding { get; set; }

    [JsonPropertyName("base_rate_reference")]
    public string? BaseRateReference { get; set; }

    [JsonPropertyName("period_rounding")]
    public string? PeriodRounding { get; set; }

    [JsonPropertyName("stacking_policy")]
    public string? StackingPolicy { get; set; }

    [JsonPropertyName("defaulted_fields")]
    public List<string> DefaultedFields { get; set; } = [];

    public List<SemanticEvidence> Evidence { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; set; }
}

public sealed class SemanticDaysCondition
{
    public string Mode { get; set; } = "include";
    public List<string> Values { get; set; } = [];
    public string Source { get; set; } = "";
}

public sealed class SemanticEvidence
{
    public string Field { get; set; } = "";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
}

public sealed record RuleSetVersion
{
    public string TenantId { get; init; } = "";
    public string RuleSetVersionId { get; init; } = "";
    public string AwardCode { get; init; } = "";
    public int PublishedYear { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string SourceSnapshotHash { get; init; } = "";
    public string ParserVersion { get; init; } = "";
    public string CompilerVersion { get; init; } = "";
    public GovernedExpressionLibrary RulesJson { get; init; } = new();
    public string Status { get; init; } = "published";
    public DateTimeOffset PublishedAt { get; init; }
    public IReadOnlyList<string> SourceRowIds { get; init; } = [];
    public IReadOnlyList<string> SourceContentHashes { get; init; } = [];
    public string SnapshotContentHash { get; init; } = "";
}

public sealed record RuleCompilerDiagnostic(string Code, string Message, string? RowId = null, string Severity = "error");

public sealed class RuleCompilationException : Exception
{
    public RuleCompilationException(IReadOnlyList<RuleCompilerDiagnostic> diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(d => $"{d.Code}: {d.Message}")))
    {
        Diagnostics = diagnostics;
    }

    public IReadOnlyList<RuleCompilerDiagnostic> Diagnostics { get; }
}

public sealed class GovernedRuleCompiler
{
    public const string CurrentCompilerVersion = "rule-compiler.ma000120.v1";

    private static readonly HashSet<string> PublishableParseStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "complete",
        "complete_defaulted"
    };

    private static readonly HashSet<string> AllowedDayValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    };

    private static readonly HashSet<string> AllowedDayTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "weekday", "weekend", "saturday", "sunday", "public_holiday", "everyday", "non_public_holiday", "off_duty_day"
    };

    private static readonly HashSet<string> AllowedHourTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ordinary", "overtime", "on_call", "recall", "sleepover", "any"
    };

    private static readonly HashSet<string> AllowedShiftTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "standard", "night", "afternoon", "early_morning", "broken", "split", "sleepover"
    };

    private static readonly HashSet<string> AllowedPaymentBasis = new(StringComparer.OrdinalIgnoreCase)
    {
        "per_hour", "per_day", "per_week", "per_shift", "per_event", "per_meal", "per_occasion",
        "per_24h_or_part", "per_annum", "percentage_of_base", "per_km"
    };

    private static readonly HashSet<string> ForbiddenConditionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "expression", "dynamicExpresso", "dynamic_expresso", "script", "lambda", "method", "memberAccess", "member_access"
    };

    private static readonly Dictionary<string, string[]> TriggerRuleIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["first_aid"] = ["ALLOW_FIRST_AID_AMOUNT"],
        ["laundry"] = ["ALLOW_LAUNDRY_AMOUNT"],
        ["meal"] = ["ALLOW_MEAL_AMOUNT"],
        ["travel"] = ["ALLOW_EXCESS_FARES_AMOUNT"],
        ["excess_fares"] = ["ALLOW_EXCESS_FARES_AMOUNT"],
        ["vehicle"] = ["ALLOW_VEHICLE_AMOUNT"],
        ["educational_leader"] = ["ALLOW_EDUCATIONAL_LEADER_AMOUNT"],
        ["broken_shift"] = ["ALLOW_BROKEN_SHIFT_AMOUNT"],
        ["public_holiday"] = ["NORM_PENALTY_BASE_RATE", "PH_RESOLVE_IS_PUBLIC_HOLIDAY", "PH_RESOLVE_DAY_TYPE", "PAY_PUBLIC_HOLIDAY_AMOUNT"]
    };

    public RuleSetVersion CompilePublishedSnapshot(RuleCompilationRequest request)
    {
        var diagnostics = Validate(request);
        if (diagnostics.Any(d => d.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)))
            throw new RuleCompilationException(diagnostics);

        var baseline = BuildMa000120BaselineLibrary(request.EffectiveFrom);
        var selectedRules = SelectRules(request, baseline);
        var library = new GovernedExpressionLibrary
        {
            LibraryId = $"{request.AwardCode}_GOVERNED_LIBRARY_{request.EffectiveFrom:yyyyMMdd}",
            LibraryName = $"{baseline.LibraryName} compiled snapshot",
            SchemaVersion = baseline.SchemaVersion,
            AwardCode = request.AwardCode,
            AwardName = baseline.AwardName,
            EffectiveFrom = request.EffectiveFrom.ToString("yyyy-MM-dd"),
            Orchestration = baseline.Orchestration,
            ReferenceData = baseline.ReferenceData,
            Parameters = baseline.Parameters.Select(CloneParameter).ToList(),
            Rules = selectedRules,
            PayCategoryMapping = baseline.PayCategoryMapping
                .Where(m => selectedRules.Any(r => r.OutputKey.Equals(m.OutputKey, StringComparison.OrdinalIgnoreCase)))
                .Select(ClonePayCategoryMap)
                .OrderBy(m => m.OutputKey, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        var snapshot = new RuleSetVersion
        {
            AwardCode = request.AwardCode,
            PublishedYear = request.PublishedYear,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            SourceSnapshotHash = request.SourceSnapshotHash,
            ParserVersion = request.ParserVersion,
            CompilerVersion = string.IsNullOrWhiteSpace(request.CompilerVersion) ? CurrentCompilerVersion : request.CompilerVersion,
            RulesJson = library,
            Status = "published",
            PublishedAt = request.PublishedAtUtc,
            SourceRowIds = request.SemanticRows.Select(r => r.RowId).Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            SourceContentHashes = request.SemanticRows.Select(r => r.ContentSha256).Where(h => !string.IsNullOrWhiteSpace(h)).Order(StringComparer.OrdinalIgnoreCase).ToArray()
        };

        var hash = ComputeContentHash(snapshot);
        return snapshot with
        {
            RuleSetVersionId = $"{request.AwardCode}-ruleset-{request.EffectiveFrom:yyyyMMdd}-{hash[..12]}",
            SnapshotContentHash = hash
        };
    }

    public IReadOnlyList<RuleCompilerDiagnostic> Validate(RuleCompilationRequest request)
    {
        var diagnostics = new List<RuleCompilerDiagnostic>();

        if (!request.AwardCode.Equals("MA000120", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new("UNSUPPORTED_AWARD", "The compiler baseline currently supports MA000120 only."));

        if (request.PublishedYear <= 0)
            diagnostics.Add(new("PUBLISHED_YEAR_REQUIRED", "A published year is required."));

        if (string.IsNullOrWhiteSpace(request.SourceSnapshotHash))
            diagnostics.Add(new("SOURCE_SNAPSHOT_HASH_REQUIRED", "A source snapshot hash is required for immutable publication."));

        if (string.IsNullOrWhiteSpace(request.ParserVersion))
            diagnostics.Add(new("PARSER_VERSION_REQUIRED", "A parser version is required for traceability."));

        if (string.IsNullOrWhiteSpace(request.ApprovedBy))
            diagnostics.Add(new("APPROVER_REQUIRED", "Approved snapshots must record the approving reviewer or workflow identity."));

        if (request.EffectiveTo is not null && request.EffectiveTo <= request.EffectiveFrom)
            diagnostics.Add(new("INVALID_EFFECTIVE_RANGE", "EffectiveTo must be later than EffectiveFrom."));

        if (request.SemanticRows.Count == 0)
            diagnostics.Add(new("NO_SOURCE_ROWS", "At least one approved semantic row is required."));

        foreach (var row in request.SemanticRows)
            ValidateRow(request, row, diagnostics);

        if (diagnostics.Count == 0)
            ValidateCompiledExpressions(request, diagnostics);

        return diagnostics;
    }

    private static GovernedExpressionLibrary BuildMa000120BaselineLibrary(DateOnly effectiveFrom)
    {
        var snapshot = new AwardSourceSnapshot
        {
            AwardCode = "MA000120",
            OnlineUrl = "compiled-semantic-rows://MA000120",
            RetrievedAtUtc = DateTimeOffset.UtcNow
        };
        var document = new ParsedAwardDocument { AwardCode = "MA000120", AwardTitle = "Children's Services Award 2010" };
        var interpretation = Ma000120InterpretationBuilder.BuildInterpretation(snapshot, document);
        interpretation.EffectiveFrom = effectiveFrom.ToString("yyyy-MM-dd");
        return Ma000120InterpretationBuilder.BuildLibrary(interpretation);
    }

    private static void ValidateRow(RuleCompilationRequest request, SemanticRuleRow row, List<RuleCompilerDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(row.RowId))
            diagnostics.Add(new("ROW_ID_REQUIRED", "Every semantic row needs a rowId for traceability."));

        if (!row.ReviewStatus.Equals("approved", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new("ROW_NOT_APPROVED", "Only rows with reviewStatus=approved may be compiled.", row.RowId));

        if (!PublishableParseStatuses.Contains(row.ParseStatus))
            diagnostics.Add(new("ROW_NOT_PUBLISHABLE", $"Parse status '{row.ParseStatus}' cannot publish without review.", row.RowId));

        if (string.IsNullOrWhiteSpace(row.AwardReference.Clause))
            diagnostics.Add(new("CLAUSE_REQUIRED", "Every compiled row must preserve an award clause reference.", row.RowId));

        if (string.IsNullOrWhiteSpace(row.SourceText))
            diagnostics.Add(new("SOURCE_TEXT_REQUIRED", "Every compiled row must preserve source text evidence.", row.RowId));

        if (row.EffectiveFrom is not null && row.EffectiveFrom > request.EffectiveFrom)
            diagnostics.Add(new("ROW_EFFECTIVE_FROM_AFTER_SNAPSHOT", "Row EffectiveFrom cannot be later than the snapshot EffectiveFrom.", row.RowId));

        if (row.EffectiveTo is not null && row.EffectiveTo <= request.EffectiveFrom)
            diagnostics.Add(new("ROW_NOT_EFFECTIVE", "Row is not effective for the snapshot EffectiveFrom date.", row.RowId));

        ValidateCondition(row, diagnostics);
    }

    private static void ValidateCondition(SemanticRuleRow row, List<RuleCompilerDiagnostic> diagnostics)
    {
        var condition = row.ConditionJson;

        if (!condition.SchemaVersion.Equals("1.0", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new("CONDITION_SCHEMA_UNSUPPORTED", "condition_json schema_version must be 1.0.", row.RowId));

        if (!condition.EntityType.Equals("allowance", StringComparison.OrdinalIgnoreCase) &&
            !condition.EntityType.Equals("penalty", StringComparison.OrdinalIgnoreCase) &&
            !condition.EntityType.Equals("pay", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new("ENTITY_TYPE_UNSUPPORTED", $"Unsupported entity_type '{condition.EntityType}'.", row.RowId));

        if (string.IsNullOrWhiteSpace(condition.Trigger))
            diagnostics.Add(new("TRIGGER_REQUIRED", "condition_json trigger is required.", row.RowId));

        if (string.IsNullOrWhiteSpace(condition.Basis))
            diagnostics.Add(new("BASIS_REQUIRED", "condition_json basis is required.", row.RowId));
        else if (!AllowedPaymentBasis.Contains(condition.Basis))
            diagnostics.Add(new("BASIS_UNSUPPORTED", $"Unsupported payment basis '{condition.Basis}'.", row.RowId));

        if (condition.DayTypes.Any(d => !AllowedDayTypes.Contains(d)))
            diagnostics.Add(new("DAY_TYPE_UNSUPPORTED", "condition_json contains an unsupported day_types value.", row.RowId));

        if (condition.Days is not null)
        {
            if (!condition.Days.Mode.Equals("include", StringComparison.OrdinalIgnoreCase) &&
                !condition.Days.Mode.Equals("exclude", StringComparison.OrdinalIgnoreCase))
                diagnostics.Add(new("DAYS_MODE_UNSUPPORTED", "days.mode must be include or exclude.", row.RowId));

            if (condition.Days.Values.Any(d => !AllowedDayValues.Contains(d)))
                diagnostics.Add(new("DAY_VALUE_UNSUPPORTED", "days.values contains an unsupported day value.", row.RowId));
        }

        if (!string.IsNullOrWhiteSpace(condition.HourType) && !AllowedHourTypes.Contains(condition.HourType))
            diagnostics.Add(new("HOUR_TYPE_UNSUPPORTED", $"Unsupported hour_type '{condition.HourType}'.", row.RowId));

        if (!string.IsNullOrWhiteSpace(condition.ShiftType) && !AllowedShiftTypes.Contains(condition.ShiftType))
            diagnostics.Add(new("SHIFT_TYPE_UNSUPPORTED", $"Unsupported shift_type '{condition.ShiftType}'.", row.RowId));

        if (condition.Evidence.Count == 0 || condition.Evidence.Any(e => string.IsNullOrWhiteSpace(e.Field) || string.IsNullOrWhiteSpace(e.Text) || string.IsNullOrWhiteSpace(e.Source)))
            diagnostics.Add(new("EVIDENCE_REQUIRED", "condition_json evidence must include field, text, and source.", row.RowId));

        if (condition.DefaultedFields.Count > 0 && !row.ParseStatus.Equals("complete_defaulted", StringComparison.OrdinalIgnoreCase))
            diagnostics.Add(new("DEFAULT_STATUS_MISMATCH", "defaulted_fields require parseStatus=complete_defaulted.", row.RowId));

        foreach (var field in condition.DefaultedFields)
        {
            var hasEvidence = condition.Evidence.Any(e =>
                e.Field.Equals(field, StringComparison.OrdinalIgnoreCase) &&
                (e.Source.Equals("domain_default", StringComparison.OrdinalIgnoreCase) ||
                 e.Source.Equals("manual_override", StringComparison.OrdinalIgnoreCase)));
            if (!hasEvidence)
                diagnostics.Add(new("DEFAULT_EVIDENCE_REQUIRED", $"Defaulted field '{field}' requires domain_default or manual_override evidence.", row.RowId));
        }

        if (condition.Extensions is not null && condition.Extensions.Keys.Any(ForbiddenConditionKeys.Contains))
            diagnostics.Add(new("RAW_EXPRESSION_FORBIDDEN", "condition_json must not carry raw DynamicExpresso/script expressions.", row.RowId));

        if (ResolveRuleIds(row).Count == 0)
            diagnostics.Add(new("CONDITION_UNSUPPORTED", $"No governed MA000120 rule mapping exists for trigger '{condition.Trigger}'.", row.RowId));
    }

    private static void ValidateCompiledExpressions(RuleCompilationRequest request, List<RuleCompilerDiagnostic> diagnostics)
    {
        var baseline = BuildMa000120BaselineLibrary(request.EffectiveFrom);
        var selectedRules = SelectRules(request, baseline);
        var expressionValidator = new RuleExpressionValidator(baseline.Parameters, selectedRules);

        foreach (var rule in selectedRules)
        {
            var error = expressionValidator.Validate(rule);
            if (error is not null)
                diagnostics.Add(new("EXPRESSION_UNSAFE", error, rule.SourceId));
        }
    }

    private static List<RuleDefinition> SelectRules(RuleCompilationRequest request, GovernedExpressionLibrary baseline)
    {
        var byId = baseline.Rules.ToDictionary(r => r.RuleId, StringComparer.OrdinalIgnoreCase);
        var selected = new Dictionary<string, RuleDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in request.SemanticRows.OrderBy(r => r.RowId, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var ruleId in ResolveRuleIds(row))
            {
                if (!byId.TryGetValue(ruleId, out var baselineRule))
                    throw new InvalidOperationException($"Baseline rule '{ruleId}' is not defined.");

                if (selected.ContainsKey(ruleId))
                    throw new RuleCompilationException([new("DUPLICATE_RULE_MAPPING", $"Multiple source rows map to governed rule '{ruleId}'.", row.RowId)]);

                selected[ruleId] = CloneRule(baselineRule, row, request.EffectiveFrom);
            }
        }

        var phases = baseline.Orchestration.EvaluationOrder;
        return selected.Values
            .OrderBy(r => PhaseIndex(phases, r.EvaluationPhase))
            .ThenBy(r => r.Precedence)
            .ThenBy(r => r.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> ResolveRuleIds(SemanticRuleRow row)
    {
        var condition = row.ConditionJson;
        var ruleIds = new List<string>();

        if (TriggerRuleIds.TryGetValue(condition.Trigger, out var mapped))
            ruleIds.AddRange(mapped);

        if (condition.ShiftType?.Equals("broken", StringComparison.OrdinalIgnoreCase) == true && !ruleIds.Contains("ALLOW_BROKEN_SHIFT_AMOUNT"))
            ruleIds.Add("ALLOW_BROKEN_SHIFT_AMOUNT");

        if ((condition.PublicHoliday == true || condition.DayTypes.Contains("public_holiday", StringComparer.OrdinalIgnoreCase)) &&
            !ruleIds.Contains("PAY_PUBLIC_HOLIDAY_AMOUNT"))
            ruleIds.AddRange(["NORM_PENALTY_BASE_RATE", "PH_RESOLVE_IS_PUBLIC_HOLIDAY", "PH_RESOLVE_DAY_TYPE", "PAY_PUBLIC_HOLIDAY_AMOUNT"]);

        if (condition.DayTypes.Contains("sunday", StringComparer.OrdinalIgnoreCase))
            ruleIds.Add("PAY_SUNDAY_AMOUNT");

        if (condition.DayTypes.Contains("saturday", StringComparer.OrdinalIgnoreCase) && condition.ShiftType is not null)
            ruleIds.Add("PAY_SATURDAY_SHIFTWORKER_AMOUNT");

        return ruleIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static RuleDefinition CloneRule(RuleDefinition baselineRule, SemanticRuleRow row, DateOnly effectiveFrom) => new()
    {
        RuleId = baselineRule.RuleId,
        Version = baselineRule.Version,
        EffectiveFrom = effectiveFrom.ToString("yyyy-MM-dd"),
        EvaluationPhase = baselineRule.EvaluationPhase,
        Precedence = baselineRule.Precedence,
        SourceId = row.RowId,
        ClauseReference = row.AwardReference.Clause,
        Description = $"{baselineRule.Description} Source: {TrimForDescription(row.SourceText)}",
        Expression = baselineRule.Expression,
        OutputKey = baselineRule.OutputKey,
        OutputType = baselineRule.OutputType,
        Action = baselineRule.Action,
        ManualReviewPolicy = baselineRule.ManualReviewPolicy,
        EvidenceRequirements = row.ConditionJson.Evidence.Select(e => $"{e.Field}: {e.Source}").Distinct(StringComparer.OrdinalIgnoreCase).ToList()
    };

    private static ParameterDefinition CloneParameter(ParameterDefinition parameter) => new()
    {
        Name = parameter.Name,
        Type = parameter.Type,
        Required = parameter.Required,
        Default = parameter.Default,
        Description = parameter.Description,
        AllowedValues = parameter.AllowedValues?.ToList()
    };

    private static PayCategoryMap ClonePayCategoryMap(PayCategoryMap map) => new()
    {
        OutputKey = map.OutputKey,
        DefaultPayCategory = map.DefaultPayCategory
    };

    private static int PhaseIndex(List<string> phases, string phase)
    {
        var index = phases.FindIndex(p => p.Equals(phase, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : int.MaxValue;
    }

    private static string TrimForDescription(string sourceText)
    {
        var compact = Regex.Replace(sourceText, @"\s+", " ").Trim();
        return compact.Length <= 160 ? compact : compact[..157] + "...";
    }

    private static string ComputeContentHash(RuleSetVersion snapshot)
    {
        var payload = new
        {
            snapshot.AwardCode,
            snapshot.PublishedYear,
            snapshot.EffectiveFrom,
            snapshot.EffectiveTo,
            snapshot.SourceSnapshotHash,
            snapshot.ParserVersion,
            snapshot.CompilerVersion,
            snapshot.RulesJson,
            snapshot.Status,
            snapshot.SourceRowIds,
            snapshot.SourceContentHashes
        };

        var json = JsonSerializer.Serialize(payload, JsonUtil.Options());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }
}

internal sealed class RuleExpressionValidator
{
    private static readonly Regex IdentifierPattern = new(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
    private static readonly Regex StringLiteralPattern = new("\"(?:\\\\.|[^\"])*\"", RegexOptions.Compiled);

    private static readonly string[] ForbiddenFragments =
    [
        "typeof", "new ", "System.", ".GetType", ".Invoke", "=>", ";", "[", "]", "{", "}", "using "
    ];

    private readonly HashSet<string> _allowedIdentifiers;
    private readonly Interpreter _interpreter;
    private readonly Parameter[] _sampleParameters;

    public RuleExpressionValidator(IEnumerable<ParameterDefinition> parameters, IEnumerable<RuleDefinition> rules)
    {
        _allowedIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "true", "false", "Max", "Min", "RoundMoney", "RoundUpToQuarterHour", "HasTag", "In"
        };

        foreach (var parameter in parameters)
            _allowedIdentifiers.Add(parameter.Name);

        foreach (var rule in rules)
            _allowedIdentifiers.Add(rule.OutputKey);

        _allowedIdentifiers.Add("BaseRate");
        _allowedIdentifiers.Add("AllPurposeAllowanceHourly");
        _allowedIdentifiers.Add("PenaltyBaseRate");
        _allowedIdentifiers.Add("StandardRateWeekly");
        _allowedIdentifiers.Add("AwardReferenceGross");
        _allowedIdentifiers.Add("WeeklySalaryAmount");

        _interpreter = new Interpreter()
            .Reference(typeof(Math))
            .SetFunction("Max", (Func<decimal, decimal, decimal>)Math.Max)
            .SetFunction("Min", (Func<decimal, decimal, decimal>)Math.Min)
            .SetFunction("RoundMoney", (Func<decimal, decimal>)(v => Math.Round(v, 2, MidpointRounding.AwayFromZero)))
            .SetFunction("RoundUpToQuarterHour", (Func<decimal, decimal>)(h => Math.Ceiling(h * 4m) / 4m))
            .SetFunction("HasTag", (Func<string?, string?, bool>)((a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase)))
            .SetFunction("In", (Func<string?, string?, bool>)((value, csv) => (csv ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))));

        _sampleParameters = BuildSampleParameters(parameters, rules).ToArray();
    }

    public string? Validate(RuleDefinition rule)
    {
        if (ForbiddenFragments.Any(fragment => rule.Expression.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            return $"Rule '{rule.RuleId}' contains a forbidden expression fragment.";

        var expressionWithoutStrings = StringLiteralPattern.Replace(rule.Expression, "\"\"");
        foreach (Match match in IdentifierPattern.Matches(expressionWithoutStrings))
        {
            if (!_allowedIdentifiers.Contains(match.Value))
                return $"Rule '{rule.RuleId}' references undeclared identifier '{match.Value}'.";
        }

        try
        {
            _interpreter.Eval(rule.Expression, _sampleParameters);
            return null;
        }
        catch (Exception ex)
        {
            return $"Rule '{rule.RuleId}' failed compile-time expression evaluation: {ex.Message}";
        }
    }

    private static IEnumerable<Parameter> BuildSampleParameters(IEnumerable<ParameterDefinition> parameters, IEnumerable<RuleDefinition> rules)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["BaseRate"] = 30m,
            ["AllPurposeAllowanceHourly"] = 0m,
            ["PenaltyBaseRate"] = 30m,
            ["StandardRateWeekly"] = 1121.80m,
            ["AwardReferenceGross"] = 0m,
            ["WeeklySalaryAmount"] = 0m
        };

        foreach (var parameter in parameters)
            values[parameter.Name] = SampleValue(parameter.Type, parameter.Name);

        foreach (var rule in rules)
            values.TryAdd(rule.OutputKey, SampleValue(rule.OutputType, rule.OutputKey));

        return values.Select(v => new Parameter(v.Key, v.Value?.GetType() ?? typeof(object), v.Value));
    }

    private static object SampleValue(string type, string name)
    {
        if (type.Equals("bool", StringComparison.OrdinalIgnoreCase)) return false;
        if (type.Equals("string", StringComparison.OrdinalIgnoreCase)) return name.Contains("Type", StringComparison.OrdinalIgnoreCase) ? "weekday" : "none";
        return 1m;
    }
}
