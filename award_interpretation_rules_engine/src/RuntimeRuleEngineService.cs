using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AwardInterpretationRulesEngine;

public sealed class RuleCalculationRequest
{
    public string TenantId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string AwardCode { get; set; } = "";
    public string RuleSetVersionId { get; set; } = "";
    public bool IsHistoricalRecalculation { get; set; }
    public string RecalculationReason { get; set; } = "";
    public PayRunInput PayRun { get; set; } = new();
}

public sealed class RuleCalculationResult
{
    public string TenantId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string RuleSetVersionId { get; set; } = "";
    public string AwardCode { get; set; } = "";
    public bool IsHistoricalRecalculation { get; set; }
    public AggregatePayRunResult Calculation { get; set; } = new();
    public List<ComplianceException> ComplianceExceptions { get; set; } = [];
    public List<StackingDecision> StackingDecisions { get; set; } = [];
    public List<PayLineCalculatedEvent> PayLineCalculatedEvents { get; set; } = [];
}

public sealed class ComplianceException
{
    public string ExceptionId { get; set; } = "";
    public string SegmentId { get; set; } = "";
    public string RuleId { get; set; } = "";
    public string ClauseReference { get; set; } = "";
    public string Severity { get; set; } = "warning";
    public string Message { get; set; } = "";
    public bool BlocksPayrollExport { get; set; }
    public List<string> EvidenceRequirements { get; set; } = [];
}

public sealed class StackingDecision
{
    public string SegmentId { get; set; } = "";
    public string StackingGroup { get; set; } = "";
    public string Policy { get; set; } = "";
    public string SelectedRuleId { get; set; } = "";
    public List<string> SuppressedRuleIds { get; set; } = [];
    public string Reason { get; set; } = "";
}

public sealed class PayLineCalculatedEvent
{
    public string TenantId { get; set; } = "";
    public string CorrelationId { get; set; } = "";
    public string RuleSetVersionId { get; set; } = "";
    public string EmployeeReference { get; set; } = "";
    public string PayPeriodReference { get; set; } = "";
    public PayLine PayLine { get; set; } = new();
    public List<RuleTrace> RuleTrace { get; set; } = [];
}

public sealed class RuleEngineRuntimeOptions
{
    public EngineOptions Engine { get; set; } = new();
    // Retained for configuration compatibility; runtime execution always requires an explicit rule_set_version_id.
    public bool RequireExplicitRuleSetVersion { get; set; } = true;
}

public interface IRuleSnapshotStore
{
    Task<RuleSetVersion> GetApprovedSnapshotAsync(string tenantId, string awardCode, string ruleSetVersionId, CancellationToken cancellationToken);
    Task<RuleSetVersion?> FindApprovedSnapshotAsync(string tenantId, string awardCode, DateOnly workDate, CancellationToken cancellationToken);
}

public interface IRuntimeRuleEngineService
{
    Task<RuleCalculationResult> CalculateAsync(RuleCalculationRequest request, CancellationToken cancellationToken = default);
    Task<RuleCalculationResult> RecalculateAsync(RuleCalculationRequest request, CancellationToken cancellationToken = default);
}

public interface ITimesheetSegmentNormaliser
{
    IReadOnlyList<PayRunRequest> BuildSegmentRequests(PayRunInput input, GovernedExpressionLibrary library);
}

public sealed class TimesheetSegmentNormaliser : ITimesheetSegmentNormaliser
{
    public IReadOnlyList<PayRunRequest> BuildSegmentRequests(PayRunInput input, GovernedExpressionLibrary library)
        => new TimesheetNormaliser(library).BuildSegmentRequests(input);
}

public interface IGovernedRuleCalculator
{
    PayRunResult Calculate(GovernedExpressionLibrary library, EngineOptions options, string ruleSetVersionId, PayRunRequest request);
}

public sealed class DynamicExpressoRuleCalculator : IGovernedRuleCalculator
{
    private readonly IExpressionParameterBinder _parameterBinder;
    private readonly ILogger<DynamicExpressoRuleCalculator> _logger;

    public DynamicExpressoRuleCalculator(
        IExpressionParameterBinder? parameterBinder = null,
        ILogger<DynamicExpressoRuleCalculator>? logger = null)
    {
        _parameterBinder = parameterBinder ?? new GovernedExpressionParameterBinder();
        _logger = logger ?? NullLogger<DynamicExpressoRuleCalculator>.Instance;
    }

    public PayRunResult Calculate(GovernedExpressionLibrary library, EngineOptions options, string ruleSetVersionId, PayRunRequest request)
    {
        _logger.LogDebug("Evaluating segment {SegmentId} with rule version {RuleSetVersionId}.", request.Parameters.GetValueOrDefault("SegmentId"), ruleSetVersionId);
        return new GovernedAwardRuleEngine(library, options, ruleSetVersionId, _parameterBinder).Calculate(request);
    }
}

public sealed class InMemoryRuleSnapshotStore : IRuleSnapshotStore
{
    private readonly List<RuleSetVersion> _snapshots;

    public InMemoryRuleSnapshotStore(IEnumerable<RuleSetVersion> snapshots)
    {
        _snapshots = snapshots.ToList();
    }

    public Task<RuleSetVersion> GetApprovedSnapshotAsync(string tenantId, string awardCode, string ruleSetVersionId, CancellationToken cancellationToken)
    {
        var snapshot = _snapshots.FirstOrDefault(s =>
            TenantMatches(s, tenantId) &&
            s.AwardCode.Equals(awardCode, StringComparison.OrdinalIgnoreCase) &&
            s.RuleSetVersionId.Equals(ruleSetVersionId, StringComparison.OrdinalIgnoreCase) &&
            IsExecutableStatus(s.Status));

        if (snapshot is null)
            throw new RuleSnapshotValidationException(ruleSetVersionId, $"Approved rule snapshot '{ruleSetVersionId}' was not found for award '{awardCode}'.");

        return Task.FromResult(snapshot);
    }

    public Task<RuleSetVersion?> FindApprovedSnapshotAsync(string tenantId, string awardCode, DateOnly workDate, CancellationToken cancellationToken)
    {
        var snapshot = _snapshots
            .Where(s =>
                TenantMatches(s, tenantId) &&
                s.AwardCode.Equals(awardCode, StringComparison.OrdinalIgnoreCase) &&
                IsExecutableStatus(s.Status) &&
                s.EffectiveFrom <= workDate &&
                (s.EffectiveTo is null || s.EffectiveTo > workDate))
            .OrderByDescending(s => s.EffectiveFrom)
            .ThenByDescending(s => s.PublishedAt)
            .FirstOrDefault();

        return Task.FromResult(snapshot);
    }

    private static bool TenantMatches(RuleSetVersion snapshot, string tenantId)
        => string.IsNullOrWhiteSpace(snapshot.TenantId) || snapshot.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutableStatus(string status)
        => status.Equals("approved", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("published", StringComparison.OrdinalIgnoreCase);
}

public sealed class RuntimeRuleEngineService : IRuntimeRuleEngineService
{
    private readonly IRuleSnapshotStore _snapshotStore;
    private readonly RuleEngineRuntimeOptions _options;
    private readonly ITimesheetSegmentNormaliser _segmentNormaliser;
    private readonly IGovernedRuleCalculator _ruleCalculator;
    private readonly ILogger<RuntimeRuleEngineService> _logger;

    public RuntimeRuleEngineService(
        IRuleSnapshotStore snapshotStore,
        RuleEngineRuntimeOptions options,
        ITimesheetSegmentNormaliser? segmentNormaliser = null,
        IGovernedRuleCalculator? ruleCalculator = null,
        ILogger<RuntimeRuleEngineService>? logger = null)
    {
        _snapshotStore = snapshotStore;
        _options = options;
        _segmentNormaliser = segmentNormaliser ?? new TimesheetSegmentNormaliser();
        _ruleCalculator = ruleCalculator ?? new DynamicExpressoRuleCalculator();
        _logger = logger ?? NullLogger<RuntimeRuleEngineService>.Instance;
    }

    public Task<RuleCalculationResult> RecalculateAsync(RuleCalculationRequest request, CancellationToken cancellationToken = default)
    {
        request.IsHistoricalRecalculation = true;
        return CalculateAsync(request, cancellationToken);
    }

    public async Task<RuleCalculationResult> CalculateAsync(RuleCalculationRequest request, CancellationToken cancellationToken = default)
    {
        var result = NewResult(request);
        var workDate = ResolveWorkDate(request.PayRun);

        if (string.IsNullOrWhiteSpace(request.RuleSetVersionId))
        {
            result.ComplianceExceptions.Add(SystemException("RULE_VERSION_REQUIRED", "A rule_set_version_id is required for runtime calculation."));
            return result;
        }

        RuleSetVersion snapshot;
        try
        {
            snapshot = await _snapshotStore.GetApprovedSnapshotAsync(request.TenantId, request.AwardCode, request.RuleSetVersionId, cancellationToken);
            ValidateSnapshot(snapshot, request, workDate);
        }
        catch (RuleSnapshotValidationException ex)
        {
            _logger.LogWarning(ex, "Rule snapshot rejected for tenant {TenantId}, award {AwardCode}, correlation {CorrelationId}.", request.TenantId, request.AwardCode, request.CorrelationId);
            result.ComplianceExceptions.Add(SystemException("RULE_SNAPSHOT_REJECTED", ex.Message));
            return result;
        }

        var library = snapshot.RulesJson;
        var segmentRequests = _segmentNormaliser.BuildSegmentRequests(request.PayRun, library);
        _logger.LogDebug(
            "Normalised {SegmentCount} pay segments for tenant {TenantId}, award {AwardCode}, rule version {RuleSetVersionId}, correlation {CorrelationId}.",
            segmentRequests.Count,
            request.TenantId,
            request.AwardCode,
            snapshot.RuleSetVersionId,
            request.CorrelationId);

        foreach (var segmentRequest in segmentRequests)
        {
            var segmentResult = _ruleCalculator.Calculate(library, _options.Engine, snapshot.RuleSetVersionId, segmentRequest);
            AppendSegment(result.Calculation, segmentResult);
        }

        result.RuleSetVersionId = snapshot.RuleSetVersionId;
        result.AwardCode = snapshot.AwardCode;
        result.Calculation.RuleSetVersionId = snapshot.RuleSetVersionId;
        result.Calculation.AwardCode = snapshot.AwardCode;
        result.Calculation.EmployeeReference = request.PayRun.EmployeeReference;
        result.Calculation.PayPeriodReference = request.PayRun.PayPeriodReference;

        ApplyStacking(result.Calculation, result.StackingDecisions);
        FinaliseAggregate(result.Calculation, request.PayRun.Employee.OpeningToilBalanceHours);
        ProjectComplianceExceptions(result.Calculation, result.ComplianceExceptions);
        ProjectPayLineEvents(request, result);

        _logger.LogInformation(
            "Calculated {PayLineCount} payroll lines for tenant {TenantId}, employee {EmployeeReference}, rule version {RuleSetVersionId}, correlation {CorrelationId}.",
            result.Calculation.PayrollLines.Count,
            request.TenantId,
            request.PayRun.EmployeeReference,
            snapshot.RuleSetVersionId,
            request.CorrelationId);

        return result;
    }

    private static void ValidateSnapshot(RuleSetVersion snapshot, RuleCalculationRequest request, DateOnly workDate)
    {
        if (!IsExecutableStatus(snapshot.Status))
            throw new RuleSnapshotValidationException(snapshot.RuleSetVersionId, "Only approved or published rule snapshots can be evaluated.");

        if (!snapshot.RuleSetVersionId.Equals(request.RuleSetVersionId, StringComparison.OrdinalIgnoreCase))
            throw new RuleSnapshotValidationException(snapshot.RuleSetVersionId, "Snapshot rule_set_version_id does not match the calculation request.");

        if (!snapshot.AwardCode.Equals(request.AwardCode, StringComparison.OrdinalIgnoreCase))
            throw new RuleSnapshotValidationException(snapshot.RuleSetVersionId, "Snapshot award_code does not match the calculation request.");

        if (snapshot.EffectiveFrom > workDate || (snapshot.EffectiveTo is not null && snapshot.EffectiveTo <= workDate))
            throw new RuleSnapshotValidationException(snapshot.RuleSetVersionId, $"Snapshot is not effective for work date {workDate:yyyy-MM-dd}.");

        if (string.IsNullOrWhiteSpace(snapshot.RuleSetVersionId) ||
            string.IsNullOrWhiteSpace(snapshot.SourceSnapshotHash) ||
            string.IsNullOrWhiteSpace(snapshot.ParserVersion) ||
            string.IsNullOrWhiteSpace(snapshot.CompilerVersion) ||
            snapshot.PublishedAt == default)
            throw new RuleSnapshotValidationException(snapshot.RuleSetVersionId, "Snapshot metadata is incomplete.");

        var library = snapshot.RulesJson;
        if (string.IsNullOrWhiteSpace(library.LibraryId) ||
            !library.AwardCode.Equals(snapshot.AwardCode, StringComparison.OrdinalIgnoreCase) ||
            library.Rules.Count == 0 ||
            library.Orchestration.EvaluationOrder.Count == 0)
            throw new RuleSnapshotValidationException(snapshot.RuleSetVersionId, "Snapshot rules_json is incomplete.");
    }

    private static bool IsExecutableStatus(string status)
        => status.Equals("approved", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("published", StringComparison.OrdinalIgnoreCase);

    private static RuleCalculationResult NewResult(RuleCalculationRequest request) => new()
    {
        TenantId = request.TenantId,
        CorrelationId = request.CorrelationId,
        RuleSetVersionId = request.RuleSetVersionId,
        AwardCode = request.AwardCode,
        IsHistoricalRecalculation = request.IsHistoricalRecalculation,
        Calculation = new AggregatePayRunResult
        {
            EmployeeReference = request.PayRun.EmployeeReference,
            PayPeriodReference = request.PayRun.PayPeriodReference
        }
    };

    private static void AppendSegment(AggregatePayRunResult aggregate, PayRunResult segment)
    {
        aggregate.PayrollLines.AddRange(segment.PayrollLines);
        aggregate.AwardReferenceLines.AddRange(segment.AwardReferenceLines);
        aggregate.BlockedPayrollLines.AddRange(segment.BlockedPayrollLines);
        aggregate.ToilMovements.AddRange(segment.ToilMovements);
        aggregate.Warnings.AddRange(segment.Warnings);
        aggregate.RuleTrace.AddRange(segment.RuleTrace);
        aggregate.SegmentContexts.Add(segment.FinalContext);
    }

    private static void ApplyStacking(AggregatePayRunResult aggregate, List<StackingDecision> decisions)
    {
        foreach (var group in aggregate.PayrollLines
                     .Where(l => !string.IsNullOrWhiteSpace(l.StackingGroup) && IsResolvingPolicy(l.StackingPolicy))
                     .GroupBy(l => new { l.SegmentId, l.StackingGroup }))
        {
            var selected = group
                .OrderByDescending(l => l.Amount)
                .ThenBy(l => l.RuleId, StringComparer.OrdinalIgnoreCase)
                .First();

            var suppressed = group
                .Where(l => l.RuleId != selected.RuleId || l.OutputKey != selected.OutputKey)
                .ToList();

            if (suppressed.Count == 0) continue;

            aggregate.PayrollLines.RemoveAll(l => suppressed.Contains(l));
            aggregate.AwardReferenceLines.RemoveAll(l => suppressed.Any(s => SameLine(s, l)));
            decisions.Add(new StackingDecision
            {
                SegmentId = selected.SegmentId,
                StackingGroup = selected.StackingGroup,
                Policy = selected.StackingPolicy,
                SelectedRuleId = selected.RuleId,
                SuppressedRuleIds = suppressed.Select(s => s.RuleId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Reason = $"{selected.StackingPolicy} selected the highest payable line for {selected.StackingGroup}."
            });
        }
    }

    private static bool IsResolvingPolicy(string policy)
        => policy.Equals("highest_of", StringComparison.OrdinalIgnoreCase) ||
           policy.Equals("exclusive", StringComparison.OrdinalIgnoreCase) ||
           policy.Equals("replacement", StringComparison.OrdinalIgnoreCase);

    private static bool SameLine(PayLine left, PayLine right)
        => left.SegmentId == right.SegmentId &&
           left.RuleId == right.RuleId &&
           left.OutputKey == right.OutputKey &&
           left.Amount == right.Amount;

    private static void FinaliseAggregate(AggregatePayRunResult aggregate, decimal openingToilBalanceHours)
    {
        aggregate.PayrollGross = RoundMoney(aggregate.PayrollLines.Sum(l => l.Amount));
        aggregate.AwardReferenceGross = RoundMoney(aggregate.AwardReferenceLines.Sum(l => l.Amount));
        aggregate.BlockedPayrollGross = RoundMoney(aggregate.BlockedPayrollLines.Sum(l => l.Amount));
        aggregate.ToilAccruedHours = aggregate.ToilMovements.Where(m => m.Type == "accrual").Sum(m => m.Hours);
        aggregate.ClosingToilBalanceHours = openingToilBalanceHours + aggregate.ToilAccruedHours;
    }

    private static void ProjectComplianceExceptions(AggregatePayRunResult aggregate, List<ComplianceException> exceptions)
    {
        exceptions.AddRange(aggregate.Warnings.Select(w => new ComplianceException
        {
            ExceptionId = $"{w.SegmentId}:{w.RuleId}:{w.Severity}",
            SegmentId = w.SegmentId,
            RuleId = w.RuleId,
            ClauseReference = w.ClauseReference,
            Severity = w.Severity,
            Message = w.Message,
            BlocksPayrollExport = w.BlocksPayrollExport,
            EvidenceRequirements = w.EvidenceRequirements
        }));
    }

    private static void ProjectPayLineEvents(RuleCalculationRequest request, RuleCalculationResult result)
    {
        var emittedLines = result.Calculation.PayrollLines.Concat(result.Calculation.BlockedPayrollLines);
        foreach (var line in emittedLines)
        {
            result.PayLineCalculatedEvents.Add(new PayLineCalculatedEvent
            {
                TenantId = request.TenantId,
                CorrelationId = request.CorrelationId,
                RuleSetVersionId = result.RuleSetVersionId,
                EmployeeReference = request.PayRun.EmployeeReference,
                PayPeriodReference = request.PayRun.PayPeriodReference,
                PayLine = line,
                RuleTrace = result.Calculation.RuleTrace
                    .Where(t => t.SegmentId == line.SegmentId && t.RuleId == line.RuleId)
                    .ToList()
            });
        }
    }

    private static DateOnly ResolveWorkDate(PayRunInput payRun)
        => payRun.Days.Count == 0 ? DateOnly.FromDateTime(DateTime.UtcNow) : payRun.Days.Min(d => d.Date);

    private static ComplianceException SystemException(string ruleId, string message) => new()
    {
        ExceptionId = ruleId,
        RuleId = ruleId,
        Severity = "error",
        Message = message,
        BlocksPayrollExport = true
    };

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed class RuleSnapshotValidationException : Exception
{
    public string RuleSetVersionId { get; }

    public RuleSnapshotValidationException(string ruleSetVersionId, string message)
        : base(string.IsNullOrWhiteSpace(ruleSetVersionId) ? message : $"{ruleSetVersionId}: {message}")
    {
        RuleSetVersionId = ruleSetVersionId;
    }
}

public static class RuleEngineServiceCollectionExtensions
{
    public static IServiceCollection AddRuleEngineRuntime<TSnapshotStore>(
        this IServiceCollection services,
        Action<RuleEngineRuntimeOptions>? configure = null)
        where TSnapshotStore : class, IRuleSnapshotStore
    {
        var options = new RuleEngineRuntimeOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton<IExpressionParameterBinder, GovernedExpressionParameterBinder>();
        services.TryAddSingleton<ITimesheetSegmentNormaliser, TimesheetSegmentNormaliser>();
        services.TryAddSingleton<IGovernedRuleCalculator, DynamicExpressoRuleCalculator>();
        services.AddSingleton<IRuleSnapshotStore, TSnapshotStore>();
        services.AddScoped<IRuntimeRuleEngineService, RuntimeRuleEngineService>();
        return services;
    }

    public static IServiceCollection AddRuleEngineRuntime(
        this IServiceCollection services,
        IEnumerable<RuleSetVersion> snapshots,
        Action<RuleEngineRuntimeOptions>? configure = null)
    {
        var options = new RuleEngineRuntimeOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton<IExpressionParameterBinder, GovernedExpressionParameterBinder>();
        services.TryAddSingleton<ITimesheetSegmentNormaliser, TimesheetSegmentNormaliser>();
        services.TryAddSingleton<IGovernedRuleCalculator, DynamicExpressoRuleCalculator>();
        services.AddSingleton<IRuleSnapshotStore>(new InMemoryRuleSnapshotStore(snapshots));
        services.AddScoped<IRuntimeRuleEngineService, RuntimeRuleEngineService>();
        return services;
    }
}
