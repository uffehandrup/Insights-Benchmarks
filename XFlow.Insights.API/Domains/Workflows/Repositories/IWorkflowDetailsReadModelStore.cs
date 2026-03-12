namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public interface IWorkflowDetailsReadModelStore
{
    Task<WorkflowDetails?> GetAsync(Guid streamId, CancellationToken ct = default);
    Task UpsertAsync(WorkflowDetails details, CancellationToken ct = default);
}
