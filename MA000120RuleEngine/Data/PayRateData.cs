// ============================================================
//  PayRateData.cs
//  All pay rates from MA000120 Pay Guide effective 01/03/2026
//  Source: Fair Work Ombudsman – Published 31 March 2026
// ============================================================

using MA000120RuleEngine.Models;

namespace MA000120RuleEngine.Data;

public static class PayRateData
{
    // -----------------------------------------------------------------------
    // All classifications keyed by a short code for lookup
    // -----------------------------------------------------------------------
    public static readonly IReadOnlyDictionary<string, Classification> Classifications;

    // -----------------------------------------------------------------------
    // Full pay rate table  (Adult FT/PT + Adult Casual)
    // -----------------------------------------------------------------------
    public static readonly IReadOnlyList<PayRate> Rates;

    // -----------------------------------------------------------------------
    // Allowances
    // -----------------------------------------------------------------------
    public static readonly IReadOnlyList<Allowance> Allowances;

    static PayRateData()
    {
        // ===================================================================
        // CLASSIFICATIONS
        // ===================================================================
        var cls = new List<(string key, Classification c)>
        {
            // -- Support Worker (CSSE) ----------------------------------------
            ("SW_1_1", new Classification { Stream = ClassificationStream.SupportWorker, Name = "Support worker level 1.1 on commencement", Level = 1, PayPoint = 1 }),
            ("SW_2_1", new Classification { Stream = ClassificationStream.SupportWorker, Name = "Support worker level 2.1 on commencement", Level = 2, PayPoint = 1 }),
            ("SW_2_2", new Classification { Stream = ClassificationStream.SupportWorker, Name = "Support worker level 2.2 after 1 year",    Level = 2, PayPoint = 2 }),
            ("SW_3_1", new Classification { Stream = ClassificationStream.SupportWorker, Name = "Support worker level 3.1 on commencement", Level = 3, PayPoint = 1 }),

            // -- Children's Services Employee (CSE) ---------------------------
            ("CSE_L1", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 1 - Introductory educator",  Level = 1, PayPoint = 1 }),
            ("CSE_L2", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 2 - Educator",               Level = 2, PayPoint = 1 }),
            ("CSE_L3", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 3 - Qualified educator",      Level = 3, PayPoint = 1 }),
            ("CSE_L4", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 4 - Experienced educator",    Level = 4, PayPoint = 1 }),
            ("CSE_L5", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 5 - Advanced educator",       Level = 5, PayPoint = 1 }),
            ("CSE_L6", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 6 - Room leader",             Level = 6, PayPoint = 1 }),
            ("CSE_L7", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 7 - Assistant director",      Level = 7, PayPoint = 1 }),
            ("CSE_L8", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 8 - Director",               Level = 8, PayPoint = 1 }),

            // -- Junior CSE - 16 yrs & under ----------------------------------
            ("CSE_L1_J16", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 1 - Introductory educator", Level = 1, JuniorAge = 16 }),
            ("CSE_L2_J16", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 2 - Educator",             Level = 2, JuniorAge = 16 }),
            ("CSE_L1_J17", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 1 - Introductory educator", Level = 1, JuniorAge = 17 }),
            ("CSE_L2_J17", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 2 - Educator",             Level = 2, JuniorAge = 17 }),
            ("CSE_L1_J18", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 1 - Introductory educator", Level = 1, JuniorAge = 18 }),
            ("CSE_L2_J18", new Classification { Stream = ClassificationStream.ChildrensServicesEmployee, Name = "Level 2 - Educator",             Level = 2, JuniorAge = 18 }),

            // -- Apprentice ---------------------------------------------------
            ("APP_1_J16",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Apprentice 1st year",          ApprenticeYear = 1, JuniorAge = 16 }),
            ("APP_2_J16",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Apprentice 2nd+ year",         ApprenticeYear = 2, JuniorAge = 16 }),
            ("APP_1_J17",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Apprentice 1st year",          ApprenticeYear = 1, JuniorAge = 17 }),
            ("APP_2_J17",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Apprentice 2nd+ year",         ApprenticeYear = 2, JuniorAge = 17 }),
            ("APP_1_J18",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Apprentice 1st year",          ApprenticeYear = 1, JuniorAge = 18 }),
            ("APP_2_J18",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Apprentice 2nd+ year",         ApprenticeYear = 2, JuniorAge = 18 }),
            ("APP_1_ADU",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Adult apprentice 1st year",    ApprenticeYear = 1 }),
            ("APP_2_ADU",  new Classification { Stream = ClassificationStream.Apprentice, Name = "Adult apprentice 2nd+ year",   ApprenticeYear = 2 }),
        };

        Classifications = cls.ToDictionary(x => x.key, x => x.c);

        // ===================================================================
        // PAY RATES  (Effective 01/03/2026)
        // ===================================================================
        var rates = new List<PayRate>
        {
            // ---- Support Worker – Full-time / Part-time ----
            new PayRate
            {
                Classification          = Classifications["SW_1_1"],
                WeeklyRate              = 948.00m,
                HourlyRate              = 24.95m,
                SundayRate              = 49.90m,
                PublicHolidayRate       = 62.38m,
                EarlyMorningShiftRate   = 27.45m,
                AfternoonShiftRate      = 28.69m,
                RotatingNightShiftRate  = 29.32m,
                PermanentNightShiftRate = 32.44m,
                SaturdayShiftworkerRate = 37.43m,
                OvertimeMFFirst2Rate    = 37.43m,
                OvertimeMFAfter2Rate    = 49.90m,
                OvertimeSatNonShiftFirst2Rate = 37.43m,
                OvertimeSatNonShiftAfter2Rate = 49.90m,
                OvertimeSatShiftFirst2Rate    = 37.43m,
                OvertimeSatShiftAfter2Rate    = 49.90m,
                CasualHourlyRate        = 31.19m,
            },
            new PayRate
            {
                Classification          = Classifications["SW_2_1"],
                WeeklyRate              = 977.00m,
                HourlyRate              = 25.71m,
                SundayRate              = 51.42m,
                PublicHolidayRate       = 64.28m,
                EarlyMorningShiftRate   = 28.28m,
                AfternoonShiftRate      = 29.57m,
                RotatingNightShiftRate  = 30.21m,
                PermanentNightShiftRate = 33.42m,
                SaturdayShiftworkerRate = 38.57m,
                OvertimeMFFirst2Rate    = 38.57m,
                OvertimeMFAfter2Rate    = 51.42m,
                OvertimeSatNonShiftFirst2Rate = 38.57m,
                OvertimeSatNonShiftAfter2Rate = 51.42m,
                OvertimeSatShiftFirst2Rate    = 38.57m,
                OvertimeSatShiftAfter2Rate    = 51.42m,
                CasualHourlyRate        = 32.14m,
            },
            new PayRate
            {
                Classification          = Classifications["SW_2_2"],
                WeeklyRate              = 1009.10m,
                HourlyRate              = 26.56m,
                SundayRate              = 53.12m,
                PublicHolidayRate       = 66.40m,
                EarlyMorningShiftRate   = 29.22m,
                AfternoonShiftRate      = 30.54m,
                RotatingNightShiftRate  = 31.21m,
                PermanentNightShiftRate = 34.53m,
                SaturdayShiftworkerRate = 39.84m,
                OvertimeMFFirst2Rate    = 39.84m,
                OvertimeMFAfter2Rate    = 53.12m,
                OvertimeSatNonShiftFirst2Rate = 39.84m,
                OvertimeSatNonShiftAfter2Rate = 53.12m,
                OvertimeSatShiftFirst2Rate    = 39.84m,
                OvertimeSatShiftAfter2Rate    = 53.12m,
                CasualHourlyRate        = 33.20m,
            },
            new PayRate
            {
                Classification          = Classifications["SW_3_1"],
                WeeklyRate              = 1068.40m,
                HourlyRate              = 28.12m,
                SundayRate              = 56.24m,
                PublicHolidayRate       = 70.30m,
                EarlyMorningShiftRate   = 30.93m,
                AfternoonShiftRate      = 32.34m,
                RotatingNightShiftRate  = 33.04m,
                PermanentNightShiftRate = 36.56m,
                SaturdayShiftworkerRate = 42.18m,
                OvertimeMFFirst2Rate    = 42.18m,
                OvertimeMFAfter2Rate    = 56.24m,
                OvertimeSatNonShiftFirst2Rate = 42.18m,
                OvertimeSatNonShiftAfter2Rate = 56.24m,
                OvertimeSatShiftFirst2Rate    = 42.18m,
                OvertimeSatShiftAfter2Rate    = 56.24m,
                CasualHourlyRate        = 35.15m,
            },

            // ---- Children's Services Employee – Full-time / Part-time ----
            new PayRate
            {
                Classification          = Classifications["CSE_L1"],
                WeeklyRate              = 995.40m,
                HourlyRate              = 26.19m,
                SundayRate              = 52.38m,
                PublicHolidayRate       = 65.48m,
                EarlyMorningShiftRate   = 28.81m,
                AfternoonShiftRate      = 30.12m,
                RotatingNightShiftRate  = 30.77m,
                PermanentNightShiftRate = 34.05m,
                SaturdayShiftworkerRate = 39.29m,
                OvertimeMFFirst2Rate    = 39.29m,
                OvertimeMFAfter2Rate    = 52.38m,
                OvertimeSatNonShiftFirst2Rate = 39.29m,
                OvertimeSatNonShiftAfter2Rate = 52.38m,
                OvertimeSatShiftFirst2Rate    = 39.29m,
                OvertimeSatShiftAfter2Rate    = 52.38m,
                CasualHourlyRate        = 32.74m,
            },
            new PayRate
            {
                Classification          = Classifications["CSE_L2"],
                WeeklyRate              = 1025.90m,
                HourlyRate              = 27.00m,
                SundayRate              = 54.00m,
                PublicHolidayRate       = 67.50m,
                EarlyMorningShiftRate   = 29.70m,
                AfternoonShiftRate      = 31.05m,
                RotatingNightShiftRate  = 31.73m,
                PermanentNightShiftRate = 35.10m,
                SaturdayShiftworkerRate = 40.50m,
                OvertimeMFFirst2Rate    = 40.50m,
                OvertimeMFAfter2Rate    = 54.00m,
                OvertimeSatNonShiftFirst2Rate = 40.50m,
                OvertimeSatNonShiftAfter2Rate = 54.00m,
                OvertimeSatShiftFirst2Rate    = 40.50m,
                OvertimeSatShiftAfter2Rate    = 54.00m,
                CasualHourlyRate        = 33.75m,
            },
            new PayRate
            {
                Classification          = Classifications["CSE_L3"],
                WeeklyRate              = 1121.80m,
                HourlyRate              = 29.52m,
                SundayRate              = 59.04m,
                PublicHolidayRate       = 73.80m,
                EarlyMorningShiftRate   = 32.47m,
                AfternoonShiftRate      = 33.95m,
                RotatingNightShiftRate  = 34.69m,
                PermanentNightShiftRate = 38.38m,
                SaturdayShiftworkerRate = 44.28m,
                OvertimeMFFirst2Rate    = 44.28m,
                OvertimeMFAfter2Rate    = 59.04m,
                OvertimeSatNonShiftFirst2Rate = 44.28m,
                OvertimeSatNonShiftAfter2Rate = 59.04m,
                OvertimeSatShiftFirst2Rate    = 44.28m,
                OvertimeSatShiftAfter2Rate    = 59.04m,
                CasualHourlyRate        = 36.90m,
            },
            new PayRate
            {
                Classification          = Classifications["CSE_L4"],
                WeeklyRate              = 1197.10m,
                HourlyRate              = 31.50m,
                SundayRate              = 63.00m,
                PublicHolidayRate       = 78.75m,
                EarlyMorningShiftRate   = 34.65m,
                AfternoonShiftRate      = 36.23m,
                RotatingNightShiftRate  = 37.01m,
                PermanentNightShiftRate = 40.95m,
                SaturdayShiftworkerRate = 47.25m,
                OvertimeMFFirst2Rate    = 47.25m,
                OvertimeMFAfter2Rate    = 63.00m,
                OvertimeSatNonShiftFirst2Rate = 47.25m,
                OvertimeSatNonShiftAfter2Rate = 63.00m,
                OvertimeSatShiftFirst2Rate    = 47.25m,
                OvertimeSatShiftAfter2Rate    = 63.00m,
                CasualHourlyRate        = 39.38m,
            },
            new PayRate
            {
                Classification          = Classifications["CSE_L5"],
                WeeklyRate              = 1263.30m,
                HourlyRate              = 33.24m,
                SundayRate              = 66.48m,
                PublicHolidayRate       = 83.10m,
                EarlyMorningShiftRate   = 36.56m,
                AfternoonShiftRate      = 38.23m,
                RotatingNightShiftRate  = 39.06m,
                PermanentNightShiftRate = 43.21m,
                SaturdayShiftworkerRate = 49.86m,
                OvertimeMFFirst2Rate    = 49.86m,
                OvertimeMFAfter2Rate    = 66.48m,
                OvertimeSatNonShiftFirst2Rate = 49.86m,
                OvertimeSatNonShiftAfter2Rate = 66.48m,
                OvertimeSatShiftFirst2Rate    = 49.86m,
                OvertimeSatShiftAfter2Rate    = 66.48m,
                CasualHourlyRate        = 41.55m,
            },
            new PayRate
            {
                Classification          = Classifications["CSE_L6"],
                WeeklyRate              = 1321.50m,
                HourlyRate              = 34.78m,
                SundayRate              = 69.56m,
                PublicHolidayRate       = 86.95m,
                EarlyMorningShiftRate   = 38.26m,
                AfternoonShiftRate      = 40.00m,
                RotatingNightShiftRate  = 40.87m,
                PermanentNightShiftRate = 45.21m,
                SaturdayShiftworkerRate = 52.17m,
                OvertimeMFFirst2Rate    = 52.17m,
                OvertimeMFAfter2Rate    = 69.56m,
                OvertimeSatNonShiftFirst2Rate = 52.17m,
                OvertimeSatNonShiftAfter2Rate = 69.56m,
                OvertimeSatShiftFirst2Rate    = 52.17m,
                OvertimeSatShiftAfter2Rate    = 69.56m,
                CasualHourlyRate        = 43.48m,
            },
            new PayRate
            {
                Classification          = Classifications["CSE_L7"],
                WeeklyRate              = 1381.90m,
                HourlyRate              = 36.37m,
                SundayRate              = 72.74m,
                PublicHolidayRate       = 90.93m,
                EarlyMorningShiftRate   = 40.01m,
                AfternoonShiftRate      = 41.83m,
                RotatingNightShiftRate  = 42.73m,
                PermanentNightShiftRate = 47.28m,
                SaturdayShiftworkerRate = 54.56m,
                OvertimeMFFirst2Rate    = 54.56m,
                OvertimeMFAfter2Rate    = 72.74m,
                OvertimeSatNonShiftFirst2Rate = 54.56m,
                OvertimeSatNonShiftAfter2Rate = 72.74m,
                OvertimeSatShiftFirst2Rate    = 54.56m,
                OvertimeSatShiftAfter2Rate    = 72.74m,
                CasualHourlyRate        = 45.46m,
            },
            new PayRate
            {
                Classification          = Classifications["CSE_L8"],
                WeeklyRate              = 1593.50m,
                HourlyRate              = 41.93m,
                SundayRate              = 83.86m,
                PublicHolidayRate       = 104.83m,
                EarlyMorningShiftRate   = 46.12m,
                AfternoonShiftRate      = 48.22m,
                RotatingNightShiftRate  = 49.27m,
                PermanentNightShiftRate = 54.51m,
                SaturdayShiftworkerRate = 62.90m,
                OvertimeMFFirst2Rate    = 62.90m,
                OvertimeMFAfter2Rate    = 83.86m,
                OvertimeSatNonShiftFirst2Rate = 62.90m,
                OvertimeSatNonShiftAfter2Rate = 83.86m,
                OvertimeSatShiftFirst2Rate    = 62.90m,
                OvertimeSatShiftAfter2Rate    = 83.86m,
                CasualHourlyRate        = 52.41m,
            },

            // ---- Junior CSE – 16 yrs & under (FT/PT) ----
            new PayRate
            {
                Classification = Classifications["CSE_L1_J16"],
                HourlyRate = 18.90m, SundayRate = 37.80m, PublicHolidayRate = 47.25m,
                EarlyMorningShiftRate = 20.79m, AfternoonShiftRate = 21.74m,
                RotatingNightShiftRate = 22.21m, PermanentNightShiftRate = 24.57m,
                SaturdayShiftworkerRate = 28.35m,
                OvertimeMFFirst2Rate = 28.35m, OvertimeMFAfter2Rate = 37.80m,
                OvertimeSatNonShiftFirst2Rate = 28.35m, OvertimeSatNonShiftAfter2Rate = 37.80m,
                OvertimeSatShiftFirst2Rate = 28.35m, OvertimeSatShiftAfter2Rate = 37.80m,
                CasualHourlyRate = 23.63m,
            },
            new PayRate
            {
                Classification = Classifications["CSE_L2_J16"],
                HourlyRate = 18.90m, SundayRate = 37.80m, PublicHolidayRate = 47.25m,
                EarlyMorningShiftRate = 20.79m, AfternoonShiftRate = 21.74m,
                RotatingNightShiftRate = 22.21m, PermanentNightShiftRate = 24.57m,
                SaturdayShiftworkerRate = 28.35m,
                OvertimeMFFirst2Rate = 28.35m, OvertimeMFAfter2Rate = 37.80m,
                OvertimeSatNonShiftFirst2Rate = 28.35m, OvertimeSatNonShiftAfter2Rate = 37.80m,
                OvertimeSatShiftFirst2Rate = 28.35m, OvertimeSatShiftAfter2Rate = 37.80m,
                CasualHourlyRate = 23.63m,
            },

            // ---- Junior CSE – 17 yrs (FT/PT) ----
            new PayRate
            {
                Classification = Classifications["CSE_L1_J17"],
                HourlyRate = 21.60m, SundayRate = 43.20m, PublicHolidayRate = 54.00m,
                EarlyMorningShiftRate = 23.76m, AfternoonShiftRate = 24.84m,
                RotatingNightShiftRate = 25.38m, PermanentNightShiftRate = 28.08m,
                SaturdayShiftworkerRate = 32.40m,
                OvertimeMFFirst2Rate = 32.40m, OvertimeMFAfter2Rate = 43.20m,
                OvertimeSatNonShiftFirst2Rate = 32.40m, OvertimeSatNonShiftAfter2Rate = 43.20m,
                OvertimeSatShiftFirst2Rate = 32.40m, OvertimeSatShiftAfter2Rate = 43.20m,
                CasualHourlyRate = 27.00m,
            },
            new PayRate
            {
                Classification = Classifications["CSE_L2_J17"],
                HourlyRate = 21.60m, SundayRate = 43.20m, PublicHolidayRate = 54.00m,
                EarlyMorningShiftRate = 23.76m, AfternoonShiftRate = 24.84m,
                RotatingNightShiftRate = 25.38m, PermanentNightShiftRate = 28.08m,
                SaturdayShiftworkerRate = 32.40m,
                OvertimeMFFirst2Rate = 32.40m, OvertimeMFAfter2Rate = 43.20m,
                OvertimeSatNonShiftFirst2Rate = 32.40m, OvertimeSatNonShiftAfter2Rate = 43.20m,
                OvertimeSatShiftFirst2Rate = 32.40m, OvertimeSatShiftAfter2Rate = 43.20m,
                CasualHourlyRate = 27.00m,
            },

            // ---- Junior CSE – 18 yrs (FT/PT) ----
            new PayRate
            {
                Classification = Classifications["CSE_L1_J18"],
                HourlyRate = 24.30m, SundayRate = 48.60m, PublicHolidayRate = 60.75m,
                EarlyMorningShiftRate = 26.73m, AfternoonShiftRate = 27.95m,
                RotatingNightShiftRate = 28.55m, PermanentNightShiftRate = 31.59m,
                SaturdayShiftworkerRate = 36.45m,
                OvertimeMFFirst2Rate = 36.45m, OvertimeMFAfter2Rate = 48.60m,
                OvertimeSatNonShiftFirst2Rate = 36.45m, OvertimeSatNonShiftAfter2Rate = 48.60m,
                OvertimeSatShiftFirst2Rate = 36.45m, OvertimeSatShiftAfter2Rate = 48.60m,
                CasualHourlyRate = 30.38m,
            },
            new PayRate
            {
                Classification = Classifications["CSE_L2_J18"],
                HourlyRate = 24.30m, SundayRate = 48.60m, PublicHolidayRate = 60.75m,
                EarlyMorningShiftRate = 26.73m, AfternoonShiftRate = 27.95m,
                RotatingNightShiftRate = 28.55m, PermanentNightShiftRate = 31.59m,
                SaturdayShiftworkerRate = 36.45m,
                OvertimeMFFirst2Rate = 36.45m, OvertimeMFAfter2Rate = 48.60m,
                OvertimeSatNonShiftFirst2Rate = 36.45m, OvertimeSatNonShiftAfter2Rate = 48.60m,
                OvertimeSatShiftFirst2Rate = 36.45m, OvertimeSatShiftAfter2Rate = 48.60m,
                CasualHourlyRate = 30.38m,
            },

            // ---- Apprentice – 16 yrs & under ----
            new PayRate
            {
                Classification = Classifications["APP_1_J16"],
                HourlyRate = 18.90m, SundayRate = 37.80m, PublicHolidayRate = 47.25m,
                EarlyMorningShiftRate = 20.79m, AfternoonShiftRate = 21.74m,
                RotatingNightShiftRate = 22.21m, PermanentNightShiftRate = 24.57m,
                SaturdayShiftworkerRate = 28.35m,
                OvertimeMFFirst2Rate = 28.35m, OvertimeMFAfter2Rate = 37.80m,
                OvertimeSatNonShiftFirst2Rate = 28.35m, OvertimeSatNonShiftAfter2Rate = 37.80m,
                OvertimeSatShiftFirst2Rate = 28.35m, OvertimeSatShiftAfter2Rate = 37.80m,
            },
            new PayRate
            {
                Classification = Classifications["APP_2_J16"],
                HourlyRate = 19.19m, SundayRate = 38.38m, PublicHolidayRate = 47.98m,
                EarlyMorningShiftRate = 21.11m, AfternoonShiftRate = 22.07m,
                RotatingNightShiftRate = 22.55m, PermanentNightShiftRate = 24.95m,
                SaturdayShiftworkerRate = 28.79m,
                OvertimeMFFirst2Rate = 28.79m, OvertimeMFAfter2Rate = 38.38m,
                OvertimeSatNonShiftFirst2Rate = 28.79m, OvertimeSatNonShiftAfter2Rate = 38.38m,
                OvertimeSatShiftFirst2Rate = 28.79m, OvertimeSatShiftAfter2Rate = 38.38m,
            },

            // ---- Apprentice – 17 yrs ----
            new PayRate
            {
                Classification = Classifications["APP_1_J17"],
                HourlyRate = 21.60m, SundayRate = 43.20m, PublicHolidayRate = 54.00m,
                EarlyMorningShiftRate = 23.76m, AfternoonShiftRate = 24.84m,
                RotatingNightShiftRate = 25.38m, PermanentNightShiftRate = 28.08m,
                SaturdayShiftworkerRate = 32.40m,
                OvertimeMFFirst2Rate = 32.40m, OvertimeMFAfter2Rate = 43.20m,
                OvertimeSatNonShiftFirst2Rate = 32.40m, OvertimeSatNonShiftAfter2Rate = 43.20m,
                OvertimeSatShiftFirst2Rate = 32.40m, OvertimeSatShiftAfter2Rate = 43.20m,
            },
            new PayRate
            {
                Classification = Classifications["APP_2_J17"],
                HourlyRate = 21.60m, SundayRate = 43.20m, PublicHolidayRate = 54.00m,
                EarlyMorningShiftRate = 23.76m, AfternoonShiftRate = 24.84m,
                RotatingNightShiftRate = 25.38m, PermanentNightShiftRate = 28.08m,
                SaturdayShiftworkerRate = 32.40m,
                OvertimeMFFirst2Rate = 32.40m, OvertimeMFAfter2Rate = 43.20m,
                OvertimeSatNonShiftFirst2Rate = 32.40m, OvertimeSatNonShiftAfter2Rate = 43.20m,
                OvertimeSatShiftFirst2Rate = 32.40m, OvertimeSatShiftAfter2Rate = 43.20m,
            },

            // ---- Apprentice – 18 yrs ----
            new PayRate
            {
                Classification = Classifications["APP_1_J18"],
                HourlyRate = 24.30m, SundayRate = 48.60m, PublicHolidayRate = 60.75m,
                EarlyMorningShiftRate = 26.73m, AfternoonShiftRate = 27.95m,
                RotatingNightShiftRate = 28.55m, PermanentNightShiftRate = 31.59m,
                SaturdayShiftworkerRate = 36.45m,
                OvertimeMFFirst2Rate = 36.45m, OvertimeMFAfter2Rate = 48.60m,
                OvertimeSatNonShiftFirst2Rate = 36.45m, OvertimeSatNonShiftAfter2Rate = 48.60m,
                OvertimeSatShiftFirst2Rate = 36.45m, OvertimeSatShiftAfter2Rate = 48.60m,
            },
            new PayRate
            {
                Classification = Classifications["APP_2_J18"],
                HourlyRate = 24.30m, SundayRate = 48.60m, PublicHolidayRate = 60.75m,
                EarlyMorningShiftRate = 26.73m, AfternoonShiftRate = 27.95m,
                RotatingNightShiftRate = 28.55m, PermanentNightShiftRate = 31.59m,
                SaturdayShiftworkerRate = 36.45m,
                OvertimeMFFirst2Rate = 36.45m, OvertimeMFAfter2Rate = 48.60m,
                OvertimeSatNonShiftFirst2Rate = 36.45m, OvertimeSatNonShiftAfter2Rate = 48.60m,
                OvertimeSatShiftFirst2Rate = 36.45m, OvertimeSatShiftAfter2Rate = 48.60m,
            },

            // ---- Adult Apprentice ----
            new PayRate
            {
                Classification = Classifications["APP_1_ADU"],
                HourlyRate = 27.00m, SundayRate = 54.00m, PublicHolidayRate = 67.50m,
                EarlyMorningShiftRate = 29.70m, AfternoonShiftRate = 31.05m,
                RotatingNightShiftRate = 31.73m, PermanentNightShiftRate = 35.10m,
                SaturdayShiftworkerRate = 40.50m,
                OvertimeMFFirst2Rate = 40.50m, OvertimeMFAfter2Rate = 54.00m,
                OvertimeSatNonShiftFirst2Rate = 40.50m, OvertimeSatNonShiftAfter2Rate = 54.00m,
                OvertimeSatShiftFirst2Rate = 40.50m, OvertimeSatShiftAfter2Rate = 54.00m,
            },
            new PayRate
            {
                Classification = Classifications["APP_2_ADU"],
                HourlyRate = 27.00m, SundayRate = 54.00m, PublicHolidayRate = 67.50m,
                EarlyMorningShiftRate = 29.70m, AfternoonShiftRate = 31.05m,
                RotatingNightShiftRate = 31.73m, PermanentNightShiftRate = 35.10m,
                SaturdayShiftworkerRate = 40.50m,
                OvertimeMFFirst2Rate = 40.50m, OvertimeMFAfter2Rate = 54.00m,
                OvertimeSatNonShiftFirst2Rate = 40.50m, OvertimeSatNonShiftAfter2Rate = 54.00m,
                OvertimeSatShiftFirst2Rate = 40.50m, OvertimeSatShiftAfter2Rate = 54.00m,
            },
        };

        Rates = rates.AsReadOnly();

        // ===================================================================
        // ALLOWANCES  (Effective 01/03/2026)
        // ===================================================================
        Allowances = new List<Allowance>
        {
            new Allowance { Code = "BROKEN_SHIFT",          Description = "Broken shift allowance",                                      Rate = 20.42m,    Frequency = AllowanceFrequency.PerDay,         Notes = "For each day on which a broken shift is worked" },
            new Allowance { Code = "EXCESS_FARES",          Description = "Excess fares – working away from usual workplace",            Rate = 16.86m,    Frequency = AllowanceFrequency.PerDay },
            new Allowance { Code = "FIRST_AID_L12_STD",     Description = "First aid – Level 1 or 2, not in OSHC",                       Rate = 12.12m,    Frequency = AllowanceFrequency.PerDay },
            new Allowance { Code = "FIRST_AID_L12_OSHC",    Description = "First aid – Level 1 or 2, employed in OSHC",                  Rate = 1.57m,     Frequency = AllowanceFrequency.PerHour },
            new Allowance { Code = "MEAL_OT",               Description = "Meal allowance – overtime",                                   Rate = 15.48m,    Frequency = AllowanceFrequency.PerDay },
            new Allowance { Code = "LAUNDRY_IRON_FT",       Description = "Laundry & ironing – full-time",                              Rate = 9.49m,     Frequency = AllowanceFrequency.PerWeek },
            new Allowance { Code = "LAUNDRY_IRON_PT",       Description = "Laundry & ironing – part-time or casual",                    Rate = 1.90m,     Frequency = AllowanceFrequency.PerDay,         Notes = "Maximum $9.49 per week" },
            new Allowance { Code = "LAUNDRY_NOIRON_FT",     Description = "Laundry (no ironing) – full-time",                           Rate = 5.98m,     Frequency = AllowanceFrequency.PerWeek },
            new Allowance { Code = "LAUNDRY_NOIRON_PT",     Description = "Laundry (no ironing) – part-time or casual",                 Rate = 1.20m,     Frequency = AllowanceFrequency.PerDay,         Notes = "Maximum $5.98 per week" },
            new Allowance { Code = "ED_LEADER_5D",          Description = "Educational leader – 5 days or more per week",               Rate = 4567.31m,  Frequency = AllowanceFrequency.PerYear },
            new Allowance { Code = "ED_LEADER_4D",          Description = "Educational leader – 4 days per week",                       Rate = 3653.84m,  Frequency = AllowanceFrequency.PerYear },
            new Allowance { Code = "ED_LEADER_3D",          Description = "Educational leader – 3 days per week",                       Rate = 2740.38m,  Frequency = AllowanceFrequency.PerYear },
            new Allowance { Code = "ED_LEADER_2D",          Description = "Educational leader – 2 days per week",                       Rate = 1826.92m,  Frequency = AllowanceFrequency.PerYear },
            new Allowance { Code = "ED_LEADER_1D",          Description = "Educational leader – 1 day per week",                        Rate = 913.46m,   Frequency = AllowanceFrequency.PerYear },
            new Allowance { Code = "VEHICLE_CAR",           Description = "Vehicle allowance – motor car",                              Rate = 0.99m,     Frequency = AllowanceFrequency.PerKm },
            new Allowance { Code = "VEHICLE_MOTO",          Description = "Vehicle allowance – motorcycle",                             Rate = 0.33m,     Frequency = AllowanceFrequency.PerKm },
            new Allowance { Code = "APP_TRAINING_FEES",     Description = "Apprentice training fees & textbook costs",                  Rate = 0m,        Frequency = AllowanceFrequency.Reimbursement,  Notes = "Reimbursement of training fees for prescribed courses and prescribed textbooks" },
            new Allowance { Code = "APP_TRAVEL_BLOCK",      Description = "Apprentice travel to block release training",                Rate = 0m,        Frequency = AllowanceFrequency.Reimbursement,  Notes = "Reimbursement of excess reasonable travel costs" },
            new Allowance { Code = "PROTECTIVE_CLOTHING",   Description = "Protective clothing and equipment",                          Rate = 0m,        Frequency = AllowanceFrequency.Reimbursement },
            new Allowance { Code = "SPECIAL_CLOTHING",      Description = "Special clothing reimbursement",                            Rate = 0m,        Frequency = AllowanceFrequency.Reimbursement },
        }.AsReadOnly();
    }

    /// <summary>Finds the pay rate for a given classification (adult FT/PT rates).</summary>
    public static PayRate? FindRate(Classification classification)
        => Rates.FirstOrDefault(r =>
            r.Classification.Stream == classification.Stream &&
            r.Classification.Level == classification.Level &&
            r.Classification.PayPoint == classification.PayPoint &&
            r.Classification.JuniorAge == classification.JuniorAge &&
            r.Classification.ApprenticeYear == classification.ApprenticeYear);

    /// <summary>Finds an allowance by its code.</summary>
    public static Allowance? FindAllowance(string code)
        => Allowances.FirstOrDefault(a => a.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
}
