using XFlow.Insights.API.Domains.Workflows.DomainEvents;

namespace XFlow.Insights.API.Domains.Workflows.Projections;

public sealed class WorkflowDetailsProjector
{
    public WorkflowDetails? Apply(WorkflowDetails? current, DomainEvent @event)
    {
        return @event switch
        {
            WorkflowStartedDomainEvent started => new WorkflowDetails
            {
                Id = started.StreamId,
                OriginalWorkflowId = started.WorkflowId,
                CurrentStatus = "Running",
                StartedAt = started.StartedAt,
                LastUpdatedAt = started.OccurredAt,
                TotalEventsProcessed = 1,
                StepNumber = 1
            },
            WorkflowStepCompletedDomainEvent stepCompleted when current is not null => ApplyStepCompleted(current, stepCompleted),
            WorkflowCompletedDomainEvent completed when current is not null => ApplyCompleted(current, completed),
            WorkflowFailedDomainEvent failed when current is not null => ApplyFailed(current, failed),
            WorkflowPausedDomainEvent paused when current is not null => ApplyPaused(current, paused),
            WorkflowResumedDomainEvent resumed when current is not null => ApplyResumed(current, resumed),
            WorkflowCancelledDomainEvent cancelled when current is not null => ApplyCancelled(current, cancelled),
            _ => current
        };
    }

    private static WorkflowDetails ApplyStepCompleted(WorkflowDetails current, WorkflowStepCompletedDomainEvent @event)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.StepNumber = @event.StepNumber;
        current.TotalEventsProcessed++;
        return current;
    }

    private static WorkflowDetails ApplyCompleted(WorkflowDetails current, WorkflowCompletedDomainEvent @event)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = @event.FinalStatus;
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
        return current;
    }

    private static WorkflowDetails ApplyFailed(WorkflowDetails current, WorkflowFailedDomainEvent @event)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Failed";
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
        return current;
    }

    private static WorkflowDetails ApplyPaused(WorkflowDetails current, WorkflowPausedDomainEvent @event)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Paused";
        current.TotalEventsProcessed++;
        return current;
    }

    private static WorkflowDetails ApplyResumed(WorkflowDetails current, WorkflowResumedDomainEvent @event)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Running";
        current.TotalEventsProcessed++;
        return current;
    }

    private static WorkflowDetails ApplyCancelled(WorkflowDetails current, WorkflowCancelledDomainEvent @event)
    {
        current.LastUpdatedAt = @event.OccurredAt;
        current.CurrentStatus = "Cancelled";
        current.CompletedAt = @event.OccurredAt;
        current.TotalEventsProcessed++;
        return current;
    }
}