namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public interface IProjectionCheckpointStore
{
    Task<ProjectionCheckpoint?> GetAsync(string projectionName, CancellationToken ct = default);
    Task StoreAsync(string projectionName, ProjectionPosition position, CancellationToken ct = default);
}