namespace AwardInterpretationRulesEngine;

public sealed class TimesheetNormaliser
{
    private readonly GovernedExpressionLibrary _library;

    public TimesheetNormaliser(GovernedExpressionLibrary library)
    {
        _library = library;
    }

    public List<PayRunRequest> BuildSegmentRequests(PayRunInput input)
    {
        var requests = new List<PayRunRequest>();
        var weeklyHoursBefore = 0m;
        DateTime? previousShiftEnd = null;
        var segmentIndex = 0;

        foreach (var day in input.Days.OrderBy(d => d.Date))
        {
            if (day.LeaveHours > 0m)
            {
                segmentIndex++;
                requests.Add(BuildLeaveRequest(input, day, segmentIndex, weeklyHoursBefore));
                weeklyHoursBefore += day.LeaveHours;
            }

            var analysedShifts = day.Shifts.Select(s => AnalyseShift(day, s)).OrderBy(s => s.StartDateTime).ToList();

            var brokenShiftCount = analysedShifts.Count;
            var brokenSpreadHours = brokenShiftCount > 1
                ? (decimal)(analysedShifts.Last().EndDateTime - analysedShifts.First().StartDateTime).TotalHours
                : 0m;

            foreach (var analysed in analysedShifts)
            {
                segmentIndex++;
                var restHours = previousShiftEnd is null ? 999m : (decimal)(analysed.StartDateTime - previousShiftEnd.Value).TotalHours;
                previousShiftEnd = analysed.EndDateTime;

                var paidHours = analysed.WorkedHours;
                var outsideSpanHours = CalculateOutsideOrdinarySpanHours(day.DayType, analysed);
                var hoursBeyondBrokenSpreadCap = CalculateHoursBeyondSpreadCap(analysed, analysedShifts);
                var higherDutiesRate = ResolveHigherDutiesRate(input, analysed.Shift);

                var request = new PayRunRequest
                {
                    EmployeeReference = input.EmployeeReference,
                    PayPeriodReference = input.PayPeriodReference,
                    Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["SegmentId"] = $"SEG-{segmentIndex:000}",
                        ["ClassificationCode"] = input.Employee.ClassificationCode,
                        ["EmploymentCategory"] = input.Employee.EmploymentCategory,
                        ["EmploymentProfileCode"] = input.Employee.EmploymentProfileCode,
                        ["DayType"] = day.DayType,
                        ["ResolvedDayType"] = day.DayType,
                        ["ShiftTag"] = analysed.Shift.Tag,
                        ["HasEvidenceReference"] = !string.IsNullOrWhiteSpace(analysed.Shift.EvidenceReference),
                        ["WorkedHours"] = analysed.WorkedHours,
                        ["RawShiftHours"] = analysed.RawShiftHours,
                        ["PaidHours"] = paidHours,
                        ["WeeklyHoursBeforeShift"] = weeklyHoursBefore,
                        ["ContractedWeeklyHours"] = input.Employee.ContractedWeeklyHours,
                        ["ShiftStartMinutes"] = analysed.StartMinutes,
                        ["ShiftEndMinutes"] = analysed.EndMinutes,
                        ["IsShiftworker"] = input.Employee.EmploymentProfileCode.Contains("SHIFT", StringComparison.OrdinalIgnoreCase) || input.Employee.EmploymentProfileCode.Contains("NIGHT", StringComparison.OrdinalIgnoreCase),
                        ["IsPermanentNightShift"] = input.Employee.EmploymentProfileCode.Contains("PERM_NIGHT", StringComparison.OrdinalIgnoreCase) || analysed.Shift.Tag == "permanentNightShift",
                        ["IsLeave"] = false,
                        ["LeaveType"] = "",
                        ["LeaveHours"] = 0m,
                        ["LeavePenaltyMultiplier"] = 1m,
                        ["IsPublicHolidayFromCalendar"] = false,
                        ["IsActualPublicHoliday"] = day.ActualPublicHoliday,
                        ["IsSubstitutedPublicHoliday"] = day.SubstitutedPublicHoliday,
                        ["HasPublicHolidayElectionEvidence"] = !string.IsNullOrWhiteSpace(day.PublicHolidayElectionEvidence),
                        ["UnpaidMealBreakMinutes"] = analysed.UnpaidMealBreakMinutes,
                        ["PaidMealBreakMinutes"] = analysed.PaidMealBreakMinutes,
                        ["MealBreakInterrupted"] = analysed.MealBreakInterrupted,
                        ["RequiredToRemainOnPremises"] = analysed.RequiredToRemainOnPremises,
                        ["PaidRestPauseCount"] = analysed.PaidRestPauseCount,
                        ["RestHoursSincePreviousShift"] = restHours,
                        ["BrokenShiftCount"] = brokenShiftCount,
                        ["BrokenShiftSpreadHours"] = brokenSpreadHours,
                        ["WorkedHoursBeyondBrokenSpreadCap"] = hoursBeyondBrokenSpreadCap,
                        ["OutsideOrdinarySpanHours"] = outsideSpanHours,
                        ["PartTimeOutsideRegularPatternHours"] = CalculatePartTimeOutsideRegularPatternHours(day, analysed),
                        ["MissedMealPenaltyHours"] = CalculateMissedMealPenaltyHours(analysed),
                        ["HigherDutiesHours"] = analysed.Shift.HigherDutiesEnabled ? analysed.WorkedHours : 0m,
                        ["HigherDutiesRate"] = higherDutiesRate,
                        ["OpeningToilBalanceHours"] = input.Employee.OpeningToilBalanceHours,
                        ["ToilTakenHours"] = 0m,
                        ["ForceToilPayoutHours"] = 0m,
                        ["AnnualSalary"] = input.Employee.AnnualSalary,
                        ["VehicleKm"] = input.Allowances.VehicleKm,
                        ["VehicleType"] = input.Allowances.VehicleType,
                        ["FirstAidRequired"] = input.Allowances.FirstAidRequired,
                        ["IsOSHC"] = input.Allowances.IsOshc,
                        ["LaundryRequired"] = input.Allowances.LaundryRequired,
                        ["LaundryRequiresIroning"] = input.Allowances.LaundryRequiresIroning,
                        ["MealAllowanceRequired"] = input.Allowances.MealAllowanceRequired,
                        ["ExcessFaresRequired"] = input.Allowances.ExcessFaresRequired,
                        ["EducationalLeaderDaysPerWeek"] = input.Allowances.EducationalLeaderDaysPerWeek,
                        ["AllPurposeAllowanceHourly"] = input.Employee.AllPurposeAllowanceHourly
                    }
                };

                requests.Add(request);
                weeklyHoursBefore += paidHours;
            }
        }

        return requests;
    }

    private static PayRunRequest BuildLeaveRequest(PayRunInput input, PayRunDay day, int segmentIndex, decimal weeklyHoursBefore)
    {
        return new PayRunRequest
        {
            EmployeeReference = input.EmployeeReference,
            PayPeriodReference = input.PayPeriodReference,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["SegmentId"] = $"SEG-{segmentIndex:000}",
                ["ClassificationCode"] = input.Employee.ClassificationCode,
                ["EmploymentCategory"] = input.Employee.EmploymentCategory,
                ["EmploymentProfileCode"] = input.Employee.EmploymentProfileCode,
                ["DayType"] = day.DayType,
                ["ResolvedDayType"] = day.DayType,
                ["ShiftTag"] = "leave",
                ["HasEvidenceReference"] = true,
                ["WorkedHours"] = 0m,
                ["RawShiftHours"] = 0m,
                ["PaidHours"] = day.LeaveHours,
                ["WeeklyHoursBeforeShift"] = weeklyHoursBefore,
                ["ContractedWeeklyHours"] = input.Employee.ContractedWeeklyHours,
                ["ShiftStartMinutes"] = 0m,
                ["ShiftEndMinutes"] = 0m,
                ["IsShiftworker"] = input.Employee.EmploymentProfileCode.Contains("SHIFT", StringComparison.OrdinalIgnoreCase) || input.Employee.EmploymentProfileCode.Contains("NIGHT", StringComparison.OrdinalIgnoreCase),
                ["IsPermanentNightShift"] = input.Employee.EmploymentProfileCode.Contains("PERM_NIGHT", StringComparison.OrdinalIgnoreCase),
                ["IsLeave"] = true,
                ["LeaveType"] = day.LeaveType,
                ["LeaveHours"] = day.LeaveHours,
                ["LeavePenaltyMultiplier"] = day.LeavePenaltyMultiplier,
                ["IsPublicHolidayFromCalendar"] = false,
                ["IsActualPublicHoliday"] = day.ActualPublicHoliday,
                ["IsSubstitutedPublicHoliday"] = day.SubstitutedPublicHoliday,
                ["HasPublicHolidayElectionEvidence"] = !string.IsNullOrWhiteSpace(day.PublicHolidayElectionEvidence),
                ["UnpaidMealBreakMinutes"] = 0m,
                ["PaidMealBreakMinutes"] = 0m,
                ["MealBreakInterrupted"] = false,
                ["RequiredToRemainOnPremises"] = false,
                ["PaidRestPauseCount"] = 0m,
                ["RestHoursSincePreviousShift"] = 999m,
                ["BrokenShiftCount"] = 0m,
                ["BrokenShiftSpreadHours"] = 0m,
                ["WorkedHoursBeyondBrokenSpreadCap"] = 0m,
                ["OutsideOrdinarySpanHours"] = 0m,
                ["PartTimeOutsideRegularPatternHours"] = 0m,
                ["MissedMealPenaltyHours"] = 0m,
                ["HigherDutiesHours"] = 0m,
                ["HigherDutiesRate"] = 0m,
                ["OpeningToilBalanceHours"] = input.Employee.OpeningToilBalanceHours,
                ["ToilTakenHours"] = 0m,
                ["ForceToilPayoutHours"] = 0m,
                ["AnnualSalary"] = input.Employee.AnnualSalary,
                ["VehicleKm"] = 0m,
                ["VehicleType"] = "none",
                ["FirstAidRequired"] = false,
                ["IsOSHC"] = false,
                ["LaundryRequired"] = false,
                ["LaundryRequiresIroning"] = false,
                ["MealAllowanceRequired"] = false,
                ["ExcessFaresRequired"] = false,
                ["EducationalLeaderDaysPerWeek"] = 0m,
                ["AllPurposeAllowanceHourly"] = input.Employee.AllPurposeAllowanceHourly
            }
        };
    }

    private decimal ResolveHigherDutiesRate(PayRunInput input, PayRunShift shift)
    {
        if (!shift.HigherDutiesEnabled || string.IsNullOrWhiteSpace(shift.HigherDutiesClassificationCode)) return 0m;
        return _library.ReferenceData.Classifications.FirstOrDefault(c => c.Code == shift.HigherDutiesClassificationCode)?.Hourly ?? 0m;
    }

    private static AnalysedShift AnalyseShift(PayRunDay day, PayRunShift shift)
    {
        var start = Combine(day.Date, shift.Start);
        var end = Combine(day.Date, shift.End);
        if (end <= start) end = end.AddDays(1);

        var rawMinutes = (decimal)(end - start).TotalMinutes;
        var unpaidBreakMinutes = 0m;
        var paidMealBreakMinutes = 0m;
        var paidRestPauseCount = 0m;
        var mealInterrupted = false;
        var requiredOnPremises = false;

        foreach (var brk in shift.Breaks)
        {
            if (string.IsNullOrWhiteSpace(brk.Start) || string.IsNullOrWhiteSpace(brk.End)) continue;

            var breakStart = Combine(day.Date, brk.Start);
            var breakEnd = Combine(day.Date, brk.End);
            if (breakEnd <= breakStart) breakEnd = breakEnd.AddDays(1);

            var minutes = (decimal)(breakEnd - breakStart).TotalMinutes;
            if (!brk.Paid) unpaidBreakMinutes += minutes;
            if (brk.Paid && brk.Type == "meal") paidMealBreakMinutes += minutes;
            if (brk.Paid && brk.Type == "rest" && minutes >= 10) paidRestPauseCount++;
            if (brk.Interrupted) mealInterrupted = true;
            if (brk.RequiredToRemainOnPremises) requiredOnPremises = true;
        }

        var workedMinutes = Math.Max(0m, rawMinutes - unpaidBreakMinutes);

        return new AnalysedShift
        {
            Shift = shift,
            StartDateTime = start,
            EndDateTime = end,
            StartMinutes = ToMinutesFromMidnight(shift.Start),
            EndMinutes = ToMinutesFromMidnight(shift.End) <= ToMinutesFromMidnight(shift.Start)
                ? ToMinutesFromMidnight(shift.End) + 1440
                : ToMinutesFromMidnight(shift.End),
            RawShiftHours = rawMinutes / 60m,
            WorkedHours = workedMinutes / 60m,
            UnpaidMealBreakMinutes = shift.Breaks.Where(b => b.Type == "meal" && !b.Paid).Sum(b => BreakMinutes(day.Date, b)),
            PaidMealBreakMinutes = paidMealBreakMinutes,
            PaidRestPauseCount = paidRestPauseCount,
            MealBreakInterrupted = mealInterrupted,
            RequiredToRemainOnPremises = requiredOnPremises
        };
    }

    private static decimal CalculateOutsideOrdinarySpanHours(string dayType, AnalysedShift shift)
    {
        if (dayType != "weekday") return 0m;
        var early = Math.Max(0m, Math.Min(shift.EndMinutes, 360) - shift.StartMinutes);
        var late = Math.Max(0m, shift.EndMinutes - Math.Max(shift.StartMinutes, 1110));
        return (early + late) / 60m;
    }

    private static decimal CalculatePartTimeOutsideRegularPatternHours(PayRunDay day, AnalysedShift shift)
    {
        if (string.IsNullOrWhiteSpace(day.RegularStart) || string.IsNullOrWhiteSpace(day.RegularEnd)) return 0m;
        var regularStart = ToMinutesFromMidnight(day.RegularStart);
        var regularEnd = ToMinutesFromMidnight(day.RegularEnd);
        if (regularEnd <= regularStart) regularEnd += 1440;

        var before = Math.Max(0m, Math.Min(shift.EndMinutes, regularStart) - shift.StartMinutes);
        var after = Math.Max(0m, shift.EndMinutes - Math.Max(shift.StartMinutes, regularEnd));
        return (before + after) / 60m;
    }

    private static decimal CalculateHoursBeyondSpreadCap(AnalysedShift shift, List<AnalysedShift> allDayShifts)
    {
        if (allDayShifts.Count <= 1) return 0m;
        var spreadStart = allDayShifts.First().StartDateTime;
        var capStart = spreadStart.AddHours(12);
        if (shift.EndDateTime <= capStart) return 0m;
        var overlapStart = shift.StartDateTime > capStart ? shift.StartDateTime : capStart;
        return (decimal)(shift.EndDateTime - overlapStart).TotalHours;
    }

    private static decimal CalculateMissedMealPenaltyHours(AnalysedShift shift)
    {
        if (shift.RawShiftHours <= 5m || shift.UnpaidMealBreakMinutes >= 30m || shift.RequiredToRemainOnPremises) return 0m;
        return Math.Max(0m, shift.RawShiftHours - 5m);
    }

    private static decimal BreakMinutes(DateOnly date, PayRunBreak brk)
    {
        if (string.IsNullOrWhiteSpace(brk.Start) || string.IsNullOrWhiteSpace(brk.End)) return 0m;
        var start = Combine(date, brk.Start);
        var end = Combine(date, brk.End);
        if (end <= start) end = end.AddDays(1);
        return (decimal)(end - start).TotalMinutes;
    }

    private static DateTime Combine(DateOnly date, string hhmm)
    {
        var parts = hhmm.Split(':');
        return date.ToDateTime(new TimeOnly(int.Parse(parts[0]), int.Parse(parts[1])));
    }

    private static decimal ToMinutesFromMidnight(string hhmm)
    {
        var parts = hhmm.Split(':');
        return int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
    }

    private sealed class AnalysedShift
    {
        public PayRunShift Shift { get; set; } = new();
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public decimal StartMinutes { get; set; }
        public decimal EndMinutes { get; set; }
        public decimal RawShiftHours { get; set; }
        public decimal WorkedHours { get; set; }
        public decimal UnpaidMealBreakMinutes { get; set; }
        public decimal PaidMealBreakMinutes { get; set; }
        public decimal PaidRestPauseCount { get; set; }
        public bool MealBreakInterrupted { get; set; }
        public bool RequiredToRemainOnPremises { get; set; }
    }
}
