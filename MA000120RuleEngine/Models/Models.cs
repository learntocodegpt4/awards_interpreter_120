// ============================================================
//  Models.cs – All domain models for MA000120 Rule Engine
// ============================================================

namespace MA000120RuleEngine.Models;

// ---------------------------------------------------------------------------
// Enumerations
// ---------------------------------------------------------------------------

public enum EmploymentType
{
    FullTime,
    PartTime,
    Casual
}

public enum ClassificationStream
{
    ChildrensServicesEmployee,   // CSE
    SupportWorker,               // CSSE
    Apprentice
}

public enum ShiftType
{
    OrdinaryDay,        // 6:00 am – 6:30 pm
    EarlyMorning,       // starts before 6:00 am
    AfternoonShift,     // finishes after 6:30 pm
    RotatingNight,
    PermanentNight
}

public enum DayType
{
    Weekday,
    Saturday,
    Sunday,
    PublicHoliday
}

public enum OvertimePeriod
{
    None,
    First2Hours,
    After2Hours
}

// ---------------------------------------------------------------------------
// Classification
// ---------------------------------------------------------------------------

/// <summary>
/// Represents a classification level/pay-point in the award.
/// Level e.g. "Level 3 - Qualified educator", PayPoint e.g. "3.1"
/// </summary>
public sealed class Classification
{
    public ClassificationStream Stream { get; init; }

    /// <summary>Friendly display name as per the pay guide.</summary>
    public string Name { get; init; } = "";

    /// <summary>Numeric level (1-8 for CSE, 1-3 for SW, 1-2 for Apprentice).</summary>
    public int Level { get; init; }

    /// <summary>Pay-point within the level (e.g. 1 = on commencement, 2 = after 1 year).</summary>
    public int PayPoint { get; init; }

    /// <summary>True when this is a legacy "A" classification (e.g. 3A, 4A).</summary>
    public bool IsALevel { get; init; }

    /// <summary>For apprentices: year of apprenticeship (1 or 2+).</summary>
    public int ApprenticeYear { get; init; }

    /// <summary>For junior employees: age in years (0 = adult).</summary>
    public int JuniorAge { get; init; }   // 0 = adult, 16, 17, 18

    public override string ToString() =>
        JuniorAge > 0 ? $"{Name} (Junior {JuniorAge} yrs)" :
        ApprenticeYear > 0 ? $"{Name} (App Yr {ApprenticeYear})" :
        Name;
}

// ---------------------------------------------------------------------------
// Rates held in the pay table
// ---------------------------------------------------------------------------

public sealed class PayRate
{
    public Classification Classification { get; init; } = null!;

    // Adult Full-time / Part-time
    public decimal WeeklyRate { get; init; }
    public decimal HourlyRate { get; init; }

    // Penalties as hourly dollar amounts (pre-calculated in pay guide)
    public decimal SundayRate { get; init; }
    public decimal PublicHolidayRate { get; init; }
    public decimal EarlyMorningShiftRate { get; init; }
    public decimal AfternoonShiftRate { get; init; }
    public decimal RotatingNightShiftRate { get; init; }
    public decimal PermanentNightShiftRate { get; init; }
    public decimal SaturdayShiftworkerRate { get; init; }

    // Overtime
    public decimal OvertimeMFFirst2Rate { get; init; }
    public decimal OvertimeMFAfter2Rate { get; init; }
    public decimal OvertimeSatNonShiftFirst2Rate { get; init; }
    public decimal OvertimeSatNonShiftAfter2Rate { get; init; }
    public decimal OvertimeSatShiftFirst2Rate { get; init; }
    public decimal OvertimeSatShiftAfter2Rate { get; init; }

    // Casual loading (25% on base) – set for casual rows
    public decimal CasualHourlyRate { get; init; }
}

// ---------------------------------------------------------------------------
// Allowance
// ---------------------------------------------------------------------------

public enum AllowanceFrequency { PerDay, PerHour, PerWeek, PerKm, PerYear, Reimbursement }

public sealed class Allowance
{
    public string Code { get; init; } = "";
    public string Description { get; init; } = "";
    public decimal Rate { get; init; }
    public AllowanceFrequency Frequency { get; init; }
    public string? Notes { get; init; }
}

// ---------------------------------------------------------------------------
// Shift / Pay Calculation inputs & outputs
// ---------------------------------------------------------------------------

public sealed class ShiftInput
{
    public Classification Classification { get; set; } = null!;
    public EmploymentType EmploymentType { get; set; }
    public DayType DayType { get; set; }
    public ShiftType ShiftType { get; set; }
    public bool IsShiftworker { get; set; }
    public decimal OrdinaryHours { get; set; }
    public decimal OvertimeHoursFirst2 { get; set; }
    public decimal OvertimeHoursAfter2 { get; set; }
    public bool ApplyMinimumEngagement { get; set; } = true;
    public List<string> ApplicableAllowanceCodes { get; set; } = new();
}

public sealed class PayCalculation
{
    public ShiftInput Input { get; init; } = null!;
    public decimal OrdinaryPay { get; init; }
    public decimal OvertimePay { get; init; }
    public decimal AllowancePay { get; init; }
    public decimal TotalPay { get; init; }
    public string EffectiveRateLabel { get; init; } = "";
    public decimal EffectiveHourlyRate { get; init; }
    public decimal MinimumEngagementHours { get; init; }
    public bool MinimumEngagementApplied { get; init; }
    public List<string> RulesApplied { get; init; } = new();
}

// ---------------------------------------------------------------------------
// Rule definition (used in DynamicExpresso evaluation)
// ---------------------------------------------------------------------------

public sealed class AwardRule
{
    public string RuleId { get; init; } = "";
    public string Description { get; init; } = "";
    public string ClauseReference { get; init; } = "";

    /// <summary>DynamicExpresso expression string; must return bool.</summary>
    public string Condition { get; init; } = "";

    /// <summary>Human-readable outcome when condition is true.</summary>
    public string Outcome { get; init; } = "";

    /// <summary>Optional DynamicExpresso expression that returns a decimal modifier/amount.</summary>
    public string? ValueExpression { get; init; }
}

public sealed class RuleEvaluationResult
{
    public AwardRule Rule { get; init; } = null!;
    public bool IsTriggered { get; init; }
    public string Outcome { get; init; } = "";
    public decimal? Value { get; init; }
}
