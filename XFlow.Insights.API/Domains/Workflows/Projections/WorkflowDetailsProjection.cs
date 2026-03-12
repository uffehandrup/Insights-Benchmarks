using Marten.Events.Aggregation;
using Microsoft.Extensions.Logging;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Telemetry;

/// <summary>
/// Read model projection for Workflows.
/// Maintains a denormalized view optimized for queries.
/// Updated inline via event streaming to maintain consistency.
/// </summary>
public class WorkflowDetailsProjection : SingleStreamProjection<WorkflowDetails, Guid>
{
    private static readonly ILogger ProjectionLatencyLogger = LoggerFactory
        .Create(builder => builder.AddConsole())
        .CreateLogger<WorkflowDetailsProjection>();

    // Marten uses convention-based routing. 
    // A method named 'Create' or 'Apply' that takes your event type will automatically be called.

    public WorkflowDetails Create(WorkflowStartedDomainEvent @event)
    {
        var ingestedAt = @event.ResolveIngestedAt(@event.IngestedAt);
        var projectionLatency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(
            ProjectionLatencyLogger,
            backend: "Postgres",
            eventType: nameof(WorkflowStartedDomainEvent),
            streamId: @event.StreamId,
            workflowId: @event.WorkflowId,
            projectionLatency: projectionLatency);

        return new WorkflowDetails
        {
            // Marten uses the stream ID as the document ID
            Id = @event.StreamId,
            OriginalWorkflowId = @event.WorkflowId,
            CurrentStatus = "Running",
            StartedAt = @event.StartedAt,
            LastUpdatedAt = @event.OccurredAt,
            TotalEventsProcessed = 1,
            StepNumber = 1
        };
    }

    public void Apply(WorkflowStepCompletedDomainEvent @event, WorkflowDetails current)
    {
        var ingestedAt = @event.ResolveIngestedAt(@event.IngestedAt);
        var projectionLatency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(
            ProjectionLatencyLogger,
            backend: "Postgres",
            eventType: nameof(WorkflowStepCompletedDomainEvent),
            streamId: @event.StreamId,
            workflowId: @event.WorkflowId,
            projectionLatency: projectionLatency);

        current.LastUpdatedAt = @event.OccurredAt;
        current.StepNumber = @event.StepNumber;
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowCompletedDomainEvent @event, WorkflowDetails current)
    {
        var ingestedAt = @event.ResolveIngestedAt(@event.IngestedAt);
        var projectionLatency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(
            ProjectionLatencyLogger,
            backend: "Postgres",
            eventType: nameof(WorkflowCompletedDomainEvent),
            streamId: @event.StreamId,
            workflowId: @event.WorkflowId,
            projectionLatency: projectionLatency);

        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = @event.FinalStatus;
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowFailedDomainEvent @event, WorkflowDetails current)
    {
        var ingestedAt = @event.ResolveIngestedAt(@event.IngestedAt);
        var projectionLatency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(
            ProjectionLatencyLogger,
            backend: "Postgres",
            eventType: nameof(WorkflowFailedDomainEvent),
            streamId: @event.StreamId,
            workflowId: @event.WorkflowId,
            projectionLatency: projectionLatency);

        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Failed";
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowPausedDomainEvent @event, WorkflowDetails current)
    {
        var ingestedAt = @event.ResolveIngestedAt(@event.IngestedAt);
        var projectionLatency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(
            ProjectionLatencyLogger,
            backend: "Postgres",
            eventType: nameof(WorkflowPausedDomainEvent),
            streamId: @event.StreamId,
            workflowId: @event.WorkflowId,
            projectionLatency: projectionLatency);

        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Paused";
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowResumedDomainEvent @event, WorkflowDetails current)
    {
        var ingestedAt = @event.ResolveIngestedAt(@event.IngestedAt);
        var projectionLatency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(
            ProjectionLatencyLogger,
            backend: "Postgres",
            eventType: nameof(WorkflowResumedDomainEvent),
            streamId: @event.StreamId,
            workflowId: @event.WorkflowId,
            projectionLatency: projectionLatency);

        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Running";
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowCancelledDomainEvent @event, WorkflowDetails current)
    {
        var ingestedAt = @event.ResolveIngestedAt(@event.IngestedAt);
        var projectionLatency = DateTime.UtcNow - ingestedAt;
        ProjectionLatencyMetrics.Record(
            ProjectionLatencyLogger,
            backend: "Postgres",
            eventType: nameof(WorkflowCancelledDomainEvent),
            streamId: @event.StreamId,
            workflowId: @event.WorkflowId,
            projectionLatency: projectionLatency);

        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Cancelled";
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
    }
}