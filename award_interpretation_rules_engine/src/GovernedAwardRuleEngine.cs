using System.Globalization;
using System.Text.Json;
using DynamicExpresso;

namespace AwardInterpretationRulesEngine;

public sealed class GovernedAwardRuleEngine
{
    private readonly GovernedExpressionLibrary _library;
    private readonly EngineOptions _options;
    private readonly Interpreter _interpreter;
    private readonly string _ruleSetVersionId;
    private readonly IExpressionParameterBinder _parameterBinder;

    public GovernedAwardRuleEngine(
        GovernedExpressionLibrary library,
        EngineOptions options,
        string ruleSetVersionId = "",
        IExpressionParameterBinder? parameterBinder = null)
    {
        _library = library;
        _options = options;
        _interpreter = BuildInterpreter();
        _ruleSetVersionId = ruleSetVersionId;
        _parameterBinder = parameterBinder ?? new GovernedExpressionParameterBinder();
    }

    public PayRunResult Calculate(PayRunRequest request)
    {
        var context = BuildContext(request);
        var result = new PayRunResult
        {
            RuleSetVersionId = _ruleSetVersionId,
            LibraryId = _library.LibraryId,
            AwardCode = _library.AwardCode,
            AwardName = _library.AwardName,
            EffectiveFrom = _library.EffectiveFrom,
            EmployeeReference = request.EmployeeReference,
            PayPeriodReference = request.PayPeriodReference,
            CalculationTimestampUtc = _options.FixedCalculationTimestampUtc ?? DateTimeOffset.UtcNow
        };

        ResolveClassification(context, result);
        ApplyDefaults(context);
        ValidateParameters(context, result);

        foreach (var rule in OrderedRules())
            EvaluateRule(rule, context, result);

        ApplyToilSuppression(context, result);
        ApplySalaryReconciliation(context, result);
        ApplyExportBlocking(result);
        Finalise(context, result);
        return result;
    }

    private static Interpreter BuildInterpreter()
        => new Interpreter()
            .Reference(typeof(Math))
            .SetFunction("Max", (Func<decimal, decimal, decimal>)Math.Max)
            .SetFunction("Min", (Func<decimal, decimal, decimal>)Math.Min)
            .SetFunction("RoundMoney", (Func<decimal, decimal>)PayCalculationPolicy.RoundMoney)
            .SetFunction("RoundUpToQuarterHour", (Func<decimal, decimal>)PayCalculationPolicy.RoundUpToQuarterHour)
            .SetFunction("HasTag", (Func<string?, string?, bool>)((a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase)))
            .SetFunction("In", (Func<string?, string?, bool>)((value, csv) => (csv ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase))));

    private static Dictionary<string, object?> BuildContext(PayRunRequest request)
    {
        var context = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in request.Parameters) context[p.Key] = p.Value;
        foreach (var p in request.DerivedParameters) context[p.Key] = p.Value;
        return context;
    }

    private void ResolveClassification(Dictionary<string, object?> context, PayRunResult result)
    {
        var code = GetString(context, "ClassificationCode");
        var classification = _library.ReferenceData.Classifications.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (classification is null)
        {
            result.Warnings.Add(new RuleWarning { Severity = "error", RuleId = "CLASSIFICATION_NOT_FOUND", Message = $"Classification '{code}' not found.", BlocksPayrollExport = true });
            return;
        }

        result.ClassificationCode = classification.Code;
        result.ClassificationName = classification.Name;
        context.TryAdd("BaseRate", classification.Hourly);
        context["BaseRate"] = ToDecimal(context["BaseRate"]) <= 0m ? classification.Hourly : context["BaseRate"];
    }

    private void ApplyDefaults(Dictionary<string, object?> context)
    {
        context.TryAdd("BaseRate", 0m);
        context.TryAdd("AllPurposeAllowanceHourly", 0m);
        context.TryAdd("PenaltyBaseRate", ToDecimal(context["BaseRate"]) + ToDecimal(context["AllPurposeAllowanceHourly"]));
        context.TryAdd("StandardRateWeekly", _library.ReferenceData.Allowances.StandardRateWeekly);
        context.TryAdd("DailyExcessOvertimeHours", 0m);
        context.TryAdd("WeeklyExcessOvertimeHours", 0m);
        context.TryAdd("OutsideSpanOvertimeHours", 0m);
        context.TryAdd("PartTimePatternOvertimeHours", 0m);
        context.TryAdd("BrokenSpreadOvertimeHours", 0m);
        context.TryAdd("InsufficientRestOvertimeHours", 0m);
        context.TryAdd("WeekdayOvertimeHours", 0m);
        context.TryAdd("ToilAccrualHours", 0m);
        context.TryAdd("AwardReferenceGross", 0m);
        context.TryAdd("WeeklySalaryAmount", 0m);
        context.TryAdd("ShiftworkMultiplier", 1m);

        foreach (var rule in _library.Rules)
            context.TryAdd(rule.OutputKey, DefaultFor(rule.OutputType));
    }

    private void ValidateParameters(Dictionary<string, object?> context, PayRunResult result)
    {
        foreach (var p in _library.Parameters.Where(p => p.Required && !context.ContainsKey(p.Name)))
            result.Warnings.Add(new RuleWarning { Severity = "error", RuleId = "PARAMETER_REQUIRED", Message = $"Required parameter '{p.Name}' is missing.", BlocksPayrollExport = true });
    }

    private IEnumerable<RuleDefinition> OrderedRules()
    {
        var phases = _library.Orchestration.EvaluationOrder;
        return _library.Rules
            .OrderBy(r => PhaseIndex(phases, r.EvaluationPhase))
            .ThenBy(r => r.Precedence)
            .ThenBy(r => r.RuleId);
    }

    private static int PhaseIndex(List<string> phases, string phase)
    {
        var index = phases.FindIndex(p => p.Equals(phase, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : int.MaxValue;
    }

    private void EvaluateRule(RuleDefinition rule, Dictionary<string, object?> context, PayRunResult result)
    {
        var trace = new RuleTrace
        {
            SegmentId = GetString(context, "SegmentId") ?? "",
            RuleId = rule.RuleId,
            Version = rule.Version,
            Phase = rule.EvaluationPhase,
            Precedence = rule.Precedence,
            ClauseReference = rule.ClauseReference,
            Expression = rule.Expression,
            OutputKey = rule.OutputKey,
            Action = rule.Action
        };

        try
        {
            var parameters = _parameterBinder.Bind(_library, context);
            var value = _interpreter.Eval(rule.Expression, parameters);
            value = Normalise(value, rule.OutputType);
            context[rule.OutputKey] = value;

            trace.Value = value;
            CapturePayLineRounding(rule, value, trace);
            trace.Status = "evaluated";
            result.RuleTrace.Add(trace);
            Materialise(rule, value, context, result);
        }
        catch (Exception ex)
        {
            trace.Status = "failed";
            trace.Error = ex.Message;
            result.RuleTrace.Add(trace);
            result.Warnings.Add(new RuleWarning { SegmentId = trace.SegmentId, Severity = "error", RuleId = rule.RuleId, ClauseReference = rule.ClauseReference, Message = ex.Message, BlocksPayrollExport = true });
        }
    }

    private void Materialise(RuleDefinition rule, object? value, Dictionary<string, object?> context, PayRunResult result)
    {
        var action = rule.Action.ToLowerInvariant();
        switch (action)
        {
            case "warning":
            case "warning_or_overtime_mode":
            case "warning_and_penalty_uplift":
                if (ToBool(value)) AddWarning(rule, context, result, action);
                break;
            case "payroll_line":
            case "payroll_line_with_warning":
            case "manual_payroll_line":
                AddPayLine(rule, value, context, result, action);
                if ((action == "payroll_line_with_warning" || action == "manual_payroll_line") && ToDecimal(value) > 0)
                    AddWarning(rule, context, result, action);
                break;
            case "toil_ledger":
                AddToil(rule, value, context, result);
                break;
        }
    }

    private void AddWarning(RuleDefinition rule, Dictionary<string, object?> context, PayRunResult result, string action)
    {
        result.Warnings.Add(new RuleWarning
        {
            SegmentId = GetString(context, "SegmentId") ?? "",
            RuleId = rule.RuleId,
            ClauseReference = rule.ClauseReference,
            Severity = action == "warning" ? "warning" : "review",
            Message = rule.Description,
            ManualReviewPolicy = rule.ManualReviewPolicy,
            EvidenceRequirements = rule.EvidenceRequirements,
            BlocksPayrollExport = rule.ManualReviewPolicy.Contains("must", StringComparison.OrdinalIgnoreCase) || rule.ManualReviewPolicy.Contains("before_payroll_export", StringComparison.OrdinalIgnoreCase)
        });
    }

    private void AddPayLine(RuleDefinition rule, object? value, Dictionary<string, object?> context, PayRunResult result, string action)
    {
        var amount = PayCalculationPolicy.RoundMoney(ToDecimal(value));
        if (amount <= 0m) return;

        var category = _library.PayCategoryMapping.FirstOrDefault(m => m.OutputKey == rule.OutputKey)?.DefaultPayCategory ?? rule.OutputKey;
        var requiresReview = RequiresReview(rule, action, context);

        var line = new PayLine
        {
            SegmentId = GetString(context, "SegmentId") ?? "",
            RuleId = rule.RuleId,
            RuleVersion = rule.Version,
            OutputKey = rule.OutputKey,
            ClauseReference = rule.ClauseReference,
            PayCategory = category,
            Description = rule.Description,
            Formula = rule.Expression,
            StackingGroup = rule.StackingGroup,
            StackingPolicy = string.IsNullOrWhiteSpace(rule.StackingPolicy) ? "cumulative" : rule.StackingPolicy,
            Amount = amount,
            Exportable = !requiresReview,
            RequiresReview = requiresReview
        };

        result.AwardReferenceLines.Add(line);
        if (!requiresReview) result.PayrollLines.Add(line with { SourceBucket = "payroll_lines" });
    }

    private static bool RequiresReview(RuleDefinition rule, string action, Dictionary<string, object?> context)
    {
        if (action.Contains("manual", StringComparison.OrdinalIgnoreCase) || action.Contains("warning", StringComparison.OrdinalIgnoreCase))
            return true;

        if (rule.ManualReviewPolicy.Equals("review_if_public_holiday_substituted", StringComparison.OrdinalIgnoreCase))
            return (ToBool(context.GetValueOrDefault("IsSubstitutedPublicHoliday")) || HasTag(GetString(context, "ShiftTag"), "agreedSubstitutedPublicHoliday"))
                && !ToBool(context.GetValueOrDefault("HasPublicHolidayElectionEvidence"));

        return rule.ManualReviewPolicy.Contains("review", StringComparison.OrdinalIgnoreCase)
            || rule.ManualReviewPolicy.Contains("must", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddToil(RuleDefinition rule, object? value, Dictionary<string, object?> context, PayRunResult result)
    {
        if (rule.OutputKey == "ToilAccrualHours" && ToDecimal(value) > 0)
        {
            result.ToilMovements.Add(new ToilMovement
            {
                SegmentId = GetString(context, "SegmentId") ?? "",
                RuleId = rule.RuleId,
                ClauseReference = rule.ClauseReference,
                Type = "accrual",
                Hours = ToDecimal(value),
                Description = rule.Description,
                EvidenceRequired = rule.EvidenceRequirements
            });
        }

        if (rule.OutputKey == "ClosingToilBalanceHours")
            result.ClosingToilBalanceHours = ToDecimal(value);
    }

    private static void ApplyToilSuppression(Dictionary<string, object?> context, PayRunResult result)
    {
        if (!ToBool(context.GetValueOrDefault("ToilEligible"))) return;
        var ids = new HashSet<string> { "PAY_OVERTIME_FIRST_TWO_AMOUNT", "PAY_OVERTIME_AFTER_TWO_AMOUNT" };
        result.PayrollLines.RemoveAll(l => ids.Contains(l.RuleId));
    }

    private static void ApplySalaryReconciliation(Dictionary<string, object?> context, PayRunResult result)
    {
        if (!string.Equals(GetString(context, "EmploymentCategory"), "salaried", StringComparison.OrdinalIgnoreCase)) return;
        var salary = result.AwardReferenceLines.FirstOrDefault(l => l.OutputKey == "WeeklySalaryAmount");
        var topUp = result.AwardReferenceLines.FirstOrDefault(l => l.OutputKey == "SalaryTopUpAmount");
        result.PayrollLines.Clear();
        if (salary is not null) result.PayrollLines.Add(salary with { SourceBucket = "payroll_lines", Exportable = true, RequiresReview = false });
        if (topUp is not null && topUp.Amount > 0) result.PayrollLines.Add(topUp with { SourceBucket = "payroll_lines", Exportable = true, RequiresReview = true });
    }

    private void ApplyExportBlocking(PayRunResult result)
    {
        if (!_options.BlockPayrollExportOnErrors || !result.Warnings.Any(w => w.BlocksPayrollExport)) return;
        result.BlockedPayrollLines.AddRange(result.PayrollLines.Select(l => l with { SourceBucket = "blocked_payroll_lines", Exportable = false, RequiresReview = true }));
        result.PayrollLines.Clear();
    }

    private void Finalise(Dictionary<string, object?> context, PayRunResult result)
    {
        result.PayrollGross = PayCalculationPolicy.RoundMoney(result.PayrollLines.Sum(l => l.Amount));
        result.AwardReferenceGross = PayCalculationPolicy.RoundMoney(result.AwardReferenceLines.Sum(l => l.Amount));
        result.BlockedPayrollGross = PayCalculationPolicy.RoundMoney(result.BlockedPayrollLines.Sum(l => l.Amount));
        result.ToilAccruedHours = result.ToilMovements.Where(m => m.Type == "accrual").Sum(m => m.Hours);
        if (_options.IncludeFinalContext) result.FinalContext = context;
    }

    private static void CapturePayLineRounding(RuleDefinition rule, object? value, RuleTrace trace)
    {
        if (!IsPayLineAction(rule.Action) || !rule.OutputType.Equals("decimal", StringComparison.OrdinalIgnoreCase))
            return;

        var raw = ToDecimal(value);
        trace.RawValue = raw;
        trace.RoundedValue = PayCalculationPolicy.RoundMoney(raw);
    }

    private static bool IsPayLineAction(string action)
        => action.Equals("payroll_line", StringComparison.OrdinalIgnoreCase) ||
           action.Equals("payroll_line_with_warning", StringComparison.OrdinalIgnoreCase) ||
           action.Equals("manual_payroll_line", StringComparison.OrdinalIgnoreCase);

    private static object? Normalise(object? value, string type)
    {
        if (value is null) return null;
        return type.ToLowerInvariant() switch
        {
            "decimal" => ToDecimal(value),
            "bool" => ToBool(value),
            "string" => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
            _ => value
        };
    }

    private static decimal ToDecimal(object? value)
    {
        if (value is null) return 0m;
        if (value is decimal d) return d;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is double db) return Convert.ToDecimal(db);
        if (value is bool b) return b ? 1m : 0m;
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Number && je.TryGetDecimal(out var jd)) return jd;
        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    private static bool ToBool(object? value)
    {
        if (value is null) return false;
        if (value is bool b) return b;
        if (value is decimal d) return d != 0;
        if (value is JsonElement je && (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False)) return je.GetBoolean();
        return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
    }

    private static string? GetString(Dictionary<string, object?> context, string key)
        => context.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

    private static bool HasTag(string? actual, string expected)
        => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static object DefaultFor(string type)
        => type.Equals("bool", StringComparison.OrdinalIgnoreCase) ? false :
           type.Equals("string", StringComparison.OrdinalIgnoreCase) ? "" :
           0m;
}

public interface IExpressionParameterBinder
{
    Parameter[] Bind(GovernedExpressionLibrary library, IReadOnlyDictionary<string, object?> context);
}

public sealed class GovernedExpressionParameterBinder : IExpressionParameterBinder
{
    private static readonly HashSet<string> ReferenceParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "BaseRate",
        "AllPurposeAllowanceHourly",
        "PenaltyBaseRate",
        "StandardRateWeekly",
        "AwardReferenceGross",
        "WeeklySalaryAmount",
        "ShiftworkMultiplier"
    };

    public Parameter[] Bind(GovernedExpressionLibrary library, IReadOnlyDictionary<string, object?> context)
    {
        var allowedNames = new HashSet<string>(library.Parameters.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        allowedNames.UnionWith(ReferenceParameterNames);
        allowedNames.UnionWith(library.Rules.Select(r => r.OutputKey).Where(k => !string.IsNullOrWhiteSpace(k)));

        return context
            .Where(p => allowedNames.Contains(p.Key))
            .Select(p => new Parameter(p.Key, p.Value?.GetType() ?? typeof(object), p.Value))
            .ToArray();
    }
}
