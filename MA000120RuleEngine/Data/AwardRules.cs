// ============================================================
//  AwardRules.cs
//  All MA000120 award rules as DynamicExpresso expressions
//  Clause references are to Children's Services Award 2010
// ============================================================

using MA000120RuleEngine.Models;

namespace MA000120RuleEngine.Data;

public static class AwardRules
{
    public static readonly IReadOnlyList<AwardRule> Rules = new List<AwardRule>
    {
        // ----------------------------------------------------------------
        // COVERAGE RULES (Clauses 3–4)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "COV_001",
            Description = "Award covers children's services and ECEC employers",
            ClauseReference = "Clause 4.1",
            Condition = "Stream == \"ChildrensServicesEmployee\" || Stream == \"SupportWorker\" || Stream == \"Apprentice\"",
            Outcome = "Employee is covered by MA000120 – Children's Services Award 2010."
        },
        new AwardRule
        {
            RuleId = "COV_002",
            Description = "Junior CSE Level 3, 4 or 5 must be paid adult rate",
            ClauseReference = "Clause 15.3",
            Condition = "Stream == \"ChildrensServicesEmployee\" && IsJunior && Level >= 3",
            Outcome = "Junior employee at CSE Level 3+ must be paid at the appropriate adult rate."
        },
        new AwardRule
        {
            RuleId = "COV_003",
            Description = "Support workers at any level must be paid adult rate (even if junior)",
            ClauseReference = "Clause 15.3",
            Condition = "Stream == \"SupportWorker\" && IsJunior",
            Outcome = "Junior support worker at any level must be paid at the appropriate adult rate."
        },

        // ----------------------------------------------------------------
        // EMPLOYMENT TYPE RULES (Clause 10)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "EMP_001",
            Description = "Full-time employee works an average of 38 ordinary hours per week",
            ClauseReference = "Clause 10.1",
            Condition = "EmploymentType == \"FullTime\"",
            Outcome = "Full-time: 38 ordinary hours/week. Ordinary hours between 6:00 am and 6:30 pm."
        },
        new AwardRule
        {
            RuleId = "EMP_002",
            Description = "Part-time employee works fewer than 38 ordinary hours per week",
            ClauseReference = "Clause 10.2",
            Condition = "EmploymentType == \"PartTime\"",
            Outcome = "Part-time: Agreed pattern; max 8 hours/day. Minimum engagement 2 continuous hours per shift."
        },
        new AwardRule
        {
            RuleId = "EMP_003",
            Description = "Casual employee minimum engagement is 2 hours",
            ClauseReference = "Clause 10.3",
            Condition = "EmploymentType == \"Casual\" && OrdinaryHours < 2",
            Outcome = "Casual minimum engagement is 2 hours – hours must be rounded up to 2.",
            ValueExpression = "2"
        },
        new AwardRule
        {
            RuleId = "EMP_004",
            Description = "Casual loading of 25% applies to all ordinary hours",
            ClauseReference = "Clause 10.3",
            Condition = "EmploymentType == \"Casual\"",
            Outcome = "Casual loading of 25% applies to ordinary-time rates.",
            ValueExpression = "BaseHourlyRate * 0.25m"
        },
        new AwardRule
        {
            RuleId = "EMP_005",
            Description = "Part-time minimum engagement 2 continuous hours per shift",
            ClauseReference = "Clause 10.2",
            Condition = "EmploymentType == \"PartTime\" && OrdinaryHours < 2",
            Outcome = "Part-time minimum engagement is 2 hours – hours must be rounded up to 2.",
            ValueExpression = "2"
        },
        new AwardRule
        {
            RuleId = "EMP_006",
            Description = "Maximum ordinary hours per day – full-time and part-time",
            ClauseReference = "Clause 13.1",
            Condition = "(EmploymentType == \"FullTime\" || EmploymentType == \"PartTime\") && OrdinaryHours > 8",
            Outcome = "Hours exceeding 8 in a day are overtime for full-time and part-time employees."
        },
        new AwardRule
        {
            RuleId = "EMP_007",
            Description = "Broken shift – maximum hours in a day (including both shifts)",
            ClauseReference = "Clause 13.3",
            Condition = "IsBrokenShift && TotalShiftHours > 12",
            Outcome = "Broken shift total hours must not exceed 12 hours in a day."
        },

        // ----------------------------------------------------------------
        // ORDINARY HOURS / SPAN (Clauses 13–14)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "ORD_001",
            Description = "Ordinary hours must fall within the spread of hours",
            ClauseReference = "Clause 14.1",
            Condition = "ShiftStart >= 6 && ShiftEnd <= 18",  // 6:00am–6:30pm (approx)
            Outcome = "Ordinary hours fall within the standard spread of hours (6:00 am – 6:30 pm)."
        },
        new AwardRule
        {
            RuleId = "ORD_002",
            Description = "Early morning shift – work commencing before 6:00 am",
            ClauseReference = "Clause 23.3",
            Condition = "ShiftType == \"EarlyMorning\"",
            Outcome = "Early morning shift allowance applies (15% loading on base hourly rate).",
            ValueExpression = "BaseHourlyRate * 0.15m"
        },
        new AwardRule
        {
            RuleId = "ORD_003",
            Description = "Afternoon shift – finishes after 6:30 pm and at or before midnight",
            ClauseReference = "Clause 23.3",
            Condition = "ShiftType == \"AfternoonShift\"",
            Outcome = "Afternoon shift allowance applies (15% loading on base hourly rate).",
            ValueExpression = "BaseHourlyRate * 0.15m"
        },
        new AwardRule
        {
            RuleId = "ORD_004",
            Description = "Rotating night shift",
            ClauseReference = "Clause 23.4",
            Condition = "ShiftType == \"RotatingNight\"",
            Outcome = "Rotating night shift allowance applies (17.5% loading on base hourly rate).",
            ValueExpression = "BaseHourlyRate * 0.175m"
        },
        new AwardRule
        {
            RuleId = "ORD_005",
            Description = "Permanent night shift",
            ClauseReference = "Clause 23.4",
            Condition = "ShiftType == \"PermanentNight\"",
            Outcome = "Permanent night shift allowance applies (30% loading on base hourly rate).",
            ValueExpression = "BaseHourlyRate * 0.30m"
        },

        // ----------------------------------------------------------------
        // WEEKEND & PUBLIC HOLIDAY PENALTIES (Clauses 25–26)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "PEN_001",
            Description = "Saturday – non-shiftworker ordinary rate (no penalty)",
            ClauseReference = "Clause 25.1",
            Condition = "DayType == \"Saturday\" && !IsShiftworker",
            Outcome = "Saturday work for non-shiftworkers is paid at ordinary time rate (no penalty)."
        },
        new AwardRule
        {
            RuleId = "PEN_002",
            Description = "Saturday – shiftworker penalty (150%)",
            ClauseReference = "Clause 25.2",
            Condition = "DayType == \"Saturday\" && IsShiftworker",
            Outcome = "Saturday shiftworker rate applies (150% of base hourly rate).",
            ValueExpression = "BaseHourlyRate * 1.50m"
        },
        new AwardRule
        {
            RuleId = "PEN_003",
            Description = "Sunday – double time (200%)",
            ClauseReference = "Clause 25.3",
            Condition = "DayType == \"Sunday\"",
            Outcome = "Sunday rate applies: 200% of base hourly rate (double time).",
            ValueExpression = "BaseHourlyRate * 2.00m"
        },
        new AwardRule
        {
            RuleId = "PEN_004",
            Description = "Public holiday – double time and a half (250%)",
            ClauseReference = "Clause 26.1",
            Condition = "DayType == \"PublicHoliday\"",
            Outcome = "Public holiday rate applies: 250% of base hourly rate (double time and a half).",
            ValueExpression = "BaseHourlyRate * 2.50m"
        },
        new AwardRule
        {
            RuleId = "PEN_005",
            Description = "Weekend/public holiday minimum engagement – 4 hours",
            ClauseReference = "Clause 25.4",
            Condition = "(DayType == \"Saturday\" || DayType == \"Sunday\" || DayType == \"PublicHoliday\") && OrdinaryHours < 4",
            Outcome = "Minimum engagement on Saturday, Sunday or public holiday is 4 hours.",
            ValueExpression = "4"
        },

        // ----------------------------------------------------------------
        // OVERTIME (Clauses 27–28)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "OVT_001",
            Description = "Overtime – Monday to Friday – first 2 hours (150%)",
            ClauseReference = "Clause 27.1",
            Condition = "DayType == \"Weekday\" && OvertimeHours > 0 && OvertimePeriod == \"First2Hours\"",
            Outcome = "Overtime Mon–Fri first 2 hours: 150% of base hourly rate.",
            ValueExpression = "BaseHourlyRate * 1.50m"
        },
        new AwardRule
        {
            RuleId = "OVT_002",
            Description = "Overtime – Monday to Friday – after 2 hours (200%)",
            ClauseReference = "Clause 27.1",
            Condition = "DayType == \"Weekday\" && OvertimeHours > 0 && OvertimePeriod == \"After2Hours\"",
            Outcome = "Overtime Mon–Fri after 2 hours: 200% of base hourly rate.",
            ValueExpression = "BaseHourlyRate * 2.00m"
        },
        new AwardRule
        {
            RuleId = "OVT_003",
            Description = "Overtime – Saturday non-shiftworker – first 2 hours (150%)",
            ClauseReference = "Clause 27.2",
            Condition = "DayType == \"Saturday\" && !IsShiftworker && OvertimeHours > 0 && OvertimePeriod == \"First2Hours\"",
            Outcome = "Overtime Saturday (non-shiftworker) first 2 hours: 150% of base hourly rate.",
            ValueExpression = "BaseHourlyRate * 1.50m"
        },
        new AwardRule
        {
            RuleId = "OVT_004",
            Description = "Overtime – Saturday non-shiftworker – after 2 hours (200%)",
            ClauseReference = "Clause 27.2",
            Condition = "DayType == \"Saturday\" && !IsShiftworker && OvertimeHours > 0 && OvertimePeriod == \"After2Hours\"",
            Outcome = "Overtime Saturday (non-shiftworker) after 2 hours: 200% of base hourly rate.",
            ValueExpression = "BaseHourlyRate * 2.00m"
        },
        new AwardRule
        {
            RuleId = "OVT_005",
            Description = "Overtime – Saturday shiftworker – first 2 hours (150%)",
            ClauseReference = "Clause 27.3",
            Condition = "DayType == \"Saturday\" && IsShiftworker && OvertimeHours > 0 && OvertimePeriod == \"First2Hours\"",
            Outcome = "Overtime Saturday (shiftworker) first 2 hours: 150% of base hourly rate.",
            ValueExpression = "BaseHourlyRate * 1.50m"
        },
        new AwardRule
        {
            RuleId = "OVT_006",
            Description = "Overtime – Saturday shiftworker – after 2 hours (200%)",
            ClauseReference = "Clause 27.3",
            Condition = "DayType == \"Saturday\" && IsShiftworker && OvertimeHours > 0 && OvertimePeriod == \"After2Hours\"",
            Outcome = "Overtime Saturday (shiftworker) after 2 hours: 200% of base hourly rate.",
            ValueExpression = "BaseHourlyRate * 2.00m"
        },
        new AwardRule
        {
            RuleId = "OVT_007",
            Description = "Overtime meal break – employee must be allowed 30-min unpaid break after 2 hrs",
            ClauseReference = "Clause 28.1",
            Condition = "OvertimeHours >= 2",
            Outcome = "Employee must be allowed a 30-minute unpaid meal break after 2 hours of overtime."
        },
        new AwardRule
        {
            RuleId = "OVT_008",
            Description = "Overtime meal allowance – employee works overtime without adequate notice",
            ClauseReference = "Clause 28.2",
            Condition = "OvertimeHours >= 2 && NoOvertimeNotice",
            Outcome = "Overtime meal allowance of $15.48 applies when no adequate notice of overtime was given.",
            ValueExpression = "15.48m"
        },

        // ----------------------------------------------------------------
        // REST BREAKS / RECALL (Clause 29)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "BRK_001",
            Description = "Rest period between shifts – minimum 10 hours",
            ClauseReference = "Clause 29.1",
            Condition = "HoursBetweenShifts < 10 && HoursBetweenShifts > 0",
            Outcome = "Minimum 10-hour rest period between shifts required. Overtime rates apply if employee works without a full break."
        },
        new AwardRule
        {
            RuleId = "BRK_002",
            Description = "Tea break – 10-minute paid rest break during each ordinary shift",
            ClauseReference = "Clause 14.3",
            Condition = "OrdinaryHours >= 4",
            Outcome = "Employee is entitled to a 10-minute paid rest break during a shift of 4 hours or more."
        },
        new AwardRule
        {
            RuleId = "BRK_003",
            Description = "Meal break – unpaid break of 30–60 minutes",
            ClauseReference = "Clause 14.4",
            Condition = "OrdinaryHours > 5",
            Outcome = "Employee must be given an unpaid meal break of between 30 and 60 minutes when working more than 5 hours."
        },

        // ----------------------------------------------------------------
        // ALLOWANCES (Clause 18)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "ALL_001",
            Description = "Broken shift allowance",
            ClauseReference = "Clause 18.2",
            Condition = "IsBrokenShift",
            Outcome = "Broken shift allowance of $20.42 applies for each day a broken shift is worked.",
            ValueExpression = "20.42m"
        },
        new AwardRule
        {
            RuleId = "ALL_002",
            Description = "Excess fares – working away from usual workplace",
            ClauseReference = "Clause 18.3",
            Condition = "IsWorkingAwayFromUsualPlace",
            Outcome = "Excess fares allowance of $16.86 per day applies when working away from usual workplace.",
            ValueExpression = "16.86m"
        },
        new AwardRule
        {
            RuleId = "ALL_003",
            Description = "First aid allowance – Level 1/2 not in OSHC",
            ClauseReference = "Clause 18.4",
            Condition = "HasFirstAidCert && (Level == 1 || Level == 2) && !IsOSHC",
            Outcome = "First aid allowance of $12.12 per day applies.",
            ValueExpression = "12.12m"
        },
        new AwardRule
        {
            RuleId = "ALL_004",
            Description = "First aid allowance – Level 1/2 in OSHC",
            ClauseReference = "Clause 18.4",
            Condition = "HasFirstAidCert && (Level == 1 || Level == 2) && IsOSHC",
            Outcome = "First aid allowance of $1.57 per hour applies (OSHC context).",
            ValueExpression = "1.57m"
        },
        new AwardRule
        {
            RuleId = "ALL_005",
            Description = "Laundry and ironing – full-time employee",
            ClauseReference = "Clause 18.5",
            Condition = "RequiresUniform && EmploymentType == \"FullTime\"",
            Outcome = "Laundry and ironing allowance of $9.49 per week (full-time) applies.",
            ValueExpression = "9.49m"
        },
        new AwardRule
        {
            RuleId = "ALL_006",
            Description = "Laundry and ironing – part-time or casual employee",
            ClauseReference = "Clause 18.5",
            Condition = "RequiresUniform && (EmploymentType == \"PartTime\" || EmploymentType == \"Casual\")",
            Outcome = "Laundry and ironing allowance of $1.90 per day (max $9.49/week) applies.",
            ValueExpression = "1.90m"
        },
        new AwardRule
        {
            RuleId = "ALL_007",
            Description = "Laundry only (no ironing) – full-time employee",
            ClauseReference = "Clause 18.5",
            Condition = "RequiresUniformNoIroning && EmploymentType == \"FullTime\"",
            Outcome = "Laundry allowance (no ironing) of $5.98 per week applies.",
            ValueExpression = "5.98m"
        },
        new AwardRule
        {
            RuleId = "ALL_008",
            Description = "Laundry only (no ironing) – part-time or casual employee",
            ClauseReference = "Clause 18.5",
            Condition = "RequiresUniformNoIroning && (EmploymentType == \"PartTime\" || EmploymentType == \"Casual\")",
            Outcome = "Laundry allowance (no ironing) of $1.20 per day (max $5.98/week) applies.",
            ValueExpression = "1.20m"
        },
        new AwardRule
        {
            RuleId = "ALL_009",
            Description = "Educational leader allowance – 5+ days per week",
            ClauseReference = "Clause 18.6",
            Condition = "IsEducationalLeader && EducationalLeaderDaysPerWeek >= 5",
            Outcome = "Educational leader allowance of $4,567.31 per year applies.",
            ValueExpression = "4567.31m"
        },
        new AwardRule
        {
            RuleId = "ALL_010",
            Description = "Educational leader allowance – 4 days per week",
            ClauseReference = "Clause 18.6",
            Condition = "IsEducationalLeader && EducationalLeaderDaysPerWeek == 4",
            Outcome = "Educational leader allowance of $3,653.84 per year applies.",
            ValueExpression = "3653.84m"
        },
        new AwardRule
        {
            RuleId = "ALL_011",
            Description = "Educational leader allowance – 3 days per week",
            ClauseReference = "Clause 18.6",
            Condition = "IsEducationalLeader && EducationalLeaderDaysPerWeek == 3",
            Outcome = "Educational leader allowance of $2,740.38 per year applies.",
            ValueExpression = "2740.38m"
        },
        new AwardRule
        {
            RuleId = "ALL_012",
            Description = "Educational leader allowance – 2 days per week",
            ClauseReference = "Clause 18.6",
            Condition = "IsEducationalLeader && EducationalLeaderDaysPerWeek == 2",
            Outcome = "Educational leader allowance of $1,826.92 per year applies.",
            ValueExpression = "1826.92m"
        },
        new AwardRule
        {
            RuleId = "ALL_013",
            Description = "Educational leader allowance – 1 day per week",
            ClauseReference = "Clause 18.6",
            Condition = "IsEducationalLeader && EducationalLeaderDaysPerWeek == 1",
            Outcome = "Educational leader allowance of $913.46 per year applies.",
            ValueExpression = "913.46m"
        },
        new AwardRule
        {
            RuleId = "ALL_014",
            Description = "Vehicle allowance – motor car",
            ClauseReference = "Clause 18.8",
            Condition = "UsesOwnCar && KilometresTravelled > 0",
            Outcome = "Vehicle allowance of $0.99 per km applies.",
            ValueExpression = "KilometresTravelled * 0.99m"
        },
        new AwardRule
        {
            RuleId = "ALL_015",
            Description = "Vehicle allowance – motorcycle",
            ClauseReference = "Clause 18.8",
            Condition = "UsesOwnMotorcycle && KilometresTravelled > 0",
            Outcome = "Vehicle allowance of $0.33 per km applies.",
            ValueExpression = "KilometresTravelled * 0.33m"
        },

        // ----------------------------------------------------------------
        // APPRENTICE RULES (Clause 17)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "APP_001",
            Description = "Apprentice – training fees and textbook reimbursement",
            ClauseReference = "Clause 17.1",
            Condition = "Stream == \"Apprentice\"",
            Outcome = "Apprentice is entitled to reimbursement of training fees for prescribed courses and cost of prescribed textbooks."
        },
        new AwardRule
        {
            RuleId = "APP_002",
            Description = "Apprentice – excess travel to block release training reimbursement",
            ClauseReference = "Clause 17.2",
            Condition = "Stream == \"Apprentice\" && HasBlockReleaseTravel",
            Outcome = "Apprentice is entitled to reimbursement of excess reasonable travel costs to and from block release training."
        },
        new AwardRule
        {
            RuleId = "APP_003",
            Description = "Apprentice – 2nd and subsequent years same rate for junior 17 yrs",
            ClauseReference = "Schedule D",
            Condition = "Stream == \"Apprentice\" && ApprenticeYear >= 2 && JuniorAge == 17",
            Outcome = "Junior 17-year-old apprentice in 2nd+ year receives the same hourly rate as in the 1st year ($21.60)."
        },

        // ----------------------------------------------------------------
        // LEAVE RULES (NES + Clause 30+)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "LVE_001",
            Description = "Annual leave – 4 weeks per year (full-time equivalent)",
            ClauseReference = "NES / Clause 30",
            Condition = "EmploymentType == \"FullTime\" || EmploymentType == \"PartTime\"",
            Outcome = "Employee accrues 4 weeks annual leave per year (pro rata for part-time)."
        },
        new AwardRule
        {
            RuleId = "LVE_002",
            Description = "Shiftworker annual leave – 5 weeks",
            ClauseReference = "NES / Clause 30.2",
            Condition = "(EmploymentType == \"FullTime\" || EmploymentType == \"PartTime\") && IsShiftworker",
            Outcome = "Shiftworker accrues 5 weeks annual leave per year."
        },
        new AwardRule
        {
            RuleId = "LVE_003",
            Description = "Annual leave loading – 17.5% on base rate during leave",
            ClauseReference = "Clause 30.4",
            Condition = "IsOnAnnualLeave && (EmploymentType == \"FullTime\" || EmploymentType == \"PartTime\")",
            Outcome = "Annual leave loading of 17.5% on the base rate applies during annual leave.",
            ValueExpression = "BaseHourlyRate * 0.175m"
        },
        new AwardRule
        {
            RuleId = "LVE_004",
            Description = "Personal/carer's leave – 10 days per year",
            ClauseReference = "NES",
            Condition = "EmploymentType == \"FullTime\" || EmploymentType == \"PartTime\"",
            Outcome = "Employee accrues 10 days personal/carer's leave per year (pro rata for part-time)."
        },
        new AwardRule
        {
            RuleId = "LVE_005",
            Description = "Compassionate leave – 2 days per occasion",
            ClauseReference = "NES",
            Condition = "EmploymentType == \"FullTime\" || EmploymentType == \"PartTime\"",
            Outcome = "Employee is entitled to 2 days compassionate leave per occasion."
        },
        new AwardRule
        {
            RuleId = "LVE_006",
            Description = "Casual employee has no annual leave entitlement",
            ClauseReference = "Clause 10.3 / NES",
            Condition = "EmploymentType == \"Casual\"",
            Outcome = "Casual employees are not entitled to paid annual leave. The 25% casual loading compensates in lieu."
        },

        // ----------------------------------------------------------------
        // COOKS WHO HOLD / ARE WORKING TOWARDS ECEC QUALIFICATION (Pay guide)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "COOK_001",
            Description = "Cook with ECEC qualification must be paid CSE rate",
            ClauseReference = "Pay Guide – Special Notes",
            Condition = "IsCook && HasECECQualification",
            Outcome = "Cook who holds (or is working toward) an ECEC qualification and may work with children for ratio purposes must be paid the minimum CSE rate applicable to that qualification."
        },

        // ----------------------------------------------------------------
        // SUPERANNUATION (Clause 19)
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "SUP_001",
            Description = "Superannuation – employer must contribute at statutory rate",
            ClauseReference = "Clause 19",
            Condition = "EmploymentType == \"FullTime\" || EmploymentType == \"PartTime\" || EmploymentType == \"Casual\"",
            Outcome = "Employer must make superannuation contributions at the legislated rate on ordinary time earnings."
        },

        // ----------------------------------------------------------------
        // NOTICE & REDUNDANCY
        // ----------------------------------------------------------------
        new AwardRule
        {
            RuleId = "NOT_001",
            Description = "Notice of termination – less than 1 year of service",
            ClauseReference = "NES / Clause 32",
            Condition = "YearsOfService < 1",
            Outcome = "Minimum notice of 1 week required (less than 1 year of service)."
        },
        new AwardRule
        {
            RuleId = "NOT_002",
            Description = "Notice of termination – 1 to 3 years of service",
            ClauseReference = "NES / Clause 32",
            Condition = "YearsOfService >= 1 && YearsOfService < 3",
            Outcome = "Minimum notice of 2 weeks required (1–3 years of service)."
        },
        new AwardRule
        {
            RuleId = "NOT_003",
            Description = "Notice of termination – 3 to 5 years of service",
            ClauseReference = "NES / Clause 32",
            Condition = "YearsOfService >= 3 && YearsOfService < 5",
            Outcome = "Minimum notice of 3 weeks required (3–5 years of service)."
        },
        new AwardRule
        {
            RuleId = "NOT_004",
            Description = "Notice of termination – more than 5 years of service",
            ClauseReference = "NES / Clause 32",
            Condition = "YearsOfService >= 5",
            Outcome = "Minimum notice of 4 weeks required (5+ years of service)."
        },
        new AwardRule
        {
            RuleId = "NOT_005",
            Description = "Additional 1 week notice – employee over 45 with 2+ years of service",
            ClauseReference = "NES / Clause 32",
            Condition = "Age >= 45 && YearsOfService >= 2",
            Outcome = "Additional 1 week notice required for employees over 45 with 2+ years of service."
        },
    }.AsReadOnly();
}
