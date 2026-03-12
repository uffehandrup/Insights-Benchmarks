namespace XFlow.Insights.API.Domains.Workflows.DomainEvents;

/// <summary>
/// Domain events represent things that have happened in the business domain.
/// These are the events that get appended to the event store.
/// </summary>

public abstract record DomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public int Version { get; init; }

    // Falls back to OccurredAt for backward compatibility with older stored events.
    public DateTime ResolveIngestedAt(DateTime? ingestedAt)
    {
        if (ingestedAt is null || ingestedAt <= DateTime.UnixEpoch)
        {
            return OccurredAt;
        }

        return ingestedAt.Value;
    }
}

public record WorkflowStartedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string WorkflowName,
    DateTime StartedAt,
    DateTime? IngestedAt = null) : DomainEvent;

public record WorkflowStepCompletedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    int StepNumber,
    DateTime CompletedAt,
    DateTime? IngestedAt = null) : DomainEvent;

public record WorkflowCompletedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string FinalStatus,
    DateTime CompletedAt,
    DateTime? IngestedAt = null
) : DomainEvent;

public record WorkflowFailedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string FailureReason,
    DateTime FailedAt,
    DateTime? IngestedAt = null
) : DomainEvent;

public record WorkflowPausedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string? Reason,
    DateTime PausedAt,
    DateTime? IngestedAt = null
) : DomainEvent;

public record WorkflowResumedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    DateTime ResumedAt,
    DateTime? IngestedAt = null
) : DomainEvent;

public record WorkflowCancelledDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string? Reason,
    DateTime CancelledAt,
    DateTime? IngestedAt = null
) : DomainEvent;
