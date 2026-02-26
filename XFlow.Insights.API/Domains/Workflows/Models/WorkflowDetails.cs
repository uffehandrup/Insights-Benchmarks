public class WorkflowDetails
{
    // Marten uses this Id to link the read model to the specific event stream
    public Guid Id { get; set; }
    public int OriginalWorkflowId { get; set; }
    public string CurrentStatus { get; set; } = "Running";
    public DateTime StartedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // metadata!
    public int TotalEventsProcessed { get; set; }
    public int StepNumber { get; set; } = 1;
}