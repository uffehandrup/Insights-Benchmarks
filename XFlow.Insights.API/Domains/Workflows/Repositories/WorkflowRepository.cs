using Marten;
using XFlow.Insights.API.Domains;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

/// <summary>
/// Marten-based implementation for appending events directly to the event store.
/// Simple, focused on event stream management.
/// </summary>
public class WorkflowRepository : IWorkflowRepository
{
    private readonly IDocumentSession _session;

    public WorkflowRepository(IDocumentSession session)
    {
        _session = session;
    }

    public async Task AppendEventAsync(Guid streamId, DomainEvent @event, CancellationToken ct = default)
    {
        // Check if this is a new stream or existing
        var existingStream = await _session.Events.FetchStreamAsync(streamId, token: ct);

        if (existingStream.Count == 0)
        {
            // Start new event stream
            _session.Events.StartStream(streamId, @event);
        }
        else
        {
            // Append to existing stream
            _session.Events.Append(streamId, @event);
        }

        await _session.SaveChangesAsync(token: ct);
    }
}
