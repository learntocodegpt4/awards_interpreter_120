namespace AwardInterpretationRulesEngine;

public sealed class AwardPipeline
{
    private readonly AppSettings _settings;

    public AwardPipeline(AppSettings settings)
    {
        _settings = settings;
    }

    public async Task<PipelineResult> RunAsync(string awardCode, PayRunInput payRun, CancellationToken cancellationToken)
    {
        var sourceClient = new AwardSourceClient(_settings);
        var snapshot = await sourceClient.FetchAsync(awardCode, cancellationToken);

        var parser = new OnlineAwardHtmlParser();
        var document = await parser.ParseAsync(snapshot, cancellationToken);

        var interpretation = awardCode.Equals("MA000120", StringComparison.OrdinalIgnoreCase)
            ? Ma000120InterpretationBuilder.BuildInterpretation(snapshot, document)
            : throw new NotSupportedException($"No deterministic interpretation template has been implemented for {awardCode}.");

        var library = Ma000120InterpretationBuilder.BuildLibrary(interpretation);
        var normaliser = new TimesheetNormaliser(library);
        var segmentRequests = normaliser.BuildSegmentRequests(payRun);

        var engine = new GovernedAwardRuleEngine(library, _settings.Engine);
        var aggregate = new AggregatePayRunResult
        {
            EmployeeReference = payRun.EmployeeReference,
            PayPeriodReference = payRun.PayPeriodReference
        };

        foreach (var request in segmentRequests)
        {
            var segment = engine.Calculate(request);
            aggregate.PayrollLines.AddRange(segment.PayrollLines);
            aggregate.AwardReferenceLines.AddRange(segment.AwardReferenceLines);
            aggregate.BlockedPayrollLines.AddRange(segment.BlockedPayrollLines);
            aggregate.ToilMovements.AddRange(segment.ToilMovements);
            aggregate.Warnings.AddRange(segment.Warnings);
            aggregate.RuleTrace.AddRange(segment.RuleTrace);
            aggregate.SegmentContexts.Add(segment.FinalContext);
        }

        aggregate.PayrollGross = Math.Round(aggregate.PayrollLines.Sum(l => l.Amount), 2, MidpointRounding.AwayFromZero);
        aggregate.AwardReferenceGross = Math.Round(aggregate.AwardReferenceLines.Sum(l => l.Amount), 2, MidpointRounding.AwayFromZero);
        aggregate.BlockedPayrollGross = Math.Round(aggregate.BlockedPayrollLines.Sum(l => l.Amount), 2, MidpointRounding.AwayFromZero);
        aggregate.ToilAccruedHours = aggregate.ToilMovements.Where(m => m.Type == "accrual").Sum(m => m.Hours);
        aggregate.ClosingToilBalanceHours = payRun.Employee.OpeningToilBalanceHours + aggregate.ToilAccruedHours;

        return new PipelineResult
        {
            Interpretation = interpretation,
            Library = library,
            Calculation = aggregate
        };
    }
}
