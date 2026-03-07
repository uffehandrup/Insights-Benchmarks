namespace XFlow.Insights.Benchmarks.PostgreSQL.Domain;

/// <summary>
/// Domain event representing the start of a workflow.
/// Used for benchmarking event append operations to PostgreSQL via Marten.
/// </summary>
public record WorkflowStartedDomainEvent(
    Guid StreamId,
    int WorkflowId,
    string WorkflowName,
    DateTime StartedAt);
