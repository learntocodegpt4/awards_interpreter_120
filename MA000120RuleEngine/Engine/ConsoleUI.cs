// ============================================================
//  ConsoleUI.cs
//  Interactive console interface for MA000120 Rule Engine
// ============================================================

using MA000120RuleEngine.Data;
using MA000120RuleEngine.Engine;
using MA000120RuleEngine.Models;

namespace MA000120RuleEngine.Engine;

public static class ConsoleUI
{
    private static readonly MA000120RuleEngine Engine = new();

    public static void Run()
    {
        PrintBanner();

        bool running = true;
        while (running)
        {
            PrintMainMenu();
            var choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            running = choice switch
            {
                "1" => RunPayCalculator(),
                "2" => RunRuleEvaluator(),
                "3" => RunDemoScenarios(),
                "4" => ShowAllRates(),
                "5" => ShowAllAllowances(),
                "6" => ShowAllRuleDefinitions(),
                "0" => ExitApp(),
                _ => UnknownOption()
            };
        }
    }

    // -----------------------------------------------------------------------
    // MAIN MENU
    // -----------------------------------------------------------------------
    private static void PrintMainMenu()
    {
        Console.WriteLine();
        Separator('=');
        ColorLine("  MA000120 – Children's Services Award 2010  |  Rule Engine", ConsoleColor.Cyan);
        ColorLine("  Effective: 1 March 2026  |  Fair Work Ombudsman", ConsoleColor.DarkCyan);
        Separator('=');
        Console.WriteLine("  [1]  Pay Calculator (interactive shift entry)");
        Console.WriteLine("  [2]  Rule Evaluator (check which rules apply to a scenario)");
        Console.WriteLine("  [3]  Demo Scenarios (pre-built examples)");
        Console.WriteLine("  [4]  View All Pay Rates");
        Console.WriteLine("  [5]  View All Allowances");
        Console.WriteLine("  [6]  View All Award Rules");
        Console.WriteLine("  [0]  Exit");
        Separator('-');
        Console.Write("  Choice: ");
    }

    // -----------------------------------------------------------------------
    // 1. PAY CALCULATOR
    // -----------------------------------------------------------------------
    private static bool RunPayCalculator()
    {
        ColorLine("\n=== PAY CALCULATOR ===", ConsoleColor.Yellow);

        var cls = PromptClassification();
        if (cls is null) return true;

        var empType = PromptEmploymentType();
        var dayType = PromptDayType();
        var shiftType = PromptShiftType();
        bool shiftworker = dayType == DayType.Saturday && PromptYesNo("Is employee a shiftworker? (y/n): ");

        Console.Write("  Ordinary hours worked: ");
        decimal ordHours = decimal.TryParse(Console.ReadLine(), out var h) ? h : 8m;

        Console.Write("  Overtime hours (first 2 hours, 0 if none): ");
        decimal ot1 = decimal.TryParse(Console.ReadLine(), out var o1) ? o1 : 0m;

        Console.Write("  Overtime hours (after 2 hours, 0 if none): ");
        decimal ot2 = decimal.TryParse(Console.ReadLine(), out var o2) ? o2 : 0m;

        Console.WriteLine("  Allowance codes to apply (comma-separated, blank for none): ");
        ShowAllowanceCodes();
        Console.Write("  >> ");
        var allCodes = Console.ReadLine()?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
                       ?? new List<string>();

        var input = new ShiftInput
        {
            Classification = cls,
            EmploymentType = empType,
            DayType = dayType,
            ShiftType = shiftType,
            IsShiftworker = shiftworker,
            OrdinaryHours = ordHours,
            OvertimeHoursFirst2 = ot1,
            OvertimeHoursAfter2 = ot2,
            ApplicableAllowanceCodes = allCodes
        };

        var result = PayCalculator.Calculate(input);
        PrintPayResult(result);
        PauseForKey();
        return true;
    }

    // -----------------------------------------------------------------------
    // 2. RULE EVALUATOR
    // -----------------------------------------------------------------------
    private static bool RunRuleEvaluator()
    {
        ColorLine("\n=== RULE EVALUATOR ===", ConsoleColor.Yellow);

        var ctx = PromptRuleContext();
        var results = Engine.EvaluateAll(ctx);

        Console.WriteLine();
        Separator('=');
        ColorLine("  TRIGGERED RULES", ConsoleColor.Green);
        Separator('-');
        var triggered = results.Where(r => r.IsTriggered).ToList();
        if (triggered.Count == 0)
            Console.WriteLine("  No rules triggered for this scenario.");
        else
            foreach (var r in triggered)
                PrintRuleResult(r);

        Console.WriteLine();
        ColorLine("  NOT APPLICABLE", ConsoleColor.DarkGray);
        Separator('-');
        var notApplicable = results.Where(r => !r.IsTriggered).ToList();
        Console.WriteLine($"  {notApplicable.Count} rules not triggered (use option [6] to see all).");

        PauseForKey();
        return true;
    }

    // -----------------------------------------------------------------------
    // 3. DEMO SCENARIOS
    // -----------------------------------------------------------------------
    private static bool RunDemoScenarios()
    {
        ColorLine("\n=== DEMO SCENARIOS ===", ConsoleColor.Yellow);
        Console.WriteLine("  [1]  Casual educator (Level 2) working Sunday – 4 hours");
        Console.WriteLine("  [2]  Full-time Room Leader (Level 6) – Monday with 3h overtime");
        Console.WriteLine("  [3]  Part-time Support Worker (Level 1.1) – Public Holiday");
        Console.WriteLine("  [4]  Junior (17) CSE Level 1 – Wednesday afternoon shift");
        Console.WriteLine("  [5]  Director (Level 8) – Saturday shiftworker 8 hours");
        Console.WriteLine("  [6]  Adult apprentice (1st year) – Thursday ordinary shift");
        Console.WriteLine("  [0]  Back");
        Console.Write("  Choice: ");

        var choice = Console.ReadLine()?.Trim();
        Console.WriteLine();

        switch (choice)
        {
            case "1": RunDemo_CasualSunday(); break;
            case "2": RunDemo_FTRoomLeaderOvertime(); break;
            case "3": RunDemo_PTSupportWorkerPH(); break;
            case "4": RunDemo_JuniorAfternoon(); break;
            case "5": RunDemo_DirectorSaturday(); break;
            case "6": RunDemo_Apprentice(); break;
            case "0": return true;
            default: Console.WriteLine("  Unknown option."); break;
        }

        PauseForKey();
        return true;
    }

    private static void RunDemo_CasualSunday()
    {
        ColorLine("DEMO 1: Casual educator (Level 2) – Sunday 4 hours", ConsoleColor.Cyan);
        var input = new ShiftInput
        {
            Classification = PayRateData.Classifications["CSE_L2"],
            EmploymentType = EmploymentType.Casual,
            DayType = DayType.Sunday,
            ShiftType = ShiftType.OrdinaryDay,
            OrdinaryHours = 4,
        };
        PrintPayResult(PayCalculator.Calculate(input));
    }

    private static void RunDemo_FTRoomLeaderOvertime()
    {
        ColorLine("DEMO 2: Full-time Room Leader (Level 6) – Monday 8h + 3h OT", ConsoleColor.Cyan);
        var input = new ShiftInput
        {
            Classification = PayRateData.Classifications["CSE_L6"],
            EmploymentType = EmploymentType.FullTime,
            DayType = DayType.Weekday,
            ShiftType = ShiftType.OrdinaryDay,
            OrdinaryHours = 8,
            OvertimeHoursFirst2 = 2,
            OvertimeHoursAfter2 = 1,
            ApplicableAllowanceCodes = new List<string> { "MEAL_OT" }
        };
        PrintPayResult(PayCalculator.Calculate(input));
    }

    private static void RunDemo_PTSupportWorkerPH()
    {
        ColorLine("DEMO 3: Part-time Support Worker (Level 1.1) – Public Holiday 3 hours", ConsoleColor.Cyan);
        var input = new ShiftInput
        {
            Classification = PayRateData.Classifications["SW_1_1"],
            EmploymentType = EmploymentType.PartTime,
            DayType = DayType.PublicHoliday,
            ShiftType = ShiftType.OrdinaryDay,
            OrdinaryHours = 3,  // will be bumped to 4 by rule
        };
        PrintPayResult(PayCalculator.Calculate(input));
    }

    private static void RunDemo_JuniorAfternoon()
    {
        ColorLine("DEMO 4: Junior (17 yrs) CSE Level 1 – Wednesday afternoon shift 6h", ConsoleColor.Cyan);
        var input = new ShiftInput
        {
            Classification = PayRateData.Classifications["CSE_L1_J17"],
            EmploymentType = EmploymentType.PartTime,
            DayType = DayType.Weekday,
            ShiftType = ShiftType.AfternoonShift,
            OrdinaryHours = 6,
        };
        PrintPayResult(PayCalculator.Calculate(input));
    }

    private static void RunDemo_DirectorSaturday()
    {
        ColorLine("DEMO 5: Director (Level 8) – Saturday shiftworker 8 hours", ConsoleColor.Cyan);
        var input = new ShiftInput
        {
            Classification = PayRateData.Classifications["CSE_L8"],
            EmploymentType = EmploymentType.FullTime,
            DayType = DayType.Saturday,
            ShiftType = ShiftType.OrdinaryDay,
            IsShiftworker = true,
            OrdinaryHours = 8,
            ApplicableAllowanceCodes = new List<string> { "LAUNDRY_IRON_FT" }
        };
        PrintPayResult(PayCalculator.Calculate(input));
    }

    private static void RunDemo_Apprentice()
    {
        ColorLine("DEMO 6: Adult apprentice (1st year) – Thursday 7.5 hours", ConsoleColor.Cyan);
        var input = new ShiftInput
        {
            Classification = PayRateData.Classifications["APP_1_ADU"],
            EmploymentType = EmploymentType.FullTime,
            DayType = DayType.Weekday,
            ShiftType = ShiftType.OrdinaryDay,
            OrdinaryHours = 7.5m,
            ApplicableAllowanceCodes = new List<string> { "APP_TRAINING_FEES" }
        };
        PrintPayResult(PayCalculator.Calculate(input));
    }

    // -----------------------------------------------------------------------
    // 4. SHOW ALL RATES
    // -----------------------------------------------------------------------
    private static bool ShowAllRates()
    {
        ColorLine("\n=== ALL PAY RATES (Effective 01/03/2026) ===", ConsoleColor.Yellow);
        Separator('=');
        Console.WriteLine($"  {"Classification",-45} {"Weekly":>10} {"Hourly FT":>10} {"Casual":>10} {"Sunday":>10} {"PH":>10}");
        Separator('-');

        foreach (var r in PayRateData.Rates)
        {
            string weekly = r.WeeklyRate > 0 ? $"${r.WeeklyRate:F2}" : "n/a";
            string casual = r.CasualHourlyRate > 0 ? $"${r.CasualHourlyRate:F2}" : "n/a";
            string sunday = r.SundayRate > 0 ? $"${r.SundayRate:F2}" : "n/a";
            string ph     = r.PublicHolidayRate > 0 ? $"${r.PublicHolidayRate:F2}" : "n/a";
            Console.WriteLine($"  {r.Classification,-45} {weekly,10} ${r.HourlyRate,9:F2} {casual,10} {sunday,10} {ph,10}");
        }

        PauseForKey();
        return true;
    }

    // -----------------------------------------------------------------------
    // 5. SHOW ALL ALLOWANCES
    // -----------------------------------------------------------------------
    private static bool ShowAllAllowances()
    {
        ColorLine("\n=== ALL ALLOWANCES (Effective 01/03/2026) ===", ConsoleColor.Yellow);
        Separator('=');
        Console.WriteLine($"  {"Code",-25} {"Rate":>12} {"Freq",-15} Description");
        Separator('-');

        foreach (var a in PayRateData.Allowances)
        {
            string rate = a.Frequency == AllowanceFrequency.Reimbursement ? "Reimburse" : $"${a.Rate:F2}";
            Console.WriteLine($"  {a.Code,-25} {rate,12} {a.Frequency,-15} {a.Description}");
            if (a.Notes is not null)
                Console.WriteLine($"  {"",25}  {"",12} {"",15} NOTE: {a.Notes}");
        }

        PauseForKey();
        return true;
    }

    // -----------------------------------------------------------------------
    // 6. SHOW ALL RULE DEFINITIONS
    // -----------------------------------------------------------------------
    private static bool ShowAllRuleDefinitions()
    {
        ColorLine("\n=== ALL AWARD RULES ===", ConsoleColor.Yellow);
        Console.WriteLine($"  Total rules: {AwardRules.Rules.Count}");
        Separator('=');

        foreach (var rule in AwardRules.Rules)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  [{rule.RuleId}] ");
            Console.ResetColor();
            Console.WriteLine(rule.Description);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"    Clause  : {rule.ClauseReference}");
            Console.WriteLine($"    Cond    : {rule.Condition}");
            if (rule.ValueExpression is not null)
                Console.WriteLine($"    Value   : {rule.ValueExpression}");
            Console.ResetColor();
            Console.WriteLine($"    Outcome : {rule.Outcome}");
            Console.WriteLine();
        }

        PauseForKey();
        return true;
    }

    // -----------------------------------------------------------------------
    // PROMPT HELPERS
    // -----------------------------------------------------------------------
    private static Classification? PromptClassification()
    {
        Console.WriteLine("  Select classification:");
        var keys = PayRateData.Classifications.Keys.ToList();
        for (int i = 0; i < keys.Count; i++)
            Console.WriteLine($"  [{i + 1,2}] {PayRateData.Classifications[keys[i]]}");

        Console.Write("  Choice (0 to cancel): ");
        if (!int.TryParse(Console.ReadLine(), out int idx) || idx == 0 || idx > keys.Count) return null;
        return PayRateData.Classifications[keys[idx - 1]];
    }

    private static EmploymentType PromptEmploymentType()
    {
        Console.WriteLine("  Employment type: [1] Full-time  [2] Part-time  [3] Casual");
        Console.Write("  Choice: ");
        return Console.ReadLine()?.Trim() switch
        {
            "2" => EmploymentType.PartTime,
            "3" => EmploymentType.Casual,
            _ => EmploymentType.FullTime
        };
    }

    private static DayType PromptDayType()
    {
        Console.WriteLine("  Day type: [1] Weekday  [2] Saturday  [3] Sunday  [4] Public holiday");
        Console.Write("  Choice: ");
        return Console.ReadLine()?.Trim() switch
        {
            "2" => DayType.Saturday,
            "3" => DayType.Sunday,
            "4" => DayType.PublicHoliday,
            _ => DayType.Weekday
        };
    }

    private static ShiftType PromptShiftType()
    {
        Console.WriteLine("  Shift: [1] Ordinary day  [2] Early morning  [3] Afternoon  [4] Rotating night  [5] Permanent night");
        Console.Write("  Choice: ");
        return Console.ReadLine()?.Trim() switch
        {
            "2" => ShiftType.EarlyMorning,
            "3" => ShiftType.AfternoonShift,
            "4" => ShiftType.RotatingNight,
            "5" => ShiftType.PermanentNight,
            _ => ShiftType.OrdinaryDay
        };
    }

    private static RuleContext PromptRuleContext()
    {
        ColorLine("  Build a rule evaluation context:", ConsoleColor.Gray);

        var cls = PromptClassification() ?? PayRateData.Classifications["CSE_L2"];
        var rate = PayRateData.FindRate(cls);

        var ctx = new RuleContext
        {
            Stream = cls.Stream.ToString(),
            Level = cls.Level,
            PayPoint = cls.PayPoint,
            IsJunior = cls.JuniorAge > 0,
            JuniorAge = cls.JuniorAge,
            ApprenticeYear = cls.ApprenticeYear,
            BaseHourlyRate = rate?.HourlyRate ?? 0m,
            CasualHourlyRate = rate?.CasualHourlyRate ?? 0m,
        };

        ctx.EmploymentType = PromptEmploymentType().ToString();
        ctx.DayType = PromptDayType().ToString();
        ctx.ShiftType = PromptShiftType().ToString();

        Console.Write("  Ordinary hours: ");
        ctx.OrdinaryHours = decimal.TryParse(Console.ReadLine(), out var oh) ? oh : 8m;

        Console.Write("  Overtime hours total (0 if none): ");
        ctx.OvertimeHours = decimal.TryParse(Console.ReadLine(), out var ovt) ? ovt : 0m;
        ctx.OvertimePeriod = ctx.OvertimeHours > 0 ? "First2Hours" : "None";

        ctx.IsShiftworker = PromptYesNo("  Shiftworker? (y/n): ");
        ctx.IsBrokenShift = PromptYesNo("  Broken shift? (y/n): ");
        ctx.HasFirstAidCert = PromptYesNo("  First aid cert holder? (y/n): ");
        ctx.IsOSHC = PromptYesNo("  OSHC setting? (y/n): ");
        ctx.IsEducationalLeader = PromptYesNo("  Educational leader? (y/n): ");
        if (ctx.IsEducationalLeader)
        {
            Console.Write("  Days per week as educational leader: ");
            ctx.EducationalLeaderDaysPerWeek = int.TryParse(Console.ReadLine(), out var d) ? d : 5;
        }

        Console.Write("  Years of service (for notice rules): ");
        ctx.YearsOfService = decimal.TryParse(Console.ReadLine(), out var ys) ? ys : 0m;

        Console.Write("  Employee age (for notice rules): ");
        ctx.Age = int.TryParse(Console.ReadLine(), out var age) ? age : 30;

        ctx.IsOnAnnualLeave = false;
        ctx.RequiresUniform = false;
        ctx.ShiftStart = 8;
        ctx.ShiftEnd = 16;

        return ctx;
    }

    private static bool PromptYesNo(string prompt)
    {
        Console.Write($"  {prompt}");
        return (Console.ReadLine()?.Trim().ToLower() ?? "") == "y";
    }

    private static void ShowAllowanceCodes()
    {
        Console.WriteLine("  Available codes:");
        foreach (var a in PayRateData.Allowances)
            Console.WriteLine($"    {a.Code,-25} {a.Description}");
    }

    // -----------------------------------------------------------------------
    // OUTPUT HELPERS
    // -----------------------------------------------------------------------
    private static void PrintPayResult(PayCalculation r)
    {
        Console.WriteLine();
        Separator('=');
        ColorLine("  PAY CALCULATION RESULT", ConsoleColor.Green);
        Separator('-');
        Console.WriteLine($"  Classification : {r.Input.Classification}");
        Console.WriteLine($"  Employment     : {r.Input.EmploymentType}");
        Console.WriteLine($"  Day / Shift    : {r.Input.DayType} / {r.Input.ShiftType}");
        Console.WriteLine($"  Rate type      : {r.EffectiveRateLabel}");
        Console.WriteLine($"  Eff. hrly rate : ${r.EffectiveHourlyRate:F2}");

        if (r.MinimumEngagementApplied)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Min. engagement: {r.MinimumEngagementHours:F1} hrs applied");
            Console.ResetColor();
        }

        Separator('-');
        Console.WriteLine($"  Ordinary hours : {r.Input.OrdinaryHours:F2} h  →  ${r.OrdinaryPay:F2}");

        if (r.OvertimePay > 0)
        {
            Console.WriteLine($"  OT first 2 hrs : {r.Input.OvertimeHoursFirst2:F2} h  →  included");
            Console.WriteLine($"  OT after 2 hrs : {r.Input.OvertimeHoursAfter2:F2} h  →  included");
            Console.WriteLine($"  Overtime total : ${r.OvertimePay:F2}");
        }

        if (r.AllowancePay > 0)
            Console.WriteLine($"  Allowances     : ${r.AllowancePay:F2}");

        Separator('-');
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  TOTAL GROSS PAY: ${r.TotalPay:F2}");
        Console.ResetColor();
        Separator('-');

        Console.WriteLine("  Rules applied:");
        foreach (var rule in r.RulesApplied)
            Console.WriteLine($"    ✓ {rule}");

        Separator('=');
    }

    private static void PrintRuleResult(RuleEvaluationResult r)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"  ✓ [{r.Rule.RuleId}] ");
        Console.ResetColor();
        Console.WriteLine(r.Rule.Description);
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"    Ref: {r.Rule.ClauseReference}");
        Console.ResetColor();
        Console.WriteLine($"    {r.Outcome}");
        if (r.Value.HasValue)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"    Value: ${r.Value:F2}");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    private static void PrintBanner()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
  ╔══════════════════════════════════════════════════════════════════╗
  ║    MA000120 – Children's Services Award 2010                    ║
  ║    Rule Engine powered by DynamicExpresso                       ║
  ║    Pay Guide Effective: 1 March 2026                            ║
  ║    Fair Work Ombudsman – fairwork.gov.au                        ║
  ╠══════════════════════════════════════════════════════════════════╣
  ║  DISCLAIMER: This tool is for reference only.                   ║
  ║  Always verify against the official award at fwc.gov.au.        ║
  ╚══════════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    private static bool ExitApp()
    {
        Console.WriteLine("  Goodbye.");
        return false;
    }

    private static bool UnknownOption()
    {
        ColorLine("  Unknown option. Please try again.", ConsoleColor.Red);
        return true;
    }

    private static void PauseForKey()
    {
        Console.WriteLine();
        Console.Write("  Press any key to continue...");
        Console.ReadKey(intercept: true);
        Console.WriteLine();
    }

    private static void Separator(char ch = '-') =>
        Console.WriteLine(new string(ch, 70));

    private static void ColorLine(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
