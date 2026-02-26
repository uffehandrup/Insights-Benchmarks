using XFlow.Insights.API.Domains.Workflows.Commands;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;
using XFlow.Insights.API.Domains.Workflows.Repositories;

namespace XFlow.Insights.API.Domains.Workflows.Handlers;

/// <summary>
/// Command handlers create events directly and append them to the event store.
/// No validation - all events are accepted to maintain a complete audit trail.
/// </summary>
public class WorkflowCommandHandler
{
    private readonly IWorkflowRepository _repository;

    public WorkflowCommandHandler(IWorkflowRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> HandleStartWorkflowAsync(StartWorkflowCommand cmd, CancellationToken ct = default)
    {
        // Create event directly from command
        var @event = new WorkflowStartedDomainEvent(
            cmd.StreamId,
            cmd.WorkflowId, 
            cmd.WorkflowName, 
            DateTime.UtcNow);

        // Append to event store (creates stream if doesn't exist)
        await _repository.AppendEventAsync(cmd.StreamId, @event, ct);

        return cmd.StreamId;
    }

    public async Task HandleCompleteStepAsync(CompleteWorkflowStepCommand cmd, CancellationToken ct = default)
    {
        // Create event directly from command
        var @event = new WorkflowStepCompletedDomainEvent(
            cmd.StreamId,
            cmd.WorkflowId, 
            cmd.StepNumber, 
            DateTime.UtcNow);

        // Append to event store
        await _repository.AppendEventAsync(cmd.StreamId, @event, ct);
    }

    public async Task HandleCompleteWorkflowAsync(CompleteWorkflowCommand cmd, CancellationToken ct = default)
    {
        // Create event directly from command
        var @event = new WorkflowCompletedDomainEvent(
            cmd.StreamId,
            cmd.WorkflowId, 
            cmd.FinalStatus, 
            DateTime.UtcNow);

        // Append to event store
        await _repository.AppendEventAsync(cmd.StreamId, @event, ct);
    }

    public async Task HandleFailWorkflowAsync(FailWorkflowCommand cmd, CancellationToken ct = default)
    {
        // Create event directly from command
        var @event = new WorkflowFailedDomainEvent(
            cmd.StreamId,
            cmd.WorkflowId, 
            cmd.FailureReason, 
            DateTime.UtcNow);

        // Append to event store
        await _repository.AppendEventAsync(cmd.StreamId, @event, ct);
    }

    public async Task HandlePauseWorkflowAsync(PauseWorkflowCommand cmd, CancellationToken ct = default)
    {
        // Create event directly from command
        var @event = new WorkflowPausedDomainEvent(
            cmd.StreamId,
            cmd.WorkflowId, 
            cmd.Reason, 
            DateTime.UtcNow);

        // Append to event store
        await _repository.AppendEventAsync(cmd.StreamId, @event, ct);
    }

    public async Task HandleResumeWorkflowAsync(ResumeWorkflowCommand cmd, CancellationToken ct = default)
    {
        // Create event directly from command
        var @event = new WorkflowResumedDomainEvent(
            cmd.StreamId,
            cmd.WorkflowId, 
            DateTime.UtcNow);

        // Append to event store
        await _repository.AppendEventAsync(cmd.StreamId, @event, ct);
    }

    public async Task HandleCancelWorkflowAsync(CancelWorkflowCommand cmd, CancellationToken ct = default)
    {
        // Create event directly from command
        var @event = new WorkflowCancelledDomainEvent(
            cmd.StreamId,
            cmd.WorkflowId, 
            cmd.Reason, 
            DateTime.UtcNow);

        // Append to event store
        await _repository.AppendEventAsync(cmd.StreamId, @event, ct);
    }
}
