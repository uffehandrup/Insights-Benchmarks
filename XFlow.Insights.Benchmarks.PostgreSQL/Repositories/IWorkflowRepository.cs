namespace XFlow.Insights.Benchmarks.PostgreSQL.Repositories;

/// <summary>
/// Abstraction for workflow repository operations.
/// Implemented by specific event store implementations (PostgreSQL, EventStoreDB, MongoDB, etc.).
/// </summary>
public interface IWorkflowRepository
{
    /// <summary>
    /// Appends an event to a workflow stream.
    /// </summary>
    /// <param name="streamId">The unique identifier of the workflow stream.</param>
    /// <param name="event">The domain event to append.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendEventAsync(Guid streamId, object @event, CancellationToken ct = default);
    Task AppendEventsAsync(Guid streamId, IEnumerable<object> events, CancellationToken ct = default);
}
