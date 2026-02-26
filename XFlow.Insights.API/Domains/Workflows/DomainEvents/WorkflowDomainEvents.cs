namespace XFlow.Insights.API.Domains.Workflows.DomainEvents;

/// <summary>
/// Domain events represent things that have happened in the business domain.
/// These are the events that get appended to the event store.
/// </summary>

public abstract record DomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int Version { get; init; }
}

public record WorkflowStartedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string WorkflowName,
    DateTime StartedAt) : DomainEvent;

public record WorkflowStepCompletedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    int StepNumber,
    DateTime CompletedAt) : DomainEvent;

public record WorkflowCompletedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string FinalStatus,
    DateTime CompletedAt
) : DomainEvent;

public record WorkflowFailedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string FailureReason,
    DateTime FailedAt
) : DomainEvent;

public record WorkflowPausedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string? Reason,
    DateTime PausedAt
) : DomainEvent;

public record WorkflowResumedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    DateTime ResumedAt
) : DomainEvent;

public record WorkflowCancelledDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string? Reason,
    DateTime CancelledAt
) : DomainEvent;
