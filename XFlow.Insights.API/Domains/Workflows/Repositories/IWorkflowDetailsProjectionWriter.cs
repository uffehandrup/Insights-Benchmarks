namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public interface IWorkflowDetailsProjectionWriter
{
    Task UpsertAsync(WorkflowDetails details, ProjectionPosition position, CancellationToken ct = default);

    /// <summary>
    /// Upserts multiple workflow read models in a single database round-trip using one connection + transaction.
    /// </summary>
    Task BulkUpsertAsync(IReadOnlyList<(WorkflowDetails Details, ProjectionPosition Position)> entries, CancellationToken ct = default);
}