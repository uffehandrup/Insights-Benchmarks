using XFlow.Insights.API.Domains.Workflows.Handlers;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public interface IWorkflowQueryRepository
{
    Task<WorkflowDetails?> GetWorkflowDetailsAsync(Guid streamId, CancellationToken ct = default);
    Task<List<WorkflowEventLog>> GetWorkflowEventHistoryAsync(Guid streamId, CancellationToken ct = default);
}