namespace AwardInterpretationRulesEngine;

public static class RuleReviewGate
{
    private static readonly string[] RequiredRuleFamilies =
    [
        "ordinary_time",
        "overtime",
        "allowances",
        "public_holiday",
        "toil",
        "rest_fatigue",
        "leave_loading",
        "blocked_exports"
    ];

    public static ReviewGateResult Evaluate(AwardInterpretation interpretation)
    {
        var result = new ReviewGateResult
        {
            ApprovalId = $"{interpretation.AwardCode}-ACCEPTANCE-GATE-2026-03-01",
            ApprovedBy = "governed_ma000120_acceptance_baseline",
            ApprovedAtUtc = interpretation.Sources.FirstOrDefault()?.RetrievedAtUtc
        };

        AddCheck(result, interpretation.AwardCode.Equals("MA000120", StringComparison.OrdinalIgnoreCase), "award_code_is_ma000120");
        AddCheck(result, interpretation.Classifications.Count > 0, "classification_rates_present");
        AddCheck(result, interpretation.Allowances.StandardRateWeekly > 0m, "allowance_reference_rates_present");
        AddCheck(result, interpretation.Clauses.Count > 0, "parsed_award_clauses_present");

        foreach (var clause in new[] { "14", "15", "21", "22", "23", "25", "27" })
            AddCheck(result, interpretation.Clauses.Any(c => c.ClauseNumber == clause), $"parsed_clause_{clause}_present");

        var rows = interpretation.SourceRecords.SelectMany(r => r.NormalizedRows).ToList();
        AddCheck(result, rows.Count > 0, "normalized_source_rows_present");

        foreach (var family in RequiredRuleFamilies)
            AddCheck(result, rows.Any(r => r.RuleFamily == family), $"source_family_{family}_present");

        foreach (var row in rows)
        {
            AddCheck(result, !string.IsNullOrWhiteSpace(row.ConditionJson.SchemaVersion), $"{row.RowId}_condition_schema_present");
            AddCheck(result, !string.IsNullOrWhiteSpace(row.ConditionJson.Trigger), $"{row.RowId}_condition_trigger_present");
            AddCheck(result, !string.IsNullOrWhiteSpace(row.ClauseReference), $"{row.RowId}_clause_reference_present");
        }

        result.Approved = result.Errors.Count == 0;
        result.Status = result.Approved ? "approved_for_compilation" : "blocked_from_compilation";
        return result;
    }

    private static void AddCheck(ReviewGateResult result, bool passed, string check)
    {
        if (passed)
        {
            result.Checks.Add(check);
            return;
        }

        result.Errors.Add(check);
    }
}
