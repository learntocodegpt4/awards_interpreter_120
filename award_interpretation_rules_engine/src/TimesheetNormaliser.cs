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
            var allowancesAppliedForDay = false;

            if (day.LeaveHours > 0m)
            {
                segmentIndex++;
                requests.Add(BuildLeaveRequest(input, day, segmentIndex, weeklyHoursBefore));
                weeklyHoursBefore += day.LeaveHours;
            }

            var analysedShifts = day.Shifts.Select(s => AnalyseShift(day, s)).OrderBy(s => s.StartDateTime).ToList();
            var payableDayWorkedHours = analysedShifts.Sum(s => s.WorkedHours);

            var brokenShiftCount = analysedShifts.Count;
            var brokenSpreadHours = brokenShiftCount > 1
                ? PayCalculationPolicy.DurationToHours(analysedShifts.Last().EndDateTime - analysedShifts.First().StartDateTime)
                : 0m;

            foreach (var analysed in analysedShifts)
            {
                var restHours = previousShiftEnd is null ? 999m : PayCalculationPolicy.DurationToHours(analysed.StartDateTime - previousShiftEnd.Value);
                previousShiftEnd = analysed.EndDateTime;
                var higherDutiesRate = ResolveHigherDutiesRate(input, analysed.Shift);
                var paidHoursBeforeSegmentInShift = 0m;

                foreach (var segment in SplitIntoPayableSegments(day, analysed))
                {
                    segmentIndex++;

                    var paidHours = segment.WorkedHours;
                    var segmentDayType = ResolveSegmentDayType(day, segment.StartDateTime);
                    var isPublicHolidaySegment = IsPublicHolidaySegment(day, segment.StartDateTime, segment.EndDateTime);
                    var outsideSpanHours = CalculateOutsideOrdinarySpanHours(segmentDayType, segment);
                    var hoursBeyondBrokenSpreadCap = CalculateHoursBeyondSpreadCap(segment, analysedShifts);
                    var appliesDailyAllowances = !allowancesAppliedForDay;

                    var request = new PayRunRequest
                    {
                        EmployeeReference = input.EmployeeReference,
                        PayPeriodReference = input.PayPeriodReference,
                        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["SegmentId"] = $"SEG-{segmentIndex:000}",
                            ["SourceShiftStartLocal"] = analysed.StartDateTime.ToString("O"),
                            ["SourceShiftEndLocal"] = analysed.EndDateTime.ToString("O"),
                            ["SegmentStartLocal"] = segment.StartDateTime.ToString("O"),
                            ["SegmentEndLocal"] = segment.EndDateTime.ToString("O"),
                            ["ClassificationCode"] = input.Employee.ClassificationCode,
                            ["EmploymentCategory"] = input.Employee.EmploymentCategory,
                            ["EmploymentProfileCode"] = input.Employee.EmploymentProfileCode,
                            ["DayType"] = segmentDayType,
                            ["ResolvedDayType"] = isPublicHolidaySegment ? "public_holiday" : segmentDayType,
                            ["ShiftTag"] = analysed.Shift.Tag,
                            ["HasEvidenceReference"] = !string.IsNullOrWhiteSpace(analysed.Shift.EvidenceReference),
                            ["WorkedHours"] = segment.WorkedHours,
                            ["RawShiftHours"] = segment.RawShiftHours,
                            ["PaidHours"] = paidHours,
                            ["PaidHoursBeforeSegmentInShift"] = paidHoursBeforeSegmentInShift,
                            ["WeeklyHoursBeforeShift"] = weeklyHoursBefore,
                            ["ContractedWeeklyHours"] = input.Employee.ContractedWeeklyHours,
                            ["ShiftStartMinutes"] = segment.StartMinutes,
                            ["ShiftEndMinutes"] = segment.EndMinutes,
                            ["IsShiftworker"] = input.Employee.EmploymentProfileCode.Contains("SHIFT", StringComparison.OrdinalIgnoreCase) || input.Employee.EmploymentProfileCode.Contains("NIGHT", StringComparison.OrdinalIgnoreCase),
                            ["IsPermanentNightShift"] = input.Employee.EmploymentProfileCode.Contains("PERM_NIGHT", StringComparison.OrdinalIgnoreCase) || analysed.Shift.Tag == "permanentNightShift",
                            ["IsLeave"] = false,
                            ["LeaveType"] = "",
                            ["LeaveHours"] = 0m,
                            ["LeavePenaltyMultiplier"] = 1m,
                            ["IsFirstPayableSegmentForDay"] = appliesDailyAllowances,
                            ["PayableDayWorkedHours"] = payableDayWorkedHours,
                            ["IsPublicHolidayFromCalendar"] = isPublicHolidaySegment,
                            ["IsActualPublicHoliday"] = isPublicHolidaySegment || day.ActualPublicHoliday,
                            ["IsSubstitutedPublicHoliday"] = day.SubstitutedPublicHoliday,
                            ["IsPartDayPublicHoliday"] = IsPartDayPublicHoliday(day),
                            ["PublicHolidayId"] = day.PublicHolidayId,
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
                            ["PartTimeOutsideRegularPatternHours"] = CalculatePartTimeOutsideRegularPatternHours(day, segment),
                            ["MissedMealPenaltyHours"] = CalculateMissedMealPenaltyHours(segment),
                            ["HigherDutiesHours"] = analysed.Shift.HigherDutiesEnabled ? segment.WorkedHours : 0m,
                            ["HigherDutiesRate"] = higherDutiesRate,
                            ["OpeningToilBalanceHours"] = input.Employee.OpeningToilBalanceHours,
                            ["ToilTakenHours"] = 0m,
                            ["ForceToilPayoutHours"] = 0m,
                            ["AnnualSalary"] = input.Employee.AnnualSalary,
                            ["VehicleKm"] = appliesDailyAllowances ? input.Allowances.VehicleKm : 0m,
                            ["VehicleType"] = appliesDailyAllowances ? input.Allowances.VehicleType : "none",
                            ["FirstAidRequired"] = appliesDailyAllowances && input.Allowances.FirstAidRequired,
                            ["IsOSHC"] = input.Allowances.IsOshc,
                            ["LaundryRequired"] = appliesDailyAllowances && input.Allowances.LaundryRequired,
                            ["LaundryRequiresIroning"] = input.Allowances.LaundryRequiresIroning,
                            ["MealAllowanceRequired"] = appliesDailyAllowances && input.Allowances.MealAllowanceRequired,
                            ["ExcessFaresRequired"] = appliesDailyAllowances && input.Allowances.ExcessFaresRequired,
                            ["EducationalLeaderDaysPerWeek"] = appliesDailyAllowances ? input.Allowances.EducationalLeaderDaysPerWeek : 0m,
                            ["AllPurposeAllowanceHourly"] = input.Employee.AllPurposeAllowanceHourly
                        }
                    };

                    requests.Add(request);
                    if (appliesDailyAllowances) allowancesAppliedForDay = true;
                    paidHoursBeforeSegmentInShift += paidHours;
                    weeklyHoursBefore += paidHours;
                }
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
                ["SourceShiftStartLocal"] = "",
                ["SourceShiftEndLocal"] = "",
                ["SegmentStartLocal"] = day.Date.ToDateTime(TimeOnly.MinValue).ToString("O"),
                ["SegmentEndLocal"] = day.Date.ToDateTime(TimeOnly.MinValue).AddHours((double)day.LeaveHours).ToString("O"),
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
                ["PaidHoursBeforeSegmentInShift"] = 0m,
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
                ["IsFirstPayableSegmentForDay"] = false,
                ["PayableDayWorkedHours"] = 0m,
                ["IsPublicHolidayFromCalendar"] = false,
                ["IsActualPublicHoliday"] = day.ActualPublicHoliday,
                ["IsSubstitutedPublicHoliday"] = day.SubstitutedPublicHoliday,
                ["IsPartDayPublicHoliday"] = IsPartDayPublicHoliday(day),
                ["PublicHolidayId"] = day.PublicHolidayId,
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

    private static List<AnalysedSegment> SplitIntoPayableSegments(PayRunDay day, AnalysedShift shift)
    {
        var payableRanges = RemoveUnpaidBreaks(day.Date, shift);
        var boundaries = BuildSplitBoundaries(day, shift.StartDateTime, shift.EndDateTime);
        var segments = new List<AnalysedSegment>();

        foreach (var range in payableRanges)
        {
            var points = boundaries
                .Where(b => b > range.Start && b < range.End)
                .Prepend(range.Start)
                .Append(range.End)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            for (var i = 0; i < points.Count - 1; i++)
            {
                if (points[i + 1] <= points[i]) continue;
                segments.Add(BuildSegment(shift, points[i], points[i + 1]));
            }
        }

        return segments;
    }

    private static List<TimeRange> RemoveUnpaidBreaks(DateOnly date, AnalysedShift shift)
    {
        var ranges = new List<TimeRange> { new(shift.StartDateTime, shift.EndDateTime) };

        foreach (var brk in shift.Shift.Breaks.Where(b => !b.Paid && !string.IsNullOrWhiteSpace(b.Start) && !string.IsNullOrWhiteSpace(b.End)))
        {
            var breakStart = Combine(date, brk.Start);
            var breakEnd = Combine(date, brk.End);
            if (breakEnd <= breakStart) breakEnd = breakEnd.AddDays(1);

            ranges = ranges
                .SelectMany(r => RemoveOverlap(r, new TimeRange(breakStart, breakEnd)))
                .Where(r => r.End > r.Start)
                .ToList();
        }

        return ranges;
    }

    private static IEnumerable<TimeRange> RemoveOverlap(TimeRange source, TimeRange removal)
    {
        if (removal.End <= source.Start || removal.Start >= source.End)
        {
            yield return source;
            yield break;
        }

        if (removal.Start > source.Start)
            yield return new TimeRange(source.Start, removal.Start);

        if (removal.End < source.End)
            yield return new TimeRange(removal.End, source.End);
    }

    private static List<DateTime> BuildSplitBoundaries(PayRunDay day, DateTime start, DateTime end)
    {
        var boundaries = new List<DateTime>();
        for (var boundary = start.Date.AddDays(1); boundary < end; boundary = boundary.AddDays(1))
            boundaries.Add(boundary);

        if (TryGetPublicHolidayWindow(day, out var publicHolidayStart, out var publicHolidayEnd))
        {
            boundaries.Add(publicHolidayStart);
            boundaries.Add(publicHolidayEnd);
        }

        return boundaries;
    }

    private static AnalysedSegment BuildSegment(AnalysedShift shift, DateTime start, DateTime end)
    {
        var rawHours = PayCalculationPolicy.DurationToHours(end - start);
        var segmentDate = DateOnly.FromDateTime(start);

        return new AnalysedSegment
        {
            Shift = shift.Shift,
            StartDateTime = start,
            EndDateTime = end,
            StartMinutes = MinutesFromDateStart(segmentDate, start),
            EndMinutes = MinutesFromDateStart(segmentDate, end),
            RawShiftHours = rawHours,
            WorkedHours = rawHours,
            UnpaidMealBreakMinutes = shift.UnpaidMealBreakMinutes,
            PaidMealBreakMinutes = shift.PaidMealBreakMinutes,
            PaidRestPauseCount = shift.PaidRestPauseCount,
            MealBreakInterrupted = shift.MealBreakInterrupted,
            RequiredToRemainOnPremises = shift.RequiredToRemainOnPremises
        };
    }

    private static AnalysedShift AnalyseShift(PayRunDay day, PayRunShift shift)
    {
        var start = Combine(day.Date, shift.Start);
        var end = Combine(day.Date, shift.End);
        if (end <= start) end = end.AddDays(1);

        var rawMinutes = PayCalculationPolicy.DurationToMinutes(end - start);
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

            var minutes = PayCalculationPolicy.DurationToMinutes(breakEnd - breakStart);
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
            RawShiftHours = PayCalculationPolicy.RoundHours(rawMinutes / 60m),
            WorkedHours = PayCalculationPolicy.RoundHours(workedMinutes / 60m),
            UnpaidMealBreakMinutes = shift.Breaks.Where(b => b.Type == "meal" && !b.Paid).Sum(b => BreakMinutes(day.Date, b)),
            PaidMealBreakMinutes = paidMealBreakMinutes,
            PaidRestPauseCount = paidRestPauseCount,
            MealBreakInterrupted = mealInterrupted,
            RequiredToRemainOnPremises = requiredOnPremises
        };
    }

    private static decimal CalculateOutsideOrdinarySpanHours(string dayType, AnalysedSegment shift)
    {
        if (dayType != "weekday") return 0m;
        var early = Math.Max(0m, Math.Min(shift.EndMinutes, 360) - shift.StartMinutes);
        var late = Math.Max(0m, shift.EndMinutes - Math.Max(shift.StartMinutes, 1110));
        return PayCalculationPolicy.RoundHours((early + late) / 60m);
    }

    private static decimal CalculatePartTimeOutsideRegularPatternHours(PayRunDay day, AnalysedSegment shift)
    {
        if (string.IsNullOrWhiteSpace(day.RegularStart) || string.IsNullOrWhiteSpace(day.RegularEnd)) return 0m;
        var regularStart = ToMinutesFromMidnight(day.RegularStart);
        var regularEnd = ToMinutesFromMidnight(day.RegularEnd);
        if (regularEnd <= regularStart) regularEnd += 1440;

        var before = Math.Max(0m, Math.Min(shift.EndMinutes, regularStart) - shift.StartMinutes);
        var after = Math.Max(0m, shift.EndMinutes - Math.Max(shift.StartMinutes, regularEnd));
        return PayCalculationPolicy.RoundHours((before + after) / 60m);
    }

    private static decimal CalculateHoursBeyondSpreadCap(AnalysedSegment shift, List<AnalysedShift> allDayShifts)
    {
        if (allDayShifts.Count <= 1) return 0m;
        var spreadStart = allDayShifts.First().StartDateTime;
        var capStart = spreadStart.AddHours(12);
        if (shift.EndDateTime <= capStart) return 0m;
        var overlapStart = shift.StartDateTime > capStart ? shift.StartDateTime : capStart;
        return PayCalculationPolicy.DurationToHours(shift.EndDateTime - overlapStart);
    }

    private static decimal CalculateMissedMealPenaltyHours(AnalysedSegment shift)
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
        return PayCalculationPolicy.DurationToMinutes(end - start);
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

    private static decimal MinutesFromDateStart(DateOnly date, DateTime value)
    {
        var dateStart = date.ToDateTime(TimeOnly.MinValue);
        return PayCalculationPolicy.DurationToMinutes(value - dateStart);
    }

    private static string ResolveSegmentDayType(PayRunDay sourceDay, DateTime segmentStart)
    {
        if (segmentStart.Date == sourceDay.Date.ToDateTime(TimeOnly.MinValue).Date)
            return sourceDay.DayType;

        return segmentStart.DayOfWeek switch
        {
            DayOfWeek.Saturday => "saturday",
            DayOfWeek.Sunday => "sunday",
            _ => "weekday"
        };
    }

    private static bool IsPublicHolidaySegment(PayRunDay day, DateTime segmentStart, DateTime segmentEnd)
    {
        if (TryGetPublicHolidayWindow(day, out var publicHolidayStart, out var publicHolidayEnd))
            return segmentStart >= publicHolidayStart && segmentEnd <= publicHolidayEnd;

        return day.ActualPublicHoliday || day.SubstitutedPublicHoliday || day.DayType.Equals("public_holiday", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPartDayPublicHoliday(PayRunDay day)
        => TryGetPublicHolidayWindow(day, out _, out _);

    private static bool TryGetPublicHolidayWindow(PayRunDay day, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;

        if (string.IsNullOrWhiteSpace(day.PublicHolidayStart) || string.IsNullOrWhiteSpace(day.PublicHolidayEnd))
            return false;

        start = Combine(day.Date, day.PublicHolidayStart);
        end = Combine(day.Date, day.PublicHolidayEnd);
        if (end <= start) end = end.AddDays(1);
        return true;
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

    private sealed class AnalysedSegment
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

    private readonly record struct TimeRange(DateTime Start, DateTime End);
}
