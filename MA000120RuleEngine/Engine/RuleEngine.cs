// ============================================================
//  RuleEngine.cs
//  Evaluates MA000120 award rules using DynamicExpresso
// ============================================================

using DynamicExpresso;
using MA000120RuleEngine.Data;
using MA000120RuleEngine.Models;

namespace MA000120RuleEngine.Engine;

/// <summary>
/// Context object passed as variables to DynamicExpresso expressions.
/// Each property maps to a variable name used in <see cref="AwardRule.Condition"/>.
/// </summary>
public sealed class RuleContext
{
    // Classification
    public string Stream { get; set; } = "";
    public int Level { get; set; }
    public int PayPoint { get; set; }
    public bool IsJunior { get; set; }
    public int JuniorAge { get; set; }
    public int ApprenticeYear { get; set; }

    // Employment
    public string EmploymentType { get; set; } = "";
    public decimal OrdinaryHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public string OvertimePeriod { get; set; } = "None";
    public bool IsShiftworker { get; set; }
    public bool IsBrokenShift { get; set; }
    public decimal TotalShiftHours { get; set; }
    public decimal HoursBetweenShifts { get; set; }
    public bool NoOvertimeNotice { get; set; }

    // Shift timing
    public string ShiftType { get; set; } = "OrdinaryDay";
    public string DayType { get; set; } = "Weekday";
    public int ShiftStart { get; set; }   // hour (0-23)
    public int ShiftEnd { get; set; }     // hour (0-23)

    // Allowance flags
    public bool IsWorkingAwayFromUsualPlace { get; set; }
    public bool HasFirstAidCert { get; set; }
    public bool IsOSHC { get; set; }
    public bool RequiresUniform { get; set; }
    public bool RequiresUniformNoIroning { get; set; }
    public bool IsEducationalLeader { get; set; }
    public int EducationalLeaderDaysPerWeek { get; set; }
    public bool UsesOwnCar { get; set; }
    public bool UsesOwnMotorcycle { get; set; }
    public decimal KilometresTravelled { get; set; }
    public bool HasBlockReleaseTravel { get; set; }

    // Cook
    public bool IsCook { get; set; }
    public bool HasECECQualification { get; set; }

    // Leave
    public bool IsOnAnnualLeave { get; set; }

    // Notice
    public decimal YearsOfService { get; set; }
    public int Age { get; set; }

    // Rates (populated from pay table before rule evaluation)
    public decimal BaseHourlyRate { get; set; }
    public decimal CasualHourlyRate { get; set; }
}

public sealed class MA000120RuleEngine
{
    private readonly Interpreter _interpreter;

    public MA000120RuleEngine()
    {
        _interpreter = new Interpreter();
    }

    /// <summary>
    /// Evaluates all award rules against the given context and returns triggered rules.
    /// </summary>
    public List<RuleEvaluationResult> EvaluateAll(RuleContext context)
    {
        var results = new List<RuleEvaluationResult>();
        var vars = BuildVariables(context);

        foreach (var rule in AwardRules.Rules)
        {
            try
            {
                bool triggered = (bool)_interpreter.Eval(rule.Condition, vars);

                decimal? value = null;
                if (triggered && rule.ValueExpression is not null)
                {
                    try
                    {
                        var raw = _interpreter.Eval(rule.ValueExpression, vars);
                        value = Convert.ToDecimal(raw);
                    }
                    catch
                    {
                        value = null;
                    }
                }

                results.Add(new RuleEvaluationResult
                {
                    Rule = rule,
                    IsTriggered = triggered,
                    Outcome = triggered ? rule.Outcome : $"[NOT APPLICABLE] {rule.Description}",
                    Value = value
                });
            }
            catch (Exception ex)
            {
                results.Add(new RuleEvaluationResult
                {
                    Rule = rule,
                    IsTriggered = false,
                    Outcome = $"[EVAL ERROR] {ex.Message}",
                    Value = null
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Evaluates rules matching a specific rule ID or prefix (e.g. "OVT" for all overtime rules).
    /// </summary>
    public List<RuleEvaluationResult> EvaluateByPrefix(RuleContext context, string prefix)
    {
        var all = EvaluateAll(context);
        return all.Where(r => r.Rule.RuleId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Builds the DynamicExpresso parameter array from the context object.
    /// </summary>
    private static Parameter[] BuildVariables(RuleContext ctx) =>
    [
        new Parameter("Stream",                     ctx.Stream),
        new Parameter("Level",                      ctx.Level),
        new Parameter("PayPoint",                   ctx.PayPoint),
        new Parameter("IsJunior",                   ctx.IsJunior),
        new Parameter("JuniorAge",                  ctx.JuniorAge),
        new Parameter("ApprenticeYear",              ctx.ApprenticeYear),
        new Parameter("EmploymentType",              ctx.EmploymentType),
        new Parameter("OrdinaryHours",               ctx.OrdinaryHours),
        new Parameter("OvertimeHours",               ctx.OvertimeHours),
        new Parameter("OvertimePeriod",              ctx.OvertimePeriod),
        new Parameter("IsShiftworker",               ctx.IsShiftworker),
        new Parameter("IsBrokenShift",               ctx.IsBrokenShift),
        new Parameter("TotalShiftHours",             ctx.TotalShiftHours),
        new Parameter("HoursBetweenShifts",          ctx.HoursBetweenShifts),
        new Parameter("NoOvertimeNotice",            ctx.NoOvertimeNotice),
        new Parameter("ShiftType",                   ctx.ShiftType),
        new Parameter("DayType",                     ctx.DayType),
        new Parameter("ShiftStart",                  ctx.ShiftStart),
        new Parameter("ShiftEnd",                    ctx.ShiftEnd),
        new Parameter("IsWorkingAwayFromUsualPlace", ctx.IsWorkingAwayFromUsualPlace),
        new Parameter("HasFirstAidCert",             ctx.HasFirstAidCert),
        new Parameter("IsOSHC",                      ctx.IsOSHC),
        new Parameter("RequiresUniform",             ctx.RequiresUniform),
        new Parameter("RequiresUniformNoIroning",    ctx.RequiresUniformNoIroning),
        new Parameter("IsEducationalLeader",         ctx.IsEducationalLeader),
        new Parameter("EducationalLeaderDaysPerWeek",ctx.EducationalLeaderDaysPerWeek),
        new Parameter("UsesOwnCar",                  ctx.UsesOwnCar),
        new Parameter("UsesOwnMotorcycle",           ctx.UsesOwnMotorcycle),
        new Parameter("KilometresTravelled",         ctx.KilometresTravelled),
        new Parameter("HasBlockReleaseTravel",       ctx.HasBlockReleaseTravel),
        new Parameter("IsCook",                      ctx.IsCook),
        new Parameter("HasECECQualification",        ctx.HasECECQualification),
        new Parameter("IsOnAnnualLeave",             ctx.IsOnAnnualLeave),
        new Parameter("YearsOfService",              ctx.YearsOfService),
        new Parameter("Age",                         ctx.Age),
        new Parameter("BaseHourlyRate",              ctx.BaseHourlyRate),
        new Parameter("CasualHourlyRate",            ctx.CasualHourlyRate),
    ];
}
