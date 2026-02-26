using Marten;
using XFlow.Insights.API.Domains.Workflows.Queries;

namespace XFlow.Insights.API.Domains.Workflows.Handlers;

/// <summary>
/// Query handlers fetch data from the read model (projections).
/// They never modify state and always read from the optimized read model.
/// </summary>
public class WorkflowQueryHandler
{
    private readonly IDocumentSession _session;

    public WorkflowQueryHandler(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<WorkflowDetails?> HandleGetWorkflowDetailsAsync(
        GetWorkflowDetailsQuery query,
        CancellationToken ct = default)
    {
        // Read from the projection (pre-calculated read model)
        var workflow = await _session.LoadAsync<WorkflowDetails>(query.StreamId, token: ct);
        return workflow;
    }

    public async Task<List<WorkflowEventLog>> HandleGetWorkflowEventHistoryAsync(
        GetWorkflowEventHistoryQuery query,
        CancellationToken ct = default)
    {
        var events = await _session.Events.FetchStreamAsync(query.StreamId, token: ct);

        return events
            .Select(e => new WorkflowEventLog
            {
                EventId = e.Id,
                EventType = e.Data.GetType().Name,
                OccurredAt = e.Timestamp.DateTime,
                EventData = System.Text.Json.JsonSerializer.Serialize(e.Data),
                Version = e.Version
            })
            .ToList();
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
