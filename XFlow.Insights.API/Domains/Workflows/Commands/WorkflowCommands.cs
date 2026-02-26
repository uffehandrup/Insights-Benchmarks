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
    string WorkflowName) : Command;

public record CompleteWorkflowStepCommand(
    Guid StreamId,
    int WorkflowId,
    int StepNumber
) : Command;

public record CompleteWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string FinalStatus = "Completed"
) : Command;

public record FailWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string FailureReason
) : Command;

public record PauseWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string? Reason = null
) : Command;

public record ResumeWorkflowCommand(
    Guid StreamId,
    int WorkflowId
) : Command;

public record CancelWorkflowCommand(
    Guid StreamId,
    int WorkflowId,
    string? Reason = null
) : Command;
