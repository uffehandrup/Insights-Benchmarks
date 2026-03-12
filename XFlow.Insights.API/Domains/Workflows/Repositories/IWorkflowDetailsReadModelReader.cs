namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public interface IWorkflowDetailsReadModelReader
{
    Task<WorkflowDetails?> GetAsync(Guid streamId, CancellationToken ct = default);
}