using System.Text;
using System.Text.Json;
using EventStore.Client;

namespace XFlow.Insights.Benchmarks.EventStoreDB.Repositories.EventStoreDB;

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
    public async Task AppendEventAsync(Guid streamId, object @event, CancellationToken ct = default)
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
    /// Appends a batch of events to a workflow stream in a single network roundtrip.
    /// This significantly reduces network latency and improves throughput.
    /// </summary>
    public async Task AppendEventsAsync(Guid streamId, IEnumerable<object> events, CancellationToken ct = default)
    {
        if (events == null || !events.Any())
            return;

        var eventDataList = events.Select(SerializeEvent).ToArray();
        var streamName = $"workflow-{streamId}";
        
        // Determine expected revision based on stream existence
        var expectedRevision = _existingStreams.Contains(streamId) 
            ? StreamState.Any 
            : StreamState.NoStream;

        // Append entire batch in ONE network roundtrip
        await _client.AppendToStreamAsync(
            streamName,
            expectedRevision,
            eventDataList,
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
    private EventData SerializeEvent(object @event)
    {
        var eventType = @event.GetType().Name;
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(@event, _jsonOptions);
        
        return new EventData(
            Uuid.NewUuid(),
            eventType,
            jsonBytes);
    }
}
