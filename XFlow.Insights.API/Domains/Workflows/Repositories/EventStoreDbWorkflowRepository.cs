using System.Text.Json;
using EventStore.Client;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

/// <summary>
/// EventStoreDB-specific implementation of the IWorkflowRepository using the gRPC client.
/// Handles appending events to EventStoreDB via the EventStoreClient.
/// </summary>
public class EventStoreDbWorkflowRepository : IWorkflowRepository
{
    private readonly EventStoreClient _client;
    private readonly HashSet<Guid> _existingStreams;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStoreDbWorkflowRepository(EventStoreClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _existingStreams = new HashSet<Guid>();
        
        // Use default System.Text.Json serialization (matches Marten's default)
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Appends a single event to a workflow stream in EventStoreDB.
    /// Creates a new stream on first append, then appends to existing stream.
    /// </summary>
    public async Task AppendEventAsync(Guid streamId, DomainEvent @event, CancellationToken ct = default)
    {
        if (@event == null)
            throw new ArgumentNullException(nameof(@event));

        var eventData = SerializeEvent(@event);
        var streamName = $"workflow-{streamId}";
        
        // Determine expected revision based on stream existence
        var expectedRevision = _existingStreams.Contains(streamId) 
            ? StreamState.Any 
            : StreamState.NoStream;

        // Append single event to stream
        await _client.AppendToStreamAsync(
            streamName,
            expectedRevision,
            new[] { eventData },
            cancellationToken: ct);

        // Track stream as existing after first append
        if (!_existingStreams.Contains(streamId))
        {
            _existingStreams.Add(streamId);
        }
    }

    /// <summary>
    /// Serializes a domain event to EventStoreDB's EventData format using JSON.
    /// </summary>
    private EventData SerializeEvent(DomainEvent @event)
    {
        var eventType = @event.GetType().Name;
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(@event, _jsonOptions);
        
        return new EventData(
            Uuid.NewUuid(),
            eventType,
            jsonBytes);
    }
}
