using Marten.Events.Aggregation;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;

/// <summary>
/// Read model projection for Workflows.
/// Maintains a denormalized view optimized for queries.
/// Updated inline via event streaming to maintain consistency.
/// </summary>
public class WorkflowDetailsProjection : SingleStreamProjection<WorkflowDetails, Guid>
{
    // Marten uses convention-based routing. 
    // A method named 'Create' or 'Apply' that takes your event type will automatically be called.

    public WorkflowDetails Create(WorkflowStartedDomainEvent @event)
    {
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
        current.LastUpdatedAt = @event.OccurredAt;
        current.StepNumber = @event.StepNumber;
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowCompletedDomainEvent @event, WorkflowDetails current)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = @event.FinalStatus;
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowFailedDomainEvent @event, WorkflowDetails current)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Failed";
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowPausedDomainEvent @event, WorkflowDetails current)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Paused";
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowResumedDomainEvent @event, WorkflowDetails current)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Running";
        current.TotalEventsProcessed++;
    }

    public void Apply(WorkflowCancelledDomainEvent @event, WorkflowDetails current)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Cancelled";
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
    }
}