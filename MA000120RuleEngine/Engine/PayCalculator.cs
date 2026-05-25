// ============================================================
//  PayCalculator.cs
//  Computes gross pay for a shift using award rules and rates
// ============================================================

using MA000120RuleEngine.Data;
using MA000120RuleEngine.Models;

namespace MA000120RuleEngine.Engine;

public static class PayCalculator
{
    /// <summary>
    /// Calculate gross pay for a given shift input, returning a detailed breakdown.
    /// </summary>
    public static PayCalculation Calculate(ShiftInput input)
    {
        var rate = PayRateData.FindRate(input.Classification)
            ?? throw new InvalidOperationException($"No pay rate found for classification: {input.Classification}");

        var rules = new List<string>();
        decimal ordinaryPay = 0m;
        decimal overtimePay = 0m;
        decimal allowancePay = 0m;

        // ---- 1. Minimum engagement ----
        decimal ordinaryHours = input.OrdinaryHours;
        bool minEngagementApplied = false;
        decimal minEngagementHours = 0m;

        bool isWeekend = input.DayType is DayType.Saturday or DayType.Sunday or DayType.PublicHoliday;

        if (input.ApplyMinimumEngagement)
        {
            if (isWeekend && ordinaryHours < 4)
            {
                minEngagementHours = 4m;
                ordinaryHours = 4m;
                minEngagementApplied = true;
                rules.Add("PEN_005: Weekend/public holiday minimum engagement of 4 hours applied.");
            }
            else if (input.EmploymentType is EmploymentType.Casual or EmploymentType.PartTime && ordinaryHours < 2)
            {
                minEngagementHours = 2m;
                ordinaryHours = 2m;
                minEngagementApplied = true;
                rules.Add("EMP_003/EMP_005: Minimum engagement of 2 hours applied.");
            }
        }

        // ---- 2. Determine effective hourly rate for ordinary hours ----
        decimal effectiveHourlyRate = ResolveOrdinaryRate(input, rate, rules);

        ordinaryPay = Math.Round(effectiveHourlyRate * ordinaryHours, 2);

        // ---- 3. Overtime ----
        if (input.OvertimeHoursFirst2 > 0)
        {
            decimal otRate = ResolveOvertimeRate(input, rate, OvertimePeriod.First2Hours, rules);
            overtimePay += Math.Round(otRate * input.OvertimeHoursFirst2, 2);
        }
        if (input.OvertimeHoursAfter2 > 0)
        {
            decimal otRate = ResolveOvertimeRate(input, rate, OvertimePeriod.After2Hours, rules);
            overtimePay += Math.Round(otRate * input.OvertimeHoursAfter2, 2);
        }

        // ---- 4. Allowances ----
        foreach (var code in input.ApplicableAllowanceCodes)
        {
            var al = PayRateData.FindAllowance(code);
            if (al is null) continue;
            if (al.Frequency == AllowanceFrequency.Reimbursement)
            {
                rules.Add($"ALL: {al.Description} – reimbursement (no fixed dollar amount).");
                continue;
            }
            allowancePay += al.Rate;
            rules.Add($"ALL: {al.Description} – ${al.Rate:F2} per {al.Frequency}.");
        }

        decimal total = ordinaryPay + overtimePay + allowancePay;

        return new PayCalculation
        {
            Input = input,
            OrdinaryPay = ordinaryPay,
            OvertimePay = overtimePay,
            AllowancePay = allowancePay,
            TotalPay = total,
            EffectiveHourlyRate = effectiveHourlyRate,
            EffectiveRateLabel = DescribeRate(input),
            MinimumEngagementHours = minEngagementHours,
            MinimumEngagementApplied = minEngagementApplied,
            RulesApplied = rules
        };
    }

    // -----------------------------------------------------------------------
    private static decimal ResolveOrdinaryRate(ShiftInput input, PayRate rate, List<string> rules)
    {
        if (input.EmploymentType == EmploymentType.Casual)
        {
            // Casual rates are pre-loaded in table
            decimal casualBase = rate.CasualHourlyRate > 0 ? rate.CasualHourlyRate : rate.HourlyRate * 1.25m;

            return input.DayType switch
            {
                DayType.Sunday => rate.SundayRate > 0 ? rate.SundayRate * 1.25m : casualBase * 2.00m,
                DayType.PublicHoliday => rate.PublicHolidayRate > 0 ? rate.PublicHolidayRate * 1.25m : casualBase * 2.50m,
                _ => input.ShiftType switch
                {
                    ShiftType.EarlyMorning => GetCasualShiftRate(rate.EarlyMorningShiftRate, casualBase, rules, "EarlyMorning"),
                    ShiftType.AfternoonShift => GetCasualShiftRate(rate.AfternoonShiftRate, casualBase, rules, "Afternoon"),
                    ShiftType.RotatingNight => GetCasualShiftRate(rate.RotatingNightShiftRate, casualBase, rules, "RotatingNight"),
                    ShiftType.PermanentNight => GetCasualShiftRate(rate.PermanentNightShiftRate, casualBase, rules, "PermanentNight"),
                    _ => casualBase
                }
            };
        }

        // Full-time / Part-time
        return input.DayType switch
        {
            DayType.Sunday => AddRule(rate.SundayRate, rules, "PEN_003: Sunday – 200%"),
            DayType.PublicHoliday => AddRule(rate.PublicHolidayRate, rules, "PEN_004: Public holiday – 250%"),
            DayType.Saturday when input.IsShiftworker => AddRule(rate.SaturdayShiftworkerRate, rules, "PEN_002: Saturday shiftworker – 150%"),
            DayType.Saturday => AddRule(rate.HourlyRate, rules, "PEN_001: Saturday – ordinary rate"),
            _ => input.ShiftType switch
            {
                ShiftType.EarlyMorning => AddRule(rate.EarlyMorningShiftRate, rules, "ORD_002: Early morning shift"),
                ShiftType.AfternoonShift => AddRule(rate.AfternoonShiftRate, rules, "ORD_003: Afternoon shift"),
                ShiftType.RotatingNight => AddRule(rate.RotatingNightShiftRate, rules, "ORD_004: Rotating night shift"),
                ShiftType.PermanentNight => AddRule(rate.PermanentNightShiftRate, rules, "ORD_005: Permanent night shift"),
                _ => AddRule(rate.HourlyRate, rules, "Base ordinary time rate")
            }
        };
    }

    private static decimal ResolveOvertimeRate(ShiftInput input, PayRate rate, OvertimePeriod period, List<string> rules)
    {
        bool isSat = input.DayType == DayType.Saturday;
        bool shift = input.IsShiftworker;
        bool first2 = period == OvertimePeriod.First2Hours;

        if (isSat && shift)
            return first2
                ? AddRule(rate.OvertimeSatShiftFirst2Rate, rules, "OVT_005: OT Saturday shiftworker first 2h")
                : AddRule(rate.OvertimeSatShiftAfter2Rate, rules, "OVT_006: OT Saturday shiftworker after 2h");

        if (isSat && !shift)
            return first2
                ? AddRule(rate.OvertimeSatNonShiftFirst2Rate, rules, "OVT_003: OT Saturday non-shift first 2h")
                : AddRule(rate.OvertimeSatNonShiftAfter2Rate, rules, "OVT_004: OT Saturday non-shift after 2h");

        return first2
            ? AddRule(rate.OvertimeMFFirst2Rate, rules, "OVT_001: OT Mon–Fri first 2h")
            : AddRule(rate.OvertimeMFAfter2Rate, rules, "OVT_002: OT Mon–Fri after 2h");
    }

    private static decimal GetCasualShiftRate(decimal tableRate, decimal casualBase, List<string> rules, string label)
    {
        if (tableRate > 0)
        {
            rules.Add($"Casual {label} shift rate from pay table.");
            return tableRate;
        }
        // fall back: casual base (already includes 25% loading)
        rules.Add($"Casual {label} shift rate calculated from casual base.");
        return casualBase;
    }

    private static decimal AddRule(decimal value, List<string> rules, string description)
    {
        rules.Add(description);
        return value;
    }

    private static string DescribeRate(ShiftInput input)
    {
        var et = input.EmploymentType switch
        {
            EmploymentType.FullTime => "FT",
            EmploymentType.PartTime => "PT",
            EmploymentType.Casual => "CAS",
            _ => "?"
        };
        var day = input.DayType.ToString();
        var shift = input.ShiftType == ShiftType.OrdinaryDay ? "" : $" / {input.ShiftType}";
        return $"{et} | {day}{shift}";
    }
}
