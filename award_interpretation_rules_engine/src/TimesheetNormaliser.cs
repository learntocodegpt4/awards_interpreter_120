namespace AwardInterpretationRulesEngine;

public sealed class TimesheetNormaliser
{
    private readonly GovernedExpressionLibrary _library;

    public TimesheetNormaliser(GovernedExpressionLibrary library)
    {
        _library = library;
    }

    public List<PayRunRequest> BuildSegmentRequests(PayRunInput input)
        => BuildNormalisedSegments(input).SegmentRequests;

    public TimesheetNormalisationResult BuildNormalisedSegments(PayRunInput input)
    {
        var result = new TimesheetNormalisationResult();
        result.Warnings.AddRange(Validate(input));
        if (result.BlocksPayrollExport) return result;

        var weeklyHoursBefore = 0m;
        DateTime? previousShiftEnd = null;
        var segmentIndex = 0;

        foreach (var day in input.Days.OrderBy(d => d.Date))
        {
            var allowancesAppliedForDay = false;

            if (day.LeaveHours > 0m)
            {
                segmentIndex++;
                var leaveSegment = BuildLeaveSegment(day, segmentIndex);
                result.Segments.Add(leaveSegment);
                result.SegmentRequests.Add(BuildLeaveRequest(input, day, leaveSegment, weeklyHoursBefore));
                weeklyHoursBefore += day.LeaveHours;
            }

            var analysedShifts = day.Shifts
                .Select((shift, index) => AnalyseShift(day, shift, index))
                .OrderBy(s => s.StartDateTime)
                .ThenBy(s => s.SourceIndex)
                .ToList();
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
                    segment.SegmentId = $"SEG-{segmentIndex:000}";

                    var paidHours = segment.WorkedHours;
                    var segmentDayType = ResolveSegmentDayType(day, segment.StartDateTime);
                    var isPublicHolidaySegment = IsPublicHolidaySegment(day, segment.StartDateTime, segment.EndDateTime);
                    var outsideSpanHours = CalculateOutsideOrdinarySpanHours(segmentDayType, segment);
                    var hoursBeyondBrokenSpreadCap = CalculateHoursBeyondSpreadCap(segment, analysedShifts);
                    var appliesDailyAllowances = !allowancesAppliedForDay;
                    var normalisedSegment = BuildNormalisedSegment(day, analysed, segment, segmentDayType, isPublicHolidaySegment);

                    var request = new PayRunRequest
                    {
                        EmployeeReference = input.EmployeeReference,
                        PayPeriodReference = input.PayPeriodReference,
                        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["SegmentId"] = segment.SegmentId,
                            ["SegmentKind"] = normalisedSegment.SegmentKind,
                            ["WorkDate"] = normalisedSegment.WorkDate.ToString("yyyy-MM-dd"),
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
                            ["IsWorkedSegment"] = normalisedSegment.IsWorkedSegment,
                            ["IsBreakSegment"] = normalisedSegment.IsBreakSegment,
                            ["IsPaidBreakSegment"] = normalisedSegment.IsPaidBreakSegment,
                            ["IsUnpaidBreakSegment"] = normalisedSegment.IsUnpaidBreakSegment,
                            ["PaidHoursBeforeSegmentInShift"] = paidHoursBeforeSegmentInShift,
                            ["WeeklyHoursBeforeShift"] = weeklyHoursBefore,
                            ["ContractedWeeklyHours"] = input.Employee.ContractedWeeklyHours,
                            ["ShiftStartMinutes"] = segment.StartMinutes,
                            ["ShiftEndMinutes"] = segment.EndMinutes,
                            ["IsShiftworker"] = input.Employee.EmploymentProfileCode.Contains("SHIFT", StringComparison.OrdinalIgnoreCase) || input.Employee.EmploymentProfileCode.Contains("NIGHT", StringComparison.OrdinalIgnoreCase),
                            ["IsPermanentNightShift"] = input.Employee.EmploymentProfileCode.Contains("PERM_NIGHT", StringComparison.OrdinalIgnoreCase) || analysed.Shift.Tag == "permanentNightShift",
                            ["IsOnCall"] = normalisedSegment.IsOnCall,
                            ["IsRecall"] = normalisedSegment.IsRecall,
                            ["IsOvernight"] = normalisedSegment.IsOvernight,
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
                            ["PublicHolidayStartLocal"] = normalisedSegment.PublicHolidayStartLocal?.ToString("O") ?? "",
                            ["PublicHolidayEndLocal"] = normalisedSegment.PublicHolidayEndLocal?.ToString("O") ?? "",
                            ["HasPublicHolidayElectionEvidence"] = !string.IsNullOrWhiteSpace(day.PublicHolidayElectionEvidence),
                            ["TotalUnpaidBreakMinutes"] = analysed.UnpaidBreakMinutes,
                            ["TotalPaidBreakMinutes"] = analysed.PaidBreakMinutes,
                            ["SegmentUnpaidBreakMinutes"] = segment.SegmentUnpaidBreakMinutes,
                            ["SegmentPaidBreakMinutes"] = segment.SegmentPaidBreakMinutes,
                            ["UnpaidMealBreakMinutes"] = analysed.UnpaidMealBreakMinutes,
                            ["PaidMealBreakMinutes"] = analysed.PaidMealBreakMinutes,
                            ["MealBreakInterrupted"] = analysed.MealBreakInterrupted,
                            ["RequiredToRemainOnPremises"] = analysed.RequiredToRemainOnPremises,
                            ["PaidRestPauseCount"] = analysed.PaidRestPauseCount,
                            ["RosterContext"] = normalisedSegment.RosterContext,
                            ["RosterStartLocal"] = normalisedSegment.RosterStartLocal?.ToString("O") ?? "",
                            ["RosterEndLocal"] = normalisedSegment.RosterEndLocal?.ToString("O") ?? "",
                            ["RosterStartMinutes"] = normalisedSegment.RosterStartMinutes,
                            ["RosterEndMinutes"] = normalisedSegment.RosterEndMinutes,
                            ["RosterVarianceMinutes"] = normalisedSegment.RosterVarianceMinutes,
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

                    result.Segments.Add(normalisedSegment);
                    result.SegmentRequests.Add(request);
                    if (appliesDailyAllowances) allowancesAppliedForDay = true;
                    paidHoursBeforeSegmentInShift += paidHours;
                    weeklyHoursBefore += paidHours;
                }
            }
        }

        return result;
    }

    private static NormalisedTimesheetSegment BuildLeaveSegment(PayRunDay day, int segmentIndex)
    {
        var start = day.Date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddHours((double)day.LeaveHours);
        return new NormalisedTimesheetSegment
        {
            SegmentId = $"SEG-{segmentIndex:000}",
            SegmentKind = "leave",
            WorkDate = day.Date,
            SegmentStartLocal = start,
            SegmentEndLocal = end,
            PaidHours = day.LeaveHours,
            IsLeave = true,
            LeaveType = day.LeaveType,
            LeaveHours = day.LeaveHours,
            DayType = day.DayType,
            ResolvedDayType = day.DayType,
            IsActualPublicHoliday = day.ActualPublicHoliday,
            IsSubstitutedPublicHoliday = day.SubstitutedPublicHoliday,
            IsPartDayPublicHoliday = IsPartDayPublicHoliday(day),
            PublicHolidayId = day.PublicHolidayId,
            RosterContext = day.RosterContext,
            PublicHolidayStartLocal = TryGetPublicHolidayWindow(day, out var publicHolidayStart, out _) ? publicHolidayStart : null,
            PublicHolidayEndLocal = TryGetPublicHolidayWindow(day, out _, out var publicHolidayEnd) ? publicHolidayEnd : null
        };
    }

    private static PayRunRequest BuildLeaveRequest(PayRunInput input, PayRunDay day, NormalisedTimesheetSegment segment, decimal weeklyHoursBefore)
    {
        return new PayRunRequest
        {
            EmployeeReference = input.EmployeeReference,
            PayPeriodReference = input.PayPeriodReference,
            Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["SegmentId"] = segment.SegmentId,
                ["SegmentKind"] = segment.SegmentKind,
                ["WorkDate"] = day.Date.ToString("yyyy-MM-dd"),
                ["SourceShiftStartLocal"] = "",
                ["SourceShiftEndLocal"] = "",
                ["SegmentStartLocal"] = segment.SegmentStartLocal.ToString("O"),
                ["SegmentEndLocal"] = segment.SegmentEndLocal.ToString("O"),
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
                ["IsWorkedSegment"] = false,
                ["IsBreakSegment"] = false,
                ["IsPaidBreakSegment"] = false,
                ["IsUnpaidBreakSegment"] = false,
                ["PaidHoursBeforeSegmentInShift"] = 0m,
                ["WeeklyHoursBeforeShift"] = weeklyHoursBefore,
                ["ContractedWeeklyHours"] = input.Employee.ContractedWeeklyHours,
                ["ShiftStartMinutes"] = 0m,
                ["ShiftEndMinutes"] = 0m,
                ["IsShiftworker"] = input.Employee.EmploymentProfileCode.Contains("SHIFT", StringComparison.OrdinalIgnoreCase) || input.Employee.EmploymentProfileCode.Contains("NIGHT", StringComparison.OrdinalIgnoreCase),
                ["IsPermanentNightShift"] = input.Employee.EmploymentProfileCode.Contains("PERM_NIGHT", StringComparison.OrdinalIgnoreCase),
                ["IsOnCall"] = false,
                ["IsRecall"] = false,
                ["IsOvernight"] = false,
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
                ["PublicHolidayStartLocal"] = segment.PublicHolidayStartLocal?.ToString("O") ?? "",
                ["PublicHolidayEndLocal"] = segment.PublicHolidayEndLocal?.ToString("O") ?? "",
                ["HasPublicHolidayElectionEvidence"] = !string.IsNullOrWhiteSpace(day.PublicHolidayElectionEvidence),
                ["TotalUnpaidBreakMinutes"] = 0m,
                ["TotalPaidBreakMinutes"] = 0m,
                ["SegmentUnpaidBreakMinutes"] = 0m,
                ["SegmentPaidBreakMinutes"] = 0m,
                ["UnpaidMealBreakMinutes"] = 0m,
                ["PaidMealBreakMinutes"] = 0m,
                ["MealBreakInterrupted"] = false,
                ["RequiredToRemainOnPremises"] = false,
                ["PaidRestPauseCount"] = 0m,
                ["RosterContext"] = day.RosterContext,
                ["RosterStartLocal"] = "",
                ["RosterEndLocal"] = "",
                ["RosterStartMinutes"] = 0m,
                ["RosterEndMinutes"] = 0m,
                ["RosterVarianceMinutes"] = 0m,
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
            var breakRange = BuildBreakRange(date, brk, shift.StartDateTime);

            ranges = ranges
                .SelectMany(r => RemoveOverlap(r, breakRange))
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
        var segmentRange = new TimeRange(start, end);

        return new AnalysedSegment
        {
            Shift = shift.Shift,
            StartDateTime = start,
            EndDateTime = end,
            StartMinutes = MinutesFromDateStart(segmentDate, start),
            EndMinutes = MinutesFromDateStart(segmentDate, end),
            RawShiftHours = rawHours,
            WorkedHours = rawHours,
            SegmentPaidBreakMinutes = shift.BreakRanges.Where(b => b.Paid).Sum(b => OverlapMinutes(segmentRange, b.Range)),
            SegmentUnpaidBreakMinutes = shift.BreakRanges.Where(b => !b.Paid).Sum(b => OverlapMinutes(segmentRange, b.Range)),
            UnpaidMealBreakMinutes = shift.UnpaidMealBreakMinutes,
            PaidMealBreakMinutes = shift.PaidMealBreakMinutes,
            PaidRestPauseCount = shift.PaidRestPauseCount,
            MealBreakInterrupted = shift.MealBreakInterrupted,
            RequiredToRemainOnPremises = shift.RequiredToRemainOnPremises
        };
    }

    private static NormalisedTimesheetSegment BuildNormalisedSegment(
        PayRunDay day,
        AnalysedShift shift,
        AnalysedSegment segment,
        string segmentDayType,
        bool isPublicHolidaySegment)
    {
        TryGetRosterWindow(day, shift.Shift, out var rosterStart, out var rosterEnd);
        TryGetPublicHolidayWindow(day, out var publicHolidayStart, out var publicHolidayEnd);

        var kind = NormaliseSegmentKind(shift.Shift);
        var isOnCall = IsKindOrTag(shift.Shift, "on_call") || IsKindOrTag(shift.Shift, "on-call");
        var isRecall = IsKindOrTag(shift.Shift, "recall");
        var rosterContext = string.IsNullOrWhiteSpace(shift.Shift.RosterContext)
            ? day.RosterContext
            : shift.Shift.RosterContext;

        return new NormalisedTimesheetSegment
        {
            SegmentId = segment.SegmentId,
            SegmentKind = kind,
            WorkDate = DateOnly.FromDateTime(segment.StartDateTime),
            SourceShiftStartLocal = shift.StartDateTime,
            SourceShiftEndLocal = shift.EndDateTime,
            SegmentStartLocal = segment.StartDateTime,
            SegmentEndLocal = segment.EndDateTime,
            RawShiftHours = segment.RawShiftHours,
            WorkedHours = segment.WorkedHours,
            PaidHours = segment.WorkedHours,
            TotalPaidBreakMinutes = shift.PaidBreakMinutes,
            TotalUnpaidBreakMinutes = shift.UnpaidBreakMinutes,
            TotalUnpaidMealBreakMinutes = shift.UnpaidMealBreakMinutes,
            TotalPaidMealBreakMinutes = shift.PaidMealBreakMinutes,
            SegmentPaidBreakMinutes = segment.SegmentPaidBreakMinutes,
            SegmentUnpaidBreakMinutes = segment.SegmentUnpaidBreakMinutes,
            IsWorkedSegment = !kind.Equals("break", StringComparison.OrdinalIgnoreCase),
            IsBreakSegment = kind.Equals("break", StringComparison.OrdinalIgnoreCase),
            IsPaidBreakSegment = kind.Equals("break", StringComparison.OrdinalIgnoreCase) && segment.SegmentPaidBreakMinutes > 0m,
            IsUnpaidBreakSegment = kind.Equals("break", StringComparison.OrdinalIgnoreCase) && segment.SegmentUnpaidBreakMinutes > 0m,
            IsOnCall = isOnCall,
            IsRecall = isRecall,
            IsOvernight = shift.EndDateTime.Date > shift.StartDateTime.Date,
            ShiftTag = shift.Shift.Tag,
            RosterContext = rosterContext,
            RosterStartLocal = rosterStart,
            RosterEndLocal = rosterEnd,
            RosterStartMinutes = rosterStart is null ? 0m : MinutesFromDateStart(day.Date, rosterStart.Value),
            RosterEndMinutes = rosterEnd is null ? 0m : MinutesFromDateStart(day.Date, rosterEnd.Value),
            RosterVarianceMinutes = rosterStart is null ? 0m : (decimal)(segment.StartDateTime - rosterStart.Value).TotalMinutes,
            DayType = segmentDayType,
            ResolvedDayType = isPublicHolidaySegment ? "public_holiday" : segmentDayType,
            IsPublicHolidayFromCalendar = isPublicHolidaySegment,
            IsActualPublicHoliday = isPublicHolidaySegment || day.ActualPublicHoliday,
            IsSubstitutedPublicHoliday = day.SubstitutedPublicHoliday,
            IsPartDayPublicHoliday = IsPartDayPublicHoliday(day),
            PublicHolidayId = day.PublicHolidayId,
            PublicHolidayStartLocal = publicHolidayStart == default ? null : publicHolidayStart,
            PublicHolidayEndLocal = publicHolidayEnd == default ? null : publicHolidayEnd
        };
    }

    private static AnalysedShift AnalyseShift(PayRunDay day, PayRunShift shift, int sourceIndex)
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
        var breakRanges = new List<AnalysedBreak>();

        foreach (var brk in shift.Breaks)
        {
            if (string.IsNullOrWhiteSpace(brk.Start) || string.IsNullOrWhiteSpace(brk.End)) continue;

            var breakRange = BuildBreakRange(day.Date, brk, start);
            var breakStart = breakRange.Start;
            var breakEnd = breakRange.End;

            var minutes = PayCalculationPolicy.DurationToMinutes(breakEnd - breakStart);
            if (!brk.Paid) unpaidBreakMinutes += minutes;
            if (brk.Paid && brk.Type == "meal") paidMealBreakMinutes += minutes;
            if (brk.Paid && brk.Type == "rest" && minutes >= 10) paidRestPauseCount++;
            if (brk.Interrupted) mealInterrupted = true;
            if (brk.RequiredToRemainOnPremises) requiredOnPremises = true;
            breakRanges.Add(new AnalysedBreak(new TimeRange(breakStart, breakEnd), brk.Paid, brk.Type));
        }

        var workedMinutes = Math.Max(0m, rawMinutes - unpaidBreakMinutes);

        return new AnalysedShift
        {
            Shift = shift,
            SourceIndex = sourceIndex,
            StartDateTime = start,
            EndDateTime = end,
            StartMinutes = ToMinutesFromMidnight(shift.Start),
            EndMinutes = ToMinutesFromMidnight(shift.End) <= ToMinutesFromMidnight(shift.Start)
                ? ToMinutesFromMidnight(shift.End) + 1440
                : ToMinutesFromMidnight(shift.End),
            RawShiftHours = PayCalculationPolicy.RoundHours(rawMinutes / 60m),
            WorkedHours = PayCalculationPolicy.RoundHours(workedMinutes / 60m),
            UnpaidBreakMinutes = unpaidBreakMinutes,
            PaidBreakMinutes = breakRanges.Where(b => b.Paid).Sum(b => (decimal)(b.Range.End - b.Range.Start).TotalMinutes),
            UnpaidMealBreakMinutes = breakRanges.Where(b => b.Type == "meal" && !b.Paid).Sum(b => (decimal)(b.Range.End - b.Range.Start).TotalMinutes),
            PaidMealBreakMinutes = paidMealBreakMinutes,
            PaidRestPauseCount = paidRestPauseCount,
            MealBreakInterrupted = mealInterrupted,
            RequiredToRemainOnPremises = requiredOnPremises,
            BreakRanges = breakRanges
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

    private static TimeRange BuildBreakRange(DateOnly date, PayRunBreak brk, DateTime shiftStart)
    {
        var start = Combine(date, brk.Start);
        var end = Combine(date, brk.End);
        if (start < shiftStart)
        {
            start = start.AddDays(1);
            end = end.AddDays(1);
        }

        if (end <= start) end = end.AddDays(1);
        return new TimeRange(start, end);
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

    private static bool TryGetRosterWindow(PayRunDay day, PayRunShift shift, out DateTime? start, out DateTime? end)
    {
        start = null;
        end = null;

        if (string.IsNullOrWhiteSpace(shift.RosterStart) && string.IsNullOrWhiteSpace(shift.RosterEnd))
            return false;

        if (string.IsNullOrWhiteSpace(shift.RosterStart) || string.IsNullOrWhiteSpace(shift.RosterEnd))
            return false;

        var rosterStart = Combine(day.Date, shift.RosterStart);
        var rosterEnd = Combine(day.Date, shift.RosterEnd);
        if (rosterEnd <= rosterStart) rosterEnd = rosterEnd.AddDays(1);

        start = rosterStart;
        end = rosterEnd;
        return true;
    }

    private static List<RuleWarning> Validate(PayRunInput input)
    {
        var warnings = new List<RuleWarning>();

        if (input.Days.Count == 0)
            AddBlockedWarning(warnings, "TIMESHEET_EMPTY", "Timesheet contains no days to calculate.");

        for (var dayIndex = 0; dayIndex < input.Days.Count; dayIndex++)
        {
            var day = input.Days[dayIndex];
            var dayRef = $"day[{dayIndex}]";

            if (day.Date == default)
                AddBlockedWarning(warnings, "TIMESHEET_DAY_DATE_REQUIRED", $"{dayRef} is missing a work date.");

            if (day.LeaveHours < 0m)
                AddBlockedWarning(warnings, "TIMESHEET_LEAVE_HOURS_INVALID", $"{dayRef} has negative leave hours.");

            if (day.LeaveHours > 0m && string.IsNullOrWhiteSpace(day.LeaveType))
                AddBlockedWarning(warnings, "TIMESHEET_LEAVE_TYPE_REQUIRED", $"{dayRef} has leave hours without a leave type.");

            if (HasPartialWindow(day.PublicHolidayStart, day.PublicHolidayEnd))
                AddBlockedWarning(warnings, "TIMESHEET_PUBLIC_HOLIDAY_WINDOW_INCOMPLETE", $"{dayRef} has an incomplete part-day public holiday window.");

            if (!string.IsNullOrWhiteSpace(day.PublicHolidayStart) && !IsTime(day.PublicHolidayStart))
                AddBlockedWarning(warnings, "TIMESHEET_PUBLIC_HOLIDAY_TIME_INVALID", $"{dayRef} has an invalid public holiday start time '{day.PublicHolidayStart}'.");

            if (!string.IsNullOrWhiteSpace(day.PublicHolidayEnd) && !IsTime(day.PublicHolidayEnd))
                AddBlockedWarning(warnings, "TIMESHEET_PUBLIC_HOLIDAY_TIME_INVALID", $"{dayRef} has an invalid public holiday end time '{day.PublicHolidayEnd}'.");

            for (var shiftIndex = 0; shiftIndex < day.Shifts.Count; shiftIndex++)
            {
                var shift = day.Shifts[shiftIndex];
                var shiftRef = $"{dayRef}.shift[{shiftIndex}]";

                if (string.IsNullOrWhiteSpace(shift.Start) || string.IsNullOrWhiteSpace(shift.End))
                {
                    AddBlockedWarning(warnings, "TIMESHEET_SHIFT_TIME_REQUIRED", $"{shiftRef} is missing a start or end time.");
                }
                else
                {
                    if (!IsTime(shift.Start))
                        AddBlockedWarning(warnings, "TIMESHEET_SHIFT_TIME_INVALID", $"{shiftRef} has an invalid start time '{shift.Start}'.");

                    if (!IsTime(shift.End))
                        AddBlockedWarning(warnings, "TIMESHEET_SHIFT_TIME_INVALID", $"{shiftRef} has an invalid end time '{shift.End}'.");

                    if (IsTime(shift.Start) && IsTime(shift.End) && ToMinutesFromMidnight(shift.Start) == ToMinutesFromMidnight(shift.End))
                        AddBlockedWarning(warnings, "TIMESHEET_SHIFT_DURATION_INVALID", $"{shiftRef} has identical start and end times.");
                }

                if (HasPartialWindow(shift.RosterStart, shift.RosterEnd))
                    AddBlockedWarning(warnings, "TIMESHEET_ROSTER_WINDOW_INCOMPLETE", $"{shiftRef} has an incomplete roster window.");

                if (!string.IsNullOrWhiteSpace(shift.RosterStart) && !IsTime(shift.RosterStart))
                    AddBlockedWarning(warnings, "TIMESHEET_ROSTER_TIME_INVALID", $"{shiftRef} has an invalid roster start time '{shift.RosterStart}'.");

                if (!string.IsNullOrWhiteSpace(shift.RosterEnd) && !IsTime(shift.RosterEnd))
                    AddBlockedWarning(warnings, "TIMESHEET_ROSTER_TIME_INVALID", $"{shiftRef} has an invalid roster end time '{shift.RosterEnd}'.");

                ValidateBreaks(warnings, day, shift, shiftRef);
            }
        }

        return warnings;
    }

    private static void ValidateBreaks(List<RuleWarning> warnings, PayRunDay day, PayRunShift shift, string shiftRef)
    {
        if (!IsTime(shift.Start) || !IsTime(shift.End)) return;

        var shiftStart = Combine(day.Date, shift.Start);
        var shiftEnd = Combine(day.Date, shift.End);
        if (shiftEnd <= shiftStart) shiftEnd = shiftEnd.AddDays(1);
        var shiftRange = new TimeRange(shiftStart, shiftEnd);

        for (var breakIndex = 0; breakIndex < shift.Breaks.Count; breakIndex++)
        {
            var brk = shift.Breaks[breakIndex];
            var breakRef = $"{shiftRef}.break[{breakIndex}]";

            if (HasPartialWindow(brk.Start, brk.End))
            {
                AddBlockedWarning(warnings, "TIMESHEET_BREAK_TIME_REQUIRED", $"{breakRef} is missing a start or end time.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(brk.Start) && string.IsNullOrWhiteSpace(brk.End)) continue;

            if (!IsTime(brk.Start))
                AddBlockedWarning(warnings, "TIMESHEET_BREAK_TIME_INVALID", $"{breakRef} has an invalid start time '{brk.Start}'.");

            if (!IsTime(brk.End))
                AddBlockedWarning(warnings, "TIMESHEET_BREAK_TIME_INVALID", $"{breakRef} has an invalid end time '{brk.End}'.");

            if (!IsTime(brk.Start) || !IsTime(brk.End)) continue;

            if (ToMinutesFromMidnight(brk.Start) == ToMinutesFromMidnight(brk.End))
                AddBlockedWarning(warnings, "TIMESHEET_BREAK_DURATION_INVALID", $"{breakRef} has identical start and end times.");

            var breakRange = BuildBreakRange(day.Date, brk, shiftStart);
            var breakStart = breakRange.Start;
            var breakEnd = breakRange.End;
            if (OverlapMinutes(shiftRange, breakRange) <= 0m || breakStart < shiftStart || breakEnd > shiftEnd)
                AddBlockedWarning(warnings, "TIMESHEET_BREAK_OUTSIDE_SHIFT", $"{breakRef} sits outside its source shift.");
        }
    }

    private static bool HasPartialWindow(string start, string end)
        => string.IsNullOrWhiteSpace(start) != string.IsNullOrWhiteSpace(end);

    private static void AddBlockedWarning(List<RuleWarning> warnings, string ruleId, string message)
        => warnings.Add(new RuleWarning
        {
            SegmentId = "TIMESHEET",
            RuleId = ruleId,
            Severity = "error",
            Message = message,
            ManualReviewPolicy = "fix_source_timesheet",
            BlocksPayrollExport = true
        });

    private static string NormaliseSegmentKind(PayRunShift shift)
    {
        var kind = string.IsNullOrWhiteSpace(shift.SegmentKind) ? "clock" : shift.SegmentKind.Trim();
        kind = kind.Replace("-", "_", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

        if (kind is "oncall") return "on_call";
        if (kind is "worked" or "work") return "clock";
        return kind;
    }

    private static bool IsKindOrTag(PayRunShift shift, string value)
    {
        var normalizedValue = value.Replace("-", "_", StringComparison.OrdinalIgnoreCase);
        if (NormaliseSegmentKind(shift).Equals(normalizedValue, StringComparison.OrdinalIgnoreCase))
            return true;

        return shift.Tag
            .Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(tag => tag.Replace("-", "_", StringComparison.OrdinalIgnoreCase).Equals(normalizedValue, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTime(string hhmm)
        => TimeOnly.TryParseExact(hhmm, ["H:mm", "HH:mm"], null, System.Globalization.DateTimeStyles.None, out _);

    private static decimal OverlapMinutes(TimeRange left, TimeRange right)
    {
        var start = left.Start > right.Start ? left.Start : right.Start;
        var end = left.End < right.End ? left.End : right.End;
        return end <= start ? 0m : (decimal)(end - start).TotalMinutes;
    }

    private sealed class AnalysedShift
    {
        public PayRunShift Shift { get; set; } = new();
        public int SourceIndex { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public decimal StartMinutes { get; set; }
        public decimal EndMinutes { get; set; }
        public decimal RawShiftHours { get; set; }
        public decimal WorkedHours { get; set; }
        public decimal UnpaidBreakMinutes { get; set; }
        public decimal PaidBreakMinutes { get; set; }
        public decimal UnpaidMealBreakMinutes { get; set; }
        public decimal PaidMealBreakMinutes { get; set; }
        public decimal PaidRestPauseCount { get; set; }
        public bool MealBreakInterrupted { get; set; }
        public bool RequiredToRemainOnPremises { get; set; }
        public List<AnalysedBreak> BreakRanges { get; set; } = [];
    }

    private sealed class AnalysedSegment
    {
        public string SegmentId { get; set; } = "";
        public PayRunShift Shift { get; set; } = new();
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public decimal StartMinutes { get; set; }
        public decimal EndMinutes { get; set; }
        public decimal RawShiftHours { get; set; }
        public decimal WorkedHours { get; set; }
        public decimal SegmentPaidBreakMinutes { get; set; }
        public decimal SegmentUnpaidBreakMinutes { get; set; }
        public decimal UnpaidMealBreakMinutes { get; set; }
        public decimal PaidMealBreakMinutes { get; set; }
        public decimal PaidRestPauseCount { get; set; }
        public bool MealBreakInterrupted { get; set; }
        public bool RequiredToRemainOnPremises { get; set; }
    }

    private readonly record struct AnalysedBreak(TimeRange Range, bool Paid, string Type);
    private readonly record struct TimeRange(DateTime Start, DateTime End);
}
