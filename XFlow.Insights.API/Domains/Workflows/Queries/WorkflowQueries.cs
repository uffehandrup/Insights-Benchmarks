namespace XFlow.Insights.API.Domains.Workflows.Queries;

/// <summary>
/// Queries represent requests for data.
/// They are immutable and return read-model data.
/// </summary>
/// 
public abstract record Query
{
    public Guid QueryId { get; init; } = Guid.NewGuid();
}

public record GetWorkflowDetailsQuery(Guid StreamId, int WorkflowId) : Query;

public record GetWorkflowEventHistoryQuery(Guid StreamId, int WorkflowId) : Query;

public record GetAllWorkflowsQuery(int? PageNumber = null, int? PageSize = null) : Query;
