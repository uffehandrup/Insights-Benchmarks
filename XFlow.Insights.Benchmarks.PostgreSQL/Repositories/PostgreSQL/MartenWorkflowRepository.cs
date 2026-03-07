using Marten;

namespace XFlow.Insights.Benchmarks.PostgreSQL.Repositories.PostgreSQL;

/// <summary>
/// PostgreSQL-specific implementation of the IWorkflowRepository using Marten.
/// Handles appending events to the event store via Marten's IDocumentSession.
/// </summary>
public class MartenWorkflowRepository : IWorkflowRepository
{
    private readonly IDocumentSession _session;
    private readonly HashSet<Guid> _existingStreams;

    public MartenWorkflowRepository(IDocumentSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _existingStreams = new HashSet<Guid>();
    }

    /// <summary>
    /// Appends a single event to a workflow stream in PostgreSQL via Marten.
    /// Creates a new stream on first append, then appends to existing stream.
    /// </summary>
    public async Task AppendEventAsync(Guid streamId, object @event, CancellationToken ct = default)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        if (!_existingStreams.Contains(streamId))
        {
            // Start a new stream with the first event
            _session.Events.StartStream(streamId, @event);
            _existingStreams.Add(streamId);
        }
        else
        {
            // Append to existing stream
            _session.Events.Append(streamId, @event);
        }

        // Save changes immediately (inline approach)
        await _session.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Appends a batch of events to a workflow stream in a single transaction.
    /// This significantly reduces network roundtrips and database I/O latency.
    /// </summary>
    public async Task AppendEventsAsync(Guid streamId, IEnumerable<object> events, CancellationToken ct = default)
    {
        if (events == null || !events.Any())
            return;

        if (!_existingStreams.Contains(streamId))
        {
            // Start a new stream with the initial batch of events
            _session.Events.StartStream(streamId, events);
            _existingStreams.Add(streamId);
        }
        else
        {
            // Append the entire batch to the existing stream
            _session.Events.Append(streamId, events);
        }

        // Commit the entire batch in ONE database transaction / network roundtrip
        await _session.SaveChangesAsync(ct);
    }
}