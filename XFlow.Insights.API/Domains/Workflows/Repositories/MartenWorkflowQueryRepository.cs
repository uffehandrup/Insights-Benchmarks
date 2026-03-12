using Marten;
using XFlow.Insights.API.Domains.Workflows.Handlers;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public class MartenWorkflowQueryRepository : IWorkflowQueryRepository
{
    private readonly IDocumentSession _session;

    public MartenWorkflowQueryRepository(IDocumentSession session)
    {
        _session = session;
    }

    public async Task<WorkflowDetails?> GetWorkflowDetailsAsync(Guid streamId, CancellationToken ct = default)
    {
        return await _session.LoadAsync<WorkflowDetails>(streamId, token: ct);
    }

    public async Task<List<WorkflowEventLog>> GetWorkflowEventHistoryAsync(Guid streamId, CancellationToken ct = default)
    {
        var events = await _session.Events.FetchStreamAsync(streamId, token: ct);

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