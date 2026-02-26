using XFlow.Insights.API.Domains.Workflows.DomainEvents;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

/// <summary>
/// Repository for appending events directly to the event store.
/// Simple event stream management without aggregate abstraction.
/// </summary>
public interface IWorkflowRepository
{
    /// <summary>
    /// Append one or more events to a workflow's event stream.
    /// If the stream doesn't exist, it will be created.
    /// </summary>
    Task AppendEventAsync(Guid streamId, DomainEvent @event, CancellationToken ct = default);

}
