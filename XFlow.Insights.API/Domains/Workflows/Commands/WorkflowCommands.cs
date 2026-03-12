namespace XFlow.Insights.API.Domains.Workflows.Commands;

/// <summary>
/// Commands represent intentions to perform an action.
/// Commands are immutable and drive the business logic.
/// </summary>

public abstract record Command
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public DateTime IssuedAt { get; init; } = DateTime.UtcNow;
}

public record StartWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string WorkflowName,
    DateTime IngestedAt) : Command;

public record CompleteWorkflowStepCommand(
    Guid StreamId,
    int WorkflowId,
    int StepNumber,
    DateTime IngestedAt
) : Command;

public record CompleteWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string FinalStatus = "Completed",
    DateTime IngestedAt = default
) : Command;

public record FailWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string FailureReason,
    DateTime IngestedAt
) : Command;

public record PauseWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string? Reason = null,
    DateTime IngestedAt = default
) : Command;

public record ResumeWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    DateTime IngestedAt
) : Command;

public record CancelWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string? Reason = null,
    DateTime IngestedAt = default
) : Command;
