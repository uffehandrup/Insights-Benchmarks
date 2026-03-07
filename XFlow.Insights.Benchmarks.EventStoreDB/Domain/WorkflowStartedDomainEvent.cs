namespace XFlow.Insights.Benchmarks.EventStoreDB.Domain;

/// <summary>
/// Domain event representing the start of a workflow.
/// Used for benchmarking event append operations to EventStoreDB.
/// </summary>
public record WorkflowStartedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string WorkflowName,
    DateTime StartedAt);
