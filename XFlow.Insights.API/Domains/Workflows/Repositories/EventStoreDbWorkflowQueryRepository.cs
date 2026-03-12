using System.Text;
using System.Text.Json;
using EventStore.Client;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Handlers;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public class EventStoreDbWorkflowQueryRepository : IWorkflowQueryRepository
{
    private readonly EventStoreClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStoreDbWorkflowQueryRepository(EventStoreClient client)
    {
        _client = client;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<WorkflowDetails?> GetWorkflowDetailsAsync(Guid streamId, CancellationToken ct = default)
    {
        var events = await ReadStreamEventsAsync(streamId, ct);
        if (events.Count == 0)
        {
            return null;
        }

        WorkflowDetails? details = null;

        foreach (var @event in events)
        {
            switch (@event)
            {
                case WorkflowStartedDomainEvent started:
                    details = new WorkflowDetails
                    {
                        Id = started.StreamId,
                        OriginalWorkflowId = started.WorkflowId,
                        CurrentStatus = "Running",
                        StartedAt = started.StartedAt,
                        LastUpdatedAt = started.OccurredAt,
                        TotalEventsProcessed = 1,
                        StepNumber = 1
                    };
                    break;

                case WorkflowStepCompletedDomainEvent stepCompleted when details is not null:
                    details.LastUpdatedAt = stepCompleted.OccurredAt;
                    details.StepNumber = stepCompleted.StepNumber;
                    details.TotalEventsProcessed++;
                    break;

                case WorkflowCompletedDomainEvent completed when details is not null:
                    details.LastUpdatedAt = completed.OccurredAt;
                    details.CurrentStatus = completed.FinalStatus;
                    details.CompletedAt = completed.OccurredAt;
                    details.TotalEventsProcessed++;
                    break;

                case WorkflowFailedDomainEvent failed when details is not null:
                    details.LastUpdatedAt = failed.OccurredAt;
                    details.CurrentStatus = "Failed";
                    details.CompletedAt = failed.OccurredAt;
                    details.TotalEventsProcessed++;
                    break;

                case WorkflowPausedDomainEvent paused when details is not null:
                    details.LastUpdatedAt = paused.OccurredAt;
                    details.CurrentStatus = "Paused";
                    details.TotalEventsProcessed++;
                    break;

                case WorkflowResumedDomainEvent resumed when details is not null:
                    details.LastUpdatedAt = resumed.OccurredAt;
                    details.CurrentStatus = "Running";
                    details.TotalEventsProcessed++;
                    break;

                case WorkflowCancelledDomainEvent cancelled when details is not null:
                    details.LastUpdatedAt = cancelled.OccurredAt;
                    details.CurrentStatus = "Cancelled";
                    details.CompletedAt = cancelled.OccurredAt;
                    details.TotalEventsProcessed++;
                    break;
            }
        }

        return details;
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