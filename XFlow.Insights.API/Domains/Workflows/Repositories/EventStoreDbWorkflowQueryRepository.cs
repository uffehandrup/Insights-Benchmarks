using System.Text;
using System.Text.Json;
using EventStore.Client;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Handlers;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public class EventStoreDbWorkflowQueryRepository : IWorkflowQueryRepository
{
    private readonly EventStoreClient _client;
    private readonly IWorkflowDetailsReadModelReader _readModelReader;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStoreDbWorkflowQueryRepository(
        EventStoreClient client,
        IWorkflowDetailsReadModelReader readModelReader)
    {
        _client = client;
        _readModelReader = readModelReader;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<WorkflowDetails?> GetWorkflowDetailsAsync(Guid streamId, CancellationToken ct = default)
    {
        return await _readModelReader.GetAsync(streamId, ct);
    }

    public async Task<List<WorkflowEventLog>> GetWorkflowEventHistoryAsync(Guid streamId, CancellationToken ct = default)
    {
        var streamName = BuildStreamName(streamId);
        var result = _client.ReadStreamAsync(Direction.Forwards, streamName, StreamPosition.Start, cancellationToken: ct);

        var eventLogs = new List<WorkflowEventLog>();

        await foreach (var resolved in result)
        {
            var domainEvent = DeserializeDomainEvent(resolved.Event.EventType, resolved.Event.Data.Span);
            if (domainEvent is null)
            {
                continue;
            }

            eventLogs.Add(new WorkflowEventLog
            {
                EventId = resolved.Event.EventId.ToGuid(),
                EventType = resolved.Event.EventType,
                OccurredAt = domainEvent.OccurredAt,
                EventData = JsonSerializer.Serialize(domainEvent),
                Version = unchecked((long)resolved.Event.EventNumber.ToUInt64())
            });
        }

        return eventLogs;
    }

    private async Task<List<DomainEvent>> ReadStreamEventsAsync(Guid streamId, CancellationToken ct)
    {
        var streamName = BuildStreamName(streamId);
        var result = _client.ReadStreamAsync(Direction.Forwards, streamName, StreamPosition.Start, cancellationToken: ct);

        var events = new List<DomainEvent>();
        await foreach (var resolved in result)
        {
            var domainEvent = DeserializeDomainEvent(resolved.Event.EventType, resolved.Event.Data.Span);
            if (domainEvent is not null)
            {
                events.Add(domainEvent);
            }
        }

        return events;
    }

    private DomainEvent? DeserializeDomainEvent(string eventType, ReadOnlySpan<byte> data)
    {
        var json = Encoding.UTF8.GetString(data);

        return eventType switch
        {
            nameof(WorkflowStartedDomainEvent) => JsonSerializer.Deserialize<WorkflowStartedDomainEvent>(json, _jsonOptions),
            nameof(WorkflowStepCompletedDomainEvent) => JsonSerializer.Deserialize<WorkflowStepCompletedDomainEvent>(json, _jsonOptions),
            nameof(WorkflowCompletedDomainEvent) => JsonSerializer.Deserialize<WorkflowCompletedDomainEvent>(json, _jsonOptions),
            nameof(WorkflowFailedDomainEvent) => JsonSerializer.Deserialize<WorkflowFailedDomainEvent>(json, _jsonOptions),
            nameof(WorkflowPausedDomainEvent) => JsonSerializer.Deserialize<WorkflowPausedDomainEvent>(json, _jsonOptions),
            nameof(WorkflowResumedDomainEvent) => JsonSerializer.Deserialize<WorkflowResumedDomainEvent>(json, _jsonOptions),
            nameof(WorkflowCancelledDomainEvent) => JsonSerializer.Deserialize<WorkflowCancelledDomainEvent>(json, _jsonOptions),
            _ => null
        };
    }

    private static string BuildStreamName(Guid streamId) => $"workflow-{streamId}";
}