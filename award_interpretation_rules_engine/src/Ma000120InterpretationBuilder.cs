namespace AwardInterpretationRulesEngine;

public static class Ma000120InterpretationBuilder
{
    public static AwardInterpretation BuildInterpretation(AwardSourceSnapshot snapshot, ParsedAwardDocument document)
    {
        var interpretation = new AwardInterpretation
        {
            AwardCode = "MA000120",
            AwardName = "Children's Services Award 2010",
            EffectiveFrom = "2026-03-01",
            Sources =
            [
                new SourceReference { SourceId = "FWC_ONLINE_AWARD", Url = snapshot.OnlineUrl, SourceType = "public_online_award_html", RetrievedAtUtc = snapshot.RetrievedAtUtc }
            ],
            Clauses = document.Clauses,
            Classifications = BuildClassifications(),
            Allowances = BuildAllowances(),
            StructuredRules = BuildStructuredSummaries()
        };
        interpretation.SourceRecords.Add(BuildSourceRecord(snapshot));

        if (!string.IsNullOrWhiteSpace(snapshot.ApiAwardJson))
            interpretation.Sources.Add(new SourceReference { SourceId = "FWC_MODERN_AWARDS_API_AWARD", Url = "configured_api_award_endpoint", SourceType = "subscription_api_json", RetrievedAtUtc = snapshot.RetrievedAtUtc });

        if (!string.IsNullOrWhiteSpace(snapshot.ApiRatesJson))
            interpretation.Sources.Add(new SourceReference { SourceId = "FWC_MODERN_AWARDS_API_RATES", Url = "configured_api_rates_endpoint", SourceType = "subscription_api_json", RetrievedAtUtc = snapshot.RetrievedAtUtc });

        interpretation.Warnings.AddRange(document.ParseWarnings);
        interpretation.Warnings.Add("Generated rules are deterministic templates and must be validated by payroll/legal before production use.");
        interpretation.Warnings.Add("Coverage, classification, agreement evidence and public holiday substitution require governed human approval.");

        return interpretation;
    }

    public static GovernedExpressionLibrary BuildLibrary(AwardInterpretation interpretation, ReviewGateResult gate)
    {
        if (!gate.Approved)
            throw new InvalidOperationException($"Cannot compile {interpretation.AwardCode}: review gate status is {gate.Status}.");

        return new GovernedExpressionLibrary
        {
            LibraryId = "MA000120_DYNAMIC_EXPRESSO_GOVERNED_LIBRARY",
            LibraryName = "Children's Services Award 2010 [MA000120] Governed Expression Library",
            SnapshotId = "MA000120-2026-03-01-ACCEPTANCE",
            SnapshotVersion = "1.0.0",
            AwardCode = interpretation.AwardCode,
            AwardName = interpretation.AwardName,
            EffectiveFrom = interpretation.EffectiveFrom,
            SourceRecordIds = interpretation.SourceRecords.Select(r => r.SourceRecordId).ToList(),
            SourceContentSha256 = interpretation.SourceRecords.Select(r => r.ContentSha256).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(),
            Approval = new RuleSnapshotApproval
            {
                GateStatus = gate.Status,
                ApprovalId = gate.ApprovalId,
                ApprovedBy = gate.ApprovedBy,
                ApprovedAtUtc = gate.ApprovedAtUtc
            },
            Orchestration = new Orchestration
            {
                EvaluationOrder =
                [
                    "PRECONDITIONS",
                    "NORMALISATION",
                    "ELIGIBILITY_FLAGS",
                    "QUANTITY_CALCULATION",
                    "PAY_CATEGORY_SELECTION",
                    "PAY_LINE_CALCULATION",
                    "ALLOWANCE_CALCULATION",
                    "TOIL_LEDGER",
                    "SALARY_RECONCILIATION",
                    "MANUAL_REVIEW",
                    "EXPORT"
                ]
            },
            ReferenceData = new ReferenceData
            {
                Classifications = interpretation.Classifications,
                Allowances = interpretation.Allowances
            },
            Parameters = BuildParameters(),
            Rules = BuildRules(),
            PayCategoryMapping = BuildPayCategoryMap()
        };
    }

    private static SourceRecord BuildSourceRecord(AwardSourceSnapshot snapshot)
    {
        var sourceRecordId = string.IsNullOrWhiteSpace(snapshot.SourceRecordId)
            ? $"{snapshot.AwardCode.ToUpperInvariant()}-TEMPLATE"
            : snapshot.SourceRecordId;

        return new SourceRecord
        {
            SourceRecordId = sourceRecordId,
            AwardCode = snapshot.AwardCode,
            SourceType = "public_award_html",
            SourceUri = snapshot.OnlineUrl,
            RetrievedAtUtc = snapshot.RetrievedAtUtc,
            ContentSha256 = snapshot.ContentSha256,
            RawPayloadRef = snapshot.OnlineUrl,
            ReviewStatus = "candidate",
            NormalizedRows = BuildNormalisedSourceRows(sourceRecordId)
        };
    }

    private static List<NormalisedSourceRow> BuildNormalisedSourceRows(string sourceRecordId) =>
    [
        SourceRow(sourceRecordId, "ORDINARY_TIME", "21", "ordinary_time", "Ordinary hours span and daily/weekly limits.", "ordinary_hours", "per_hour", ["weekday"], "ordinary time"),
        SourceRow(sourceRecordId, "OVERTIME", "23.2", "overtime", "Overtime for daily, weekly, outside span, part-time pattern and broken spread triggers.", "overtime", "per_hour", ["weekday"], "overtime"),
        SourceRow(sourceRecordId, "ALLOWANCES", "15", "allowances", "Broken shift, laundry, first aid, meal, vehicle, fares and educational leader allowances.", "allowance", "per_trigger", ["everyday"], "allowances", defaultedFields: ["days", "day_types"]),
        SourceRow(sourceRecordId, "PUBLIC_HOLIDAY", "27", "public_holiday", "Public holiday and agreed substitution treatment.", "public_holiday", "per_hour", ["public_holiday"], "public holiday", publicHoliday: true),
        SourceRow(sourceRecordId, "TOIL", "23.8", "toil", "Written agreement may convert eligible overtime to TOIL.", "toil", "per_overtime_hour", ["weekday"], "time off instead of payment"),
        SourceRow(sourceRecordId, "REST_FATIGUE", "22.3", "rest_fatigue", "Insufficient rest after overtime triggers review and overtime mode.", "insufficient_rest", "per_affected_hour", ["weekday"], "rest between work periods"),
        SourceRow(sourceRecordId, "LEAVE_LOADING", "25", "leave_loading", "Annual leave loading pays the higher applicable loading outcome.", "annual_leave_loading", "per_leave_hour", ["everyday"], "annual leave loading", defaultedFields: ["days", "day_types"]),
        SourceRow(sourceRecordId, "BLOCKED_EXPORTS", "Governance", "blocked_exports", "Manual-review and evidence failures block payroll export.", "manual_review_block", "per_blocked_line", ["everyday"], "review before payroll export", defaultedFields: ["days", "day_types"])
    ];

    private static NormalisedSourceRow SourceRow(
        string sourceRecordId,
        string suffix,
        string clause,
        string family,
        string text,
        string trigger,
        string basis,
        List<string> dayTypes,
        string evidenceText,
        bool publicHoliday = false,
        List<string>? defaultedFields = null)
    {
        List<string> dayValues = dayTypes.Contains("everyday")
            ? ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"]
            : dayTypes;

        return new NormalisedSourceRow
        {
            RowId = $"{sourceRecordId}-{suffix}",
            ClauseReference = clause,
            RuleFamily = family,
            SourceText = text,
            ParseStatus = defaultedFields is { Count: > 0 } ? "complete_defaulted" : "complete",
            Confidence = defaultedFields is { Count: > 0 } ? 0.95m : 1m,
            ConditionJson = new ConditionJson
            {
                EntityType = family == "allowances" ? "allowance" : "pay_rule",
                Days = new ConditionDays
                {
                    Values = dayValues,
                    Source = defaultedFields is { Count: > 0 } ? "domain_default" : "explicit_text"
                },
                DayTypes = dayTypes,
                PublicHoliday = publicHoliday,
                HourType = trigger,
                Trigger = trigger,
                Basis = basis,
                DefaultedFields = defaultedFields ?? [],
                Evidence =
                [
                    new ConditionEvidence { Field = "trigger", Text = evidenceText, Source = "ma000120_deterministic_template" },
                    new ConditionEvidence { Field = "clause", Text = clause, Source = "award_clause_reference" }
                ]
            }
        };
    }

    private static List<ClassificationRate> BuildClassifications() =>
    [
        new() { Code = "CSE_L1", Name = "CSE Level 1 - Introductory Educator", Weekly = 995.40m, Hourly = 26.19m, ClauseReference = "14.1(b)" },
        new() { Code = "CSE_L2", Name = "CSE Level 2 - Educator", Weekly = 1025.90m, Hourly = 27.00m, ClauseReference = "14.1(b)" },
        new() { Code = "CSE_L3", Name = "CSE Level 3 - Qualified Educator", Weekly = 1121.80m, Hourly = 29.52m, ClauseReference = "14.1(b)" },
        new() { Code = "CSE_L4", Name = "CSE Level 4 - Experienced Educator", Weekly = 1197.10m, Hourly = 31.50m, ClauseReference = "14.1(b)" },
        new() { Code = "CSE_L5", Name = "CSE Level 5 - Advanced Educator", Weekly = 1263.30m, Hourly = 33.24m, ClauseReference = "14.1(b)" },
        new() { Code = "CSE_L6", Name = "CSE Level 6 - Room Leader", Weekly = 1321.50m, Hourly = 34.78m, ClauseReference = "14.1(b)" },
        new() { Code = "CSE_L7", Name = "CSE Level 7 - Assistant Director", Weekly = 1381.90m, Hourly = 36.37m, ClauseReference = "14.1(b)" },
        new() { Code = "CSE_L8", Name = "CSE Level 8 - Director", Weekly = 1593.50m, Hourly = 41.93m, ClauseReference = "14.1(b)" },
        new() { Code = "SW_L1_1", Name = "Support Worker Level 1.1", Weekly = 948.00m, Hourly = 24.95m, ClauseReference = "14.2" },
        new() { Code = "SW_L2_1", Name = "Support Worker Level 2.1", Weekly = 977.00m, Hourly = 25.71m, ClauseReference = "14.2" },
        new() { Code = "SW_L2_2", Name = "Support Worker Level 2.2", Weekly = 1009.10m, Hourly = 26.56m, ClauseReference = "14.2" },
        new() { Code = "SW_L3_1", Name = "Support Worker Level 3.1", Weekly = 1068.40m, Hourly = 28.12m, ClauseReference = "14.2" }
    ];

    private static AllowanceReference BuildAllowances() => new()
    {
        StandardRateWeekly = 1121.80m,
        BrokenShiftPercentOfStandardRate = 0.0182m,
        LaundryRequiresIroningPerDay = 1.90m,
        LaundryNoIroningPerDay = 1.20m,
        ExcessFaresPerDay = 16.86m,
        MealAllowance = 15.48m,
        VehicleCarPerKm = 0.99m,
        VehicleMotorcyclePerKm = 0.33m,
        EducationalLeaderAnnual = 4567.31m
    };

    private static List<StructuredRuleSummary> BuildStructuredSummaries() =>
    [
        new() { RuleId = "ORD_WEEKDAY", ClauseReference = "21", Description = "Weekday ordinary hours and daily cap.", Output = "ordinary/overtime split", Governance = "auto with agreement evidence for 10-hour day" },
        new() { RuleId = "OT", ClauseReference = "23.2", Description = "Overtime first two hours and after two hours.", Output = "overtime pay lines", Governance = "auto unless TOIL applies" },
        new() { RuleId = "BREAKS", ClauseReference = "22", Description = "Meal break, paid onsite break, rest pauses and insufficient rest.", Output = "warnings and penalty uplift", Governance = "manual review where evidence is required" },
        new() { RuleId = "PH", ClauseReference = "27", Description = "Public holiday and substitution logic.", Output = "public holiday pay lines", Governance = "public holiday calendar and election evidence required" },
        new() { RuleId = "ALLOWANCES", ClauseReference = "15", Description = "Broken shift, first aid, laundry, meal, vehicle, excess fares and educational leader allowances.", Output = "allowance lines", Governance = "some allowances require manager attestation" }
    ];

    private static List<ParameterDefinition> BuildParameters()
    {
        string[] required =
        [
            "ClassificationCode", "EmploymentCategory", "EmploymentProfileCode", "DayType", "ResolvedDayType", "ShiftTag",
            "HasEvidenceReference", "WorkedHours", "RawShiftHours", "PaidHours", "WeeklyHoursBeforeShift",
            "ContractedWeeklyHours", "ShiftStartMinutes", "ShiftEndMinutes", "IsShiftworker", "IsPermanentNightShift",
            "IsLeave", "LeaveType", "LeaveHours", "LeavePenaltyMultiplier",
            "IsPublicHolidayFromCalendar", "IsActualPublicHoliday", "IsSubstitutedPublicHoliday", "HasPublicHolidayElectionEvidence",
            "UnpaidMealBreakMinutes", "PaidMealBreakMinutes", "MealBreakInterrupted", "RequiredToRemainOnPremises",
            "PaidRestPauseCount", "RestHoursSincePreviousShift", "BrokenShiftCount", "BrokenShiftSpreadHours",
            "WorkedHoursBeyondBrokenSpreadCap", "OutsideOrdinarySpanHours", "PartTimeOutsideRegularPatternHours",
            "MissedMealPenaltyHours", "HigherDutiesHours", "HigherDutiesRate", "OpeningToilBalanceHours",
            "ToilTakenHours", "ForceToilPayoutHours", "AnnualSalary", "VehicleKm", "VehicleType", "FirstAidRequired",
            "IsOSHC", "LaundryRequired", "LaundryRequiresIroning", "MealAllowanceRequired", "ExcessFaresRequired",
            "EducationalLeaderDaysPerWeek"
        ];

        return required.Select(name => new ParameterDefinition { Name = name, Required = true, Type = InferType(name) }).ToList();
    }

    private static string InferType(string name)
    {
        if (name.StartsWith("Is") || name.StartsWith("Has") || name.EndsWith("Required") || name.Contains("Interrupted") || name.Contains("Remain")) return "bool";
        if (name.Contains("Code") || name.Contains("Category") || name.Contains("Type") || name.Contains("Tag") || name.Contains("Profile")) return "string";
        return "decimal";
    }

    private static List<RuleDefinition> BuildRules()
    {
        var rules = new List<RuleDefinition>();

        void Rule(string id, string phase, decimal precedence, string clause, string desc, string expr, string key, string type, string action = "set_value", string review = "auto")
            => rules.Add(new RuleDefinition { RuleId = id, EvaluationPhase = phase, Precedence = precedence, ClauseReference = clause, Description = desc, Expression = expr, OutputKey = key, OutputType = type, Action = action, ManualReviewPolicy = review, EffectiveFrom = "2026-03-01" });

        Rule("PRE_EVIDENCE_REQUIRED_FOR_AGREEMENT_TAGS", "PRECONDITIONS", 1, "Evidence governance",
            "Agreement-based shift tags require evidence.",
            "ShiftTag != \"none\" && ShiftTag != \"permanentNightShift\" && !HasEvidenceReference",
            "RequiresEvidenceReview", "bool", "warning", "must_review_before_payroll_export");

        Rule("NORM_PENALTY_BASE_RATE", "NORMALISATION", 5, "23.2",
            "Base hourly rate plus all-purpose allowance.",
            "BaseRate + AllPurposeAllowanceHourly",
            "PenaltyBaseRate", "decimal");

        Rule("PH_RESOLVE_IS_PUBLIC_HOLIDAY", "NORMALISATION", 10, "27",
            "Resolve public holiday treatment after calendar and substitution tags.",
            "HasTag(ShiftTag, \"agreedSubstitutedPublicHoliday\") || IsSubstitutedPublicHoliday || ((IsActualPublicHoliday || IsPublicHolidayFromCalendar || DayType == \"public_holiday\") && !HasTag(ShiftTag, \"agreedNonPublicHoliday\"))",
            "ResolvedIsPublicHoliday", "bool");

        Rule("PH_RESOLVE_DAY_TYPE", "NORMALISATION", 11, "27",
            "Map public holiday flag to resolved day type.",
            "ResolvedIsPublicHoliday ? \"public_holiday\" : DayType",
            "ResolvedDayType", "string");

        Rule("MIN_WEEKEND_PUBLIC_HOLIDAY_4H", "QUANTITY_CALCULATION", 20, "23.5",
            "Four-hour minimum payment for Saturday, Sunday or public holiday.",
            "(ResolvedDayType == \"saturday\" || ResolvedDayType == \"sunday\" || ResolvedDayType == \"public_holiday\") ? Max(PaidHours, 4m) : PaidHours",
            "PaidHours", "decimal");

        Rule("MIN_WEEKDAY_PART_TIME_CASUAL_2H", "QUANTITY_CALCULATION", 21, "10, 23.5",
            "Two-hour weekday minimum engagement for part-time/casual.",
            "(ResolvedDayType == \"weekday\" && (EmploymentCategory == \"part_time\" || EmploymentCategory == \"casual\")) ? Max(PaidHours, 2m) : PaidHours",
            "PaidHours", "decimal");

        Rule("OT_DAILY_EXCESS_HOURS", "QUANTITY_CALCULATION", 40, "21.3, 23.2",
            "Daily overtime excess over 8 hours or 10 hours by agreement.",
            "!IsLeave ? Max(0m, PaidHours - (HasTag(ShiftTag, \"agreed10HourDay\") ? 10m : 8m)) : 0m",
            "DailyExcessOvertimeHours", "decimal");

        Rule("OT_WEEKLY_EXCESS_HOURS", "QUANTITY_CALCULATION", 41, "21, 23.2",
            "Weekly overtime excess over contracted weekly hours.",
            "!IsLeave ? Max(0m, (WeeklyHoursBeforeShift + PaidHours) - ContractedWeeklyHours) : 0m",
            "WeeklyExcessOvertimeHours", "decimal");

        Rule("OT_OUTSIDE_SPAN_HOURS", "QUANTITY_CALCULATION", 42, "21.3, 23.2",
            "Outside ordinary span hours for weekday day workers.",
            "(!IsLeave && !IsShiftworker && ResolvedDayType == \"weekday\") ? OutsideOrdinarySpanHours : 0m",
            "OutsideSpanOvertimeHours", "decimal");

        Rule("OT_PART_TIME_PATTERN_EXCESS", "QUANTITY_CALCULATION", 43, "10.4, 23.2",
            "Part-time outside regular pattern hours unless agreed additional normal hours tag applies.",
            "(!IsLeave && EmploymentCategory == \"part_time\" && !HasTag(ShiftTag, \"agreedAdditionalNormalHours\")) ? PartTimeOutsideRegularPatternHours : 0m",
            "PartTimePatternOvertimeHours", "decimal", "set_value", "requires_agreement_evidence_for_suppression");

        Rule("OT_INSUFFICIENT_REST_TRIGGER", "ELIGIBILITY_FLAGS", 44, "22.3",
            "Insufficient rest between work periods.",
            "!IsLeave && RestHoursSincePreviousShift < (HasTag(ShiftTag, \"agreed8HourBreak\") || EmploymentProfileCode == \"FULL_TIME_8H_BREAK\" || EmploymentProfileCode == \"PART_TIME_8H_BREAK\" ? 8m : 10m)",
            "InsufficientRestTriggered", "bool", "warning_or_overtime_mode", "review_release_from_duty_requirement");

        Rule("OT_INSUFFICIENT_REST_HOURS", "QUANTITY_CALCULATION", 44.1m, "22.3",
            "Affected work period hours if insufficient rest is treated as overtime.",
            "InsufficientRestTriggered ? PaidHours : 0m",
            "InsufficientRestOvertimeHours", "decimal");

        Rule("OT_BROKEN_SHIFT_SPREAD_EXCESS", "QUANTITY_CALCULATION", 45, "21.5, 23.2",
            "Overtime hours beyond 12-hour broken-shift spread for day workers.",
            "(!IsLeave && BrokenShiftCount > 1m && BrokenShiftSpreadHours > 12m && !IsShiftworker) ? WorkedHoursBeyondBrokenSpreadCap : 0m",
            "BrokenSpreadOvertimeHours", "decimal");

        Rule("OT_COMPOSITE_WEEKDAY_OVERTIME_HOURS", "QUANTITY_CALCULATION", 49, "21, 22.3, 23.2",
            "Final weekday overtime hours are the maximum applicable overtime trigger, capped at paid hours.",
            "Min(PaidHours, Max(Max(Max(Max(Max(DailyExcessOvertimeHours, WeeklyExcessOvertimeHours), OutsideSpanOvertimeHours), PartTimePatternOvertimeHours), BrokenSpreadOvertimeHours), InsufficientRestOvertimeHours))",
            "WeekdayOvertimeHours", "decimal");

        Rule("PAY_ORDINARY_HOURS", "PAY_LINE_CALCULATION", 60, "14, 21",
            "Ordinary weekday pay amount.",
            "!IsLeave && !IsShiftworker && ResolvedDayType == \"weekday\" ? Max(0m, PaidHours - WeekdayOvertimeHours) * PenaltyBaseRate * (EmploymentCategory == \"casual\" ? 1.25m : 1.0m) : 0m",
            "OrdinaryPayAmount", "decimal", "payroll_line");

        Rule("PAY_SHIFTWORK_MULTIPLIER", "PAY_CATEGORY_SELECTION", 61, "23.4",
            "Select shiftwork multiplier.",
            "(IsPermanentNightShift || HasTag(ShiftTag, \"permanentNightShift\")) ? 1.30m : (IsShiftworker && ShiftStartMinutes >= 300m && ShiftStartMinutes < 360m ? 1.10m : (IsShiftworker && ShiftEndMinutes > 1110m && ShiftEndMinutes <= 1440m ? 1.15m : (IsShiftworker && (ShiftEndMinutes > 1440m || ShiftEndMinutes <= 480m) ? 1.175m : 1.0m)))",
            "ShiftworkMultiplier", "decimal");

        Rule("PAY_SHIFTWORK_AMOUNT", "PAY_LINE_CALCULATION", 61.1m, "23.4",
            "Ordinary shiftworker amount.",
            "!IsLeave && ResolvedDayType == \"weekday\" && IsShiftworker ? Max(0m, PaidHours - WeekdayOvertimeHours) * PenaltyBaseRate * ShiftworkMultiplier : 0m",
            "ShiftworkOrdinaryAmount", "decimal", "payroll_line");

        Rule("PAY_OVERTIME_FIRST_TWO_MULTIPLIER", "PAY_CATEGORY_SELECTION", 62, "23.2",
            "Overtime multiplier for first two hours.",
            "EmploymentCategory == \"casual\" ? 1.75m : 1.5m",
            "OvertimeFirstTwoMultiplier", "decimal");

        Rule("PAY_OVERTIME_AFTER_TWO_MULTIPLIER", "PAY_CATEGORY_SELECTION", 63, "23.2",
            "Overtime multiplier after two hours.",
            "EmploymentCategory == \"casual\" ? 2.25m : 2.0m",
            "OvertimeAfterTwoMultiplier", "decimal");

        Rule("PAY_OVERTIME_FIRST_TWO_AMOUNT", "PAY_LINE_CALCULATION", 64, "23.2",
            "Amount for first two overtime hours.",
            "Min(WeekdayOvertimeHours, 2m) * PenaltyBaseRate * OvertimeFirstTwoMultiplier",
            "OvertimeFirstTwoAmount", "decimal", "payroll_line");

        Rule("PAY_OVERTIME_AFTER_TWO_AMOUNT", "PAY_LINE_CALCULATION", 65, "23.2",
            "Amount for overtime after first two hours.",
            "Max(0m, WeekdayOvertimeHours - 2m) * PenaltyBaseRate * OvertimeAfterTwoMultiplier",
            "OvertimeAfterTwoAmount", "decimal", "payroll_line");

        Rule("PAY_SATURDAY_SHIFTWORKER_AMOUNT", "PAY_LINE_CALCULATION", 70, "23.5",
            "Saturday shiftworker ordinary amount.",
            "!IsLeave && ResolvedDayType == \"saturday\" && IsShiftworker ? PaidHours * PenaltyBaseRate * 1.5m : 0m",
            "SaturdayShiftworkerAmount", "decimal", "payroll_line");

        Rule("PAY_SUNDAY_AMOUNT", "PAY_LINE_CALCULATION", 71, "23.5",
            "Sunday work at 200%.",
            "!IsLeave && ResolvedDayType == \"sunday\" ? PaidHours * PenaltyBaseRate * 2.0m : 0m",
            "SundayAmount", "decimal", "payroll_line");

        Rule("PAY_PUBLIC_HOLIDAY_AMOUNT", "PAY_LINE_CALCULATION", 72, "23.5, 27",
            "Public holiday work at 250%.",
            "!IsLeave && ResolvedDayType == \"public_holiday\" ? PaidHours * PenaltyBaseRate * 2.5m : 0m",
            "PublicHolidayAmount", "decimal", "payroll_line", "review_if_public_holiday_substituted");

        Rule("BREAK_MISSED_MEAL_TRIGGER", "ELIGIBILITY_FLAGS", 80, "22.1",
            "Shift over five hours without qualifying meal break.",
            "!IsLeave && RawShiftHours > 5m && UnpaidMealBreakMinutes < 30m && !RequiredToRemainOnPremises && !HasTag(ShiftTag, \"mealBreakAgreement6HourShift\")",
            "MissedMealBreakTriggered", "bool", "warning_and_penalty_uplift", "review_evidence_and_rounding");

        Rule("PAY_MISSED_MEAL_UPLIFT_FIRST_TWO", "PAY_LINE_CALCULATION", 85, "22.1, 23.2",
            "Missed meal-break overtime uplift.",
            "MissedMealBreakTriggered ? Min(MissedMealPenaltyHours, 2m) * PenaltyBaseRate * ((EmploymentCategory == \"casual\" ? 1.75m : 1.5m) - (EmploymentCategory == \"casual\" ? 1.25m : 1.0m)) : 0m",
            "MissedMealUpliftFirstTwoAmount", "decimal", "payroll_line_with_warning", "review_evidence_and_rounding");

        Rule("HD_HIGHER_DUTIES_ELIGIBLE", "ELIGIBILITY_FLAGS", 90, "18",
            "Higher duties eligibility.",
            "!IsLeave && HigherDutiesHours >= 2m && HigherDutiesRate > BaseRate",
            "HigherDutiesEligible", "bool", "set_value", "review_duties_and_classification_evidence");

        Rule("PAY_HIGHER_DUTIES_UPLIFT", "PAY_LINE_CALCULATION", 91, "18",
            "Higher duties uplift.",
            "HigherDutiesEligible ? HigherDutiesHours * Max(0m, HigherDutiesRate - BaseRate) : 0m",
            "HigherDutiesUpliftAmount", "decimal", "payroll_line", "review_duties_and_classification_evidence");

        Rule("ALLOW_BROKEN_SHIFT_AMOUNT", "ALLOWANCE_CALCULATION", 100, "15.2",
            "Broken shift allowance.",
            "!IsLeave && BrokenShiftCount > 1m ? StandardRateWeekly * 0.0182m : 0m",
            "BrokenShiftAllowanceAmount", "decimal", "payroll_line");

        Rule("ALLOW_LAUNDRY_AMOUNT", "ALLOWANCE_CALCULATION", 101, "15.3",
            "Laundry allowance.",
            "!IsLeave && LaundryRequired ? (LaundryRequiresIroning ? 1.90m : 1.20m) : 0m",
            "LaundryAllowanceAmount", "decimal", "payroll_line");

        Rule("ALLOW_FIRST_AID_AMOUNT", "ALLOWANCE_CALCULATION", 102, "15.5",
            "First aid allowance.",
            "!IsLeave && FirstAidRequired ? (IsOSHC ? WorkedHours * StandardRateWeekly * 0.0014m : StandardRateWeekly * 0.0108m) : 0m",
            "FirstAidAllowanceAmount", "decimal", "payroll_line");

        Rule("ALLOW_MEAL_AMOUNT", "ALLOWANCE_CALCULATION", 103, "15.6",
            "Meal allowance.",
            "!IsLeave && MealAllowanceRequired ? 15.48m : 0m",
            "MealAllowanceAmount", "decimal", "manual_payroll_line", "must_be_manager_attested");

        Rule("ALLOW_EXCESS_FARES_AMOUNT", "ALLOWANCE_CALCULATION", 104, "15.4",
            "Excess fares allowance.",
            "!IsLeave && ExcessFaresRequired ? 16.86m : 0m",
            "ExcessFaresAllowanceAmount", "decimal", "manual_payroll_line", "must_be_manager_attested");

        Rule("ALLOW_VEHICLE_AMOUNT", "ALLOWANCE_CALCULATION", 105, "15.7",
            "Vehicle allowance.",
            "!IsLeave && VehicleType == \"car\" ? VehicleKm * 0.99m : (!IsLeave && VehicleType == \"motorcycle\" ? VehicleKm * 0.33m : 0m)",
            "VehicleAllowanceAmount", "decimal", "manual_payroll_line", "must_be_manager_attested");

        Rule("ALLOW_EDUCATIONAL_LEADER_AMOUNT", "ALLOWANCE_CALCULATION", 106, "15.8",
            "Educational leader allowance weekly amount.",
            "!IsLeave && EducationalLeaderDaysPerWeek > 0m ? (4567.31m * EducationalLeaderDaysPerWeek / 5m / 52m) : 0m",
            "EducationalLeaderAllowanceWeeklyAmount", "decimal", "payroll_line", "must_have_regulation_118_assignment");

        Rule("TOIL_ELIGIBLE", "TOIL_LEDGER", 120, "23.8",
            "Eligible overtime may convert to TOIL.",
            "HasTag(ShiftTag, \"toil\") && HasEvidenceReference && WeekdayOvertimeHours > 0m",
            "ToilEligible", "bool", "toil_ledger", "must_have_separate_written_agreement");

        Rule("TOIL_ACCRUAL_HOURS", "TOIL_LEDGER", 121, "23.8",
            "TOIL accrues one hour per overtime hour.",
            "ToilEligible ? WeekdayOvertimeHours : 0m",
            "ToilAccrualHours", "decimal", "toil_ledger", "must_have_separate_written_agreement");

        Rule("TOIL_CLOSING_BALANCE", "TOIL_LEDGER", 122, "23.8",
            "Closing TOIL balance.",
            "OpeningToilBalanceHours + ToilAccrualHours - ToilTakenHours - ForceToilPayoutHours",
            "ClosingToilBalanceHours", "decimal", "toil_ledger");

        Rule("LEAVE_ANNUAL_BASE_AMOUNT", "PAY_LINE_CALCULATION", 125, "25",
            "Annual leave base pay amount.",
            "IsLeave && LeaveType == \"annual\" ? LeaveHours * PenaltyBaseRate : 0m",
            "AnnualLeaveBaseAmount", "decimal", "payroll_line");

        Rule("LEAVE_ANNUAL_LOADING_AMOUNT", "PAY_LINE_CALCULATION", 126, "25",
            "Annual leave loading higher-of amount.",
            "IsLeave && LeaveType == \"annual\" ? LeaveHours * PenaltyBaseRate * Max(0.175m, LeavePenaltyMultiplier - 1m) : 0m",
            "AnnualLeaveLoadingAmount", "decimal", "payroll_line");

        Rule("SAL_WEEKLY_SALARY_AMOUNT", "SALARY_RECONCILIATION", 130, "Governance overlay",
            "Weekly salary amount.",
            "EmploymentCategory == \"salaried\" ? AnnualSalary / 52m : 0m",
            "WeeklySalaryAmount", "decimal", "payroll_line", "salary_reconciliation_required");

        Rule("SAL_AWARD_TOP_UP_AMOUNT", "SALARY_RECONCILIATION", 131, "Governance overlay",
            "Award salary top-up amount.",
            "EmploymentCategory == \"salaried\" ? Max(0m, AwardReferenceGross - WeeklySalaryAmount) : 0m",
            "SalaryTopUpAmount", "decimal", "payroll_line", "salary_reconciliation_required");

        return rules;
    }

    private static List<PayCategoryMap> BuildPayCategoryMap() =>
    [
        new() { OutputKey = "OrdinaryPayAmount", DefaultPayCategory = "MA000120 Ordinary" },
        new() { OutputKey = "ShiftworkOrdinaryAmount", DefaultPayCategory = "MA000120 Shiftwork Ordinary" },
        new() { OutputKey = "OvertimeFirstTwoAmount", DefaultPayCategory = "MA000120 Overtime 150/175" },
        new() { OutputKey = "OvertimeAfterTwoAmount", DefaultPayCategory = "MA000120 Overtime 200/225" },
        new() { OutputKey = "SaturdayShiftworkerAmount", DefaultPayCategory = "MA000120 Saturday Shiftworker" },
        new() { OutputKey = "SundayAmount", DefaultPayCategory = "MA000120 Sunday" },
        new() { OutputKey = "PublicHolidayAmount", DefaultPayCategory = "MA000120 Public Holiday" },
        new() { OutputKey = "MissedMealUpliftFirstTwoAmount", DefaultPayCategory = "MA000120 Missed Meal Break Uplift" },
        new() { OutputKey = "HigherDutiesUpliftAmount", DefaultPayCategory = "MA000120 Higher Duties Uplift" },
        new() { OutputKey = "BrokenShiftAllowanceAmount", DefaultPayCategory = "MA000120 Broken Shift Allowance" },
        new() { OutputKey = "LaundryAllowanceAmount", DefaultPayCategory = "MA000120 Laundry Allowance" },
        new() { OutputKey = "FirstAidAllowanceAmount", DefaultPayCategory = "MA000120 First Aid Allowance" },
        new() { OutputKey = "MealAllowanceAmount", DefaultPayCategory = "MA000120 Meal Allowance" },
        new() { OutputKey = "ExcessFaresAllowanceAmount", DefaultPayCategory = "MA000120 Excess Fares" },
        new() { OutputKey = "VehicleAllowanceAmount", DefaultPayCategory = "MA000120 Vehicle Allowance" },
        new() { OutputKey = "EducationalLeaderAllowanceWeeklyAmount", DefaultPayCategory = "MA000120 Educational Leader Allowance" },
        new() { OutputKey = "AnnualLeaveBaseAmount", DefaultPayCategory = "MA000120 Annual Leave Base" },
        new() { OutputKey = "AnnualLeaveLoadingAmount", DefaultPayCategory = "MA000120 Annual Leave Loading" },
        new() { OutputKey = "WeeklySalaryAmount", DefaultPayCategory = "Weekly Salary" },
        new() { OutputKey = "SalaryTopUpAmount", DefaultPayCategory = "MA000120 Salary Reconciliation Top-Up" }
    ];
}
