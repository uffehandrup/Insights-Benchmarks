using System.Text;
using System.Text.Json;
using EventStore.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Repositories;
using XFlow.Insights.API.Domains.Workflows.Telemetry;

namespace XFlow.Insights.API.Domains.Workflows.Projections;

public class EventStoreWorkflowProjectionWorker : BackgroundService
{
    private const string ProjectionName = "workflow-details-v1";
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private const int MaxEventsPerIteration = 500;

    private readonly EventStoreClient _client;
    private readonly IWorkflowDetailsReadModelReader _readModelReader;
    private readonly IWorkflowDetailsProjectionWriter _projectionWriter;
    private readonly IProjectionCheckpointStore _checkpointStore;
    private readonly WorkflowDetailsProjector _projector;
    private readonly ILogger<EventStoreWorkflowProjectionWorker> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public EventStoreWorkflowProjectionWorker(
        EventStoreClient client,
        IWorkflowDetailsReadModelReader readModelReader,
        IWorkflowDetailsProjectionWriter projectionWriter,
        IProjectionCheckpointStore checkpointStore,
        WorkflowDetailsProjector projector,
        ILogger<EventStoreWorkflowProjectionWorker> logger)
    {
        _client = client;
        _readModelReader = readModelReader;
        _projectionWriter = projectionWriter;
        _checkpointStore = checkpointStore;
        _projector = projector;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[EventStoreProjection] Background projection worker started");

        var lastPosition = await LoadLastPositionAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            lastPosition = await ProjectPendingEventsAsync(lastPosition, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task<ProjectionPosition?> LoadLastPositionAsync(CancellationToken ct)
    {
        var checkpoint = await _checkpointStore.GetAsync(ProjectionName, ct);
        return checkpoint?.Position;
    }

    private async Task<ProjectionPosition?> ProjectPendingEventsAsync(ProjectionPosition? lastPosition, CancellationToken ct)
    {
        // maxCount lets the server-side stream end naturally — avoids faulting the gRPC HTTP/2 connection
        // that occurs when breaking out of an open streaming call mid-flight.
        var stream = _client.ReadAllAsync(
            Direction.Forwards,
            lastPosition?.ToEventStorePosition() ?? Position.Start,
            maxCount: MaxEventsPerIteration,
            cancellationToken: ct);

        var latestProcessedPosition = lastPosition;
        var stateCache = new Dictionary<Guid, WorkflowDetails?>();

        // Accumulate per-workflow final state so we only write to PostgreSQL once per batch
        // (keyed by streamId so each workflow keeps only its latest projected state).
        var dirtyEntries = new Dictionary<Guid, (WorkflowDetails Details, ProjectionPosition Position)>();

        await foreach (var resolved in stream.WithCancellation(ct))
        {
            if (!TryGetPosition(resolved, out var eventPosition))
            {
                continue;
            }

            if (latestProcessedPosition.HasValue && eventPosition.CompareTo(latestProcessedPosition.Value) <= 0)
            {
                continue;
            }

            // Always advance the checkpoint, even for system / non-workflow events.
            latestProcessedPosition = eventPosition;

            if (!resolved.Event.EventStreamId.StartsWith("workflow-", StringComparison.Ordinal))
            {
                continue;
            }

            var domainEvent = DeserializeDomainEvent(resolved.Event.EventType, resolved.Event.Data.Span);
            if (domainEvent is null)
            {
                continue;
            }

            var streamId = GetStreamId(domainEvent);
            var current = await LoadCurrentStateAsync(streamId, stateCache, ct);
            var projected = _projector.Apply(current, domainEvent);

            stateCache[streamId] = projected;

            if (projected is not null)
            {
                // Overwrite so the dictionary always holds the latest state + position for this workflow.
                dirtyEntries[streamId] = (projected, eventPosition);
                RecordLatency(domainEvent, streamId, GetWorkflowId(domainEvent), resolved.Event.EventType);
            }
        }

        if (latestProcessedPosition.HasValue && latestProcessedPosition != lastPosition)
        {
            // Single batch write: one connection + transaction for all dirty read models.
            if (dirtyEntries.Count > 0)
            {
                await _projectionWriter.BulkUpsertAsync([.. dirtyEntries.Values], ct);
            }

            await _checkpointStore.StoreAsync(ProjectionName, latestProcessedPosition.Value, ct);
        }

        return latestProcessedPosition;
    }

    private async Task<WorkflowDetails?> LoadCurrentStateAsync(
        Guid streamId,
        Dictionary<Guid, WorkflowDetails?> stateCache,
        CancellationToken ct)
    {
        if (stateCache.TryGetValue(streamId, out var current))
        {
            return current;
        }

        current = await _readModelReader.GetAsync(streamId, ct);
        stateCache[streamId] = current;
        return current;
    }

    private static bool TryGetPosition(ResolvedEvent resolved, out ProjectionPosition position)
    {
        if (!resolved.OriginalPosition.HasValue)
        {
            position = default;
            return false;
        }

        position = ProjectionPosition.From(resolved.OriginalPosition.Value);
        return true;
    }

    private static Guid GetStreamId(DomainEvent @event)
    {
        return @event switch
        {
            WorkflowStartedDomainEvent started => started.StreamId,
            WorkflowStepCompletedDomainEvent stepCompleted => stepCompleted.StreamId,
            WorkflowCompletedDomainEvent completed => completed.StreamId,
            WorkflowFailedDomainEvent failed => failed.StreamId,
            WorkflowPausedDomainEvent paused => paused.StreamId,
            WorkflowResumedDomainEvent resumed => resumed.StreamId,
            WorkflowCancelledDomainEvent cancelled => cancelled.StreamId,
            _ => Guid.Empty
        };
    }

    private static int GetWorkflowId(DomainEvent @event)
    {
        return @event switch
        {
            WorkflowStartedDomainEvent started => started.WorkflowId,
            WorkflowStepCompletedDomainEvent stepCompleted => stepCompleted.WorkflowId,
            WorkflowCompletedDomainEvent completed => completed.WorkflowId,
            WorkflowFailedDomainEvent failed => failed.WorkflowId,
            WorkflowPausedDomainEvent paused => paused.WorkflowId,
            WorkflowResumedDomainEvent resumed => resumed.WorkflowId,
            WorkflowCancelledDomainEvent cancelled => cancelled.WorkflowId,
            _ => 0
        };
    }

    private void RecordLatency(DomainEvent @event, Guid streamId, int workflowId, string eventType)
    {
        var ingestedAt = @event switch
        {
            WorkflowStartedDomainEvent started => started.ResolveIngestedAt(started.IngestedAt),
            WorkflowStepCompletedDomainEvent stepCompleted => stepCompleted.ResolveIngestedAt(stepCompleted.IngestedAt),
            WorkflowCompletedDomainEvent completed => completed.ResolveIngestedAt(completed.IngestedAt),
            WorkflowFailedDomainEvent failed => failed.ResolveIngestedAt(failed.IngestedAt),
            WorkflowPausedDomainEvent paused => paused.ResolveIngestedAt(paused.IngestedAt),
            WorkflowResumedDomainEvent resumed => resumed.ResolveIngestedAt(resumed.IngestedAt),
            WorkflowCancelledDomainEvent cancelled => cancelled.ResolveIngestedAt(cancelled.IngestedAt),
            _ => @event.OccurredAt
        };

        var latency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(_logger, "EventStoreDB", eventType, streamId, workflowId, latency);
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
}
