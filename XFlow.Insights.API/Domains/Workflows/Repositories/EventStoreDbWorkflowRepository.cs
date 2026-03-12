using System.Text.Json;
using EventStore.Client;
using Microsoft.Extensions.Logging;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

/// <summary>
/// EventStoreDB-specific implementation of the IWorkflowRepository using the gRPC client.
/// Handles appending events to EventStoreDB via the EventStoreClient.
/// </summary>
public class EventStoreDbWorkflowRepository : IWorkflowRepository
{
    private readonly EventStoreClient _client;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<EventStoreDbWorkflowRepository> _logger;

    public EventStoreDbWorkflowRepository(EventStoreClient client, ILogger<EventStoreDbWorkflowRepository> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
        
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

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var eventData = SerializeEvent(@event);
        var streamName = $"workflow-{streamId}";
        
        // Use StreamState.Any for both new and existing streams
        // EventStoreDB will create the stream if it doesn't exist
        await _client.AppendToStreamAsync(
            streamName,
            StreamState.Any,
            new[] { eventData },
            cancellationToken: ct);

        sw.Stop();
        _logger.LogDebug("[EventStoreDB] AppendEvent took {ElapsedMs}ms for stream {StreamId}", sw.ElapsedMilliseconds, streamId);
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
