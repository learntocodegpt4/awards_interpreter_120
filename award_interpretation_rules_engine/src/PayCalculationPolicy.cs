namespace AwardInterpretationRulesEngine;

/// <summary>
/// Central deterministic precision policy for payroll calculation.
/// </summary>
public static class PayCalculationPolicy
{
    public const int CurrencyDecimalPlaces = 2;
    public const int QuantityDecimalPlaces = 4;
    public static readonly MidpointRounding RoundingMode = MidpointRounding.AwayFromZero;

    public static decimal RoundMoney(decimal value)
        => Math.Round(value, CurrencyDecimalPlaces, RoundingMode);

    public static decimal RoundQuantity(decimal value)
        => Math.Round(value, QuantityDecimalPlaces, RoundingMode);

    public static decimal RoundHours(decimal hours)
        => RoundQuantity(hours);

    public static decimal RoundMinutes(decimal minutes)
        => RoundQuantity(minutes);

    public static decimal DurationToHours(TimeSpan duration)
        => RoundHours((decimal)duration.Ticks / TimeSpan.TicksPerHour);

    public static decimal DurationToMinutes(TimeSpan duration)
        => RoundMinutes((decimal)duration.Ticks / TimeSpan.TicksPerMinute);

    public static decimal RoundUpToQuarterHour(decimal hours)
        => RoundHours(Math.Ceiling(hours * 4m) / 4m);
}
