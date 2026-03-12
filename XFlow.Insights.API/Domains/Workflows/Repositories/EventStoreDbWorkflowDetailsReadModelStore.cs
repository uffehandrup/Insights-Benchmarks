using System.Text.Json;
using EventStore.Client;
using Microsoft.Extensions.Logging;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public class EventStoreDbWorkflowDetailsReadModelStore : IWorkflowDetailsReadModelStore
{
    private const string SnapshotEventType = "WorkflowDetailsProjectionSnapshot";
    private readonly EventStoreClient _client;
    private readonly ILogger<EventStoreDbWorkflowDetailsReadModelStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStoreDbWorkflowDetailsReadModelStore(
        EventStoreClient client,
        ILogger<EventStoreDbWorkflowDetailsReadModelStore> logger)
    {
        _client = client;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<WorkflowDetails?> GetAsync(Guid streamId, CancellationToken ct = default)
    {
        var streamName = BuildReadModelStreamName(streamId);

        try
        {
            var stream = _client.ReadStreamAsync(
                Direction.Backwards,
                streamName,
                StreamPosition.End,
                maxCount: 1,
                cancellationToken: ct);

            await foreach (var resolved in stream)
            {
                var snapshot = JsonSerializer.Deserialize<WorkflowDetailsProjectionSnapshot>(
                    resolved.Event.Data.Span,
                    _jsonOptions);

                return snapshot?.Details;
            }

            return null;
        }
        catch (StreamNotFoundException)
        {
            return null;
        }
    }

    public async Task UpsertAsync(WorkflowDetails details, CancellationToken ct = default)
    {
        var streamName = BuildReadModelStreamName(details.Id);
        var snapshot = new WorkflowDetailsProjectionSnapshot(details);
        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, _jsonOptions);

        var eventData = new EventData(
            Uuid.NewUuid(),
            SnapshotEventType,
            payload);

        await _client.AppendToStreamAsync(
            streamName,
            StreamState.Any,
            new[] { eventData },
            cancellationToken: ct);

        _logger.LogDebug("[EventStoreDB] Upserted workflow read model snapshot for stream {StreamId}", details.Id);
    }

    private static string BuildReadModelStreamName(Guid streamId) => $"workflow-details-{streamId}";

    private sealed record WorkflowDetailsProjectionSnapshot(WorkflowDetails Details);
}
