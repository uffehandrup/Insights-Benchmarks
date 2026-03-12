using XFlow.Insights.API.Domains.Workflows.Queries;
using XFlow.Insights.API.Domains.Workflows.Repositories;

namespace XFlow.Insights.API.Domains.Workflows.Handlers;

/// <summary>
/// Query handlers fetch data from the read model (projections).
/// They never modify state and always read from the optimized read model.
/// </summary>
public class WorkflowQueryHandler
{
    private readonly IWorkflowQueryRepository _queryRepository;

    public WorkflowQueryHandler(IWorkflowQueryRepository queryRepository)
    {
        _queryRepository = queryRepository;
    }

    public async Task<WorkflowDetails?> HandleGetWorkflowDetailsAsync(
        GetWorkflowDetailsQuery query,
        CancellationToken ct = default)
    {
        return await _queryRepository.GetWorkflowDetailsAsync(query.StreamId, ct);
    }

    public async Task<List<WorkflowEventLog>> HandleGetWorkflowEventHistoryAsync(
        GetWorkflowEventHistoryQuery query,
        CancellationToken ct = default)
    {
        return await _queryRepository.GetWorkflowEventHistoryAsync(query.StreamId, ct);
    }
}

/// <summary>
/// Event log DTO for read-side queries.
/// </summary>
public class WorkflowEventLog
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string EventData { get; set; } = string.Empty;
    public long Version { get; set; }
}
