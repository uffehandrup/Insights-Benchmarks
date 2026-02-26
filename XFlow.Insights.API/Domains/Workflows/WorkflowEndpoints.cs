using Marten;
using XFlow.Insights.API.Domains.Workflows.Commands;
using XFlow.Insights.API.Domains.Workflows.Handlers;
using XFlow.Insights.API.Domains.Workflows.Queries;
using XFlow.Insights.API.Domains.Workflows.Repositories;

namespace XFlow.Insights.API.Domains.Workflows;

public static class WorkflowEndpoints
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflows")
            .WithTags("Workflows");

// QUERY ENDPOINTS ------------------------
        
        group.MapGet("/{streamId:guid}/{workflowId:int}", GetWorkflowDetailsAsync)
            .WithName("GetWorkflowDetails")
            .WithOpenApi()
            .Produces<WorkflowDetails>(200)
            .Produces(404);

        group.MapGet("/{streamId:guid}/{workflowId:int}/events", GetWorkflowEventHistoryAsync)
            .WithName("GetWorkflowEventHistory")
            .WithOpenApi()
            .Produces<List<WorkflowEventLog>>(200)
            .Produces(404);

// COMMAND ENDPOINTS ------------------------
        
        group.MapPost("/start/{streamId:guid}/{workflowId:int}", StartWorkflowAsync)
            .WithName("StartWorkflow")
            .WithOpenApi()
            .Produces(201)
            .Produces(400);

        group.MapPost("/{streamId:guid}/{workflowId:int}/step-completed", CompleteStepAsync)
            .WithName("CompleteWorkflowStep")
            .WithOpenApi()
            .Produces(200)
            .Produces(404)
            .Produces(400);

        group.MapPost("/{streamId:guid}/{workflowId:int}/complete", CompleteWorkflowAsync)
            .WithName("CompleteWorkflow")
            .WithOpenApi()
            .Produces(200)
            .Produces(404)
            .Produces(400);

        group.MapPost("/{streamId:guid}/{workflowId:int}/fail", FailWorkflowAsync)
            .WithName("FailWorkflow")
            .WithOpenApi()
            .Produces(200)
            .Produces(404)
            .Produces(400);

        group.MapPost("/{streamId:guid}/{workflowId:int}/pause", PauseWorkflowAsync)
            .WithName("PauseWorkflow")
            .WithOpenApi()
            .Produces(200)
            .Produces(404)
            .Produces(400);

        group.MapPost("/{streamId:guid}/{workflowId:int}/resume", ResumeWorkflowAsync)
            .WithName("ResumeWorkflow")
            .WithOpenApi()
            .Produces(200)
            .Produces(404)
            .Produces(400);

        group.MapPost("/{streamId:guid}/{workflowId:int}/cancel", CancelWorkflowAsync)
            .WithName("CancelWorkflow")
            .WithOpenApi()
            .Produces(200)
            .Produces(404)
            .Produces(400);

        return app;
    }

// QUERY HANDLERS ------------------------


    private static async Task<IResult> GetWorkflowDetailsAsync(
        Guid streamId,
        int workflowId,
        IDocumentSession session,
        CancellationToken ct)
    {
        var handler = new WorkflowQueryHandler(session);
        var query = new GetWorkflowDetailsQuery(streamId, workflowId);
        var result = await handler.HandleGetWorkflowDetailsAsync(query, ct);

        return result is not null
            ? Results.Ok(result)
            : Results.NotFound(new { message = $"Workflow {workflowId} not found" });
    }

    private static async Task<IResult> GetWorkflowEventHistoryAsync(
        Guid streamId,
        int workflowId,
        IDocumentSession session,
        CancellationToken ct)
    {
        var handler = new WorkflowQueryHandler(session);
        var query = new GetWorkflowEventHistoryQuery(streamId, workflowId);

        try
        {
            var result = await handler.HandleGetWorkflowEventHistoryAsync(query, ct);
            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }

// COMMAND HANDLERS ------------------------

    private static async Task<IResult> StartWorkflowAsync(
        Guid streamId,
        int workflowId,
        StartWorkflowRequest request,
        IDocumentSession session,
        CancellationToken ct)
    {
        try
        {
            var repository = new WorkflowRepository(session);
            var handler = new WorkflowCommandHandler(repository);
            var command = new StartWorkflowCommand(streamId, workflowId, request.WorkflowName);
            await handler.HandleStartWorkflowAsync(command, ct);

            return Results.Created($"/api/workflows/{streamId}/{workflowId}", new { streamId, workflowId });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CompleteStepAsync(
        Guid streamId,
        int workflowId,
        CompleteWorkflowStepRequest request,
        IDocumentSession session,
        CancellationToken ct)
    {
        try
        {
            var repository = new WorkflowRepository(session);
            var handler = new WorkflowCommandHandler(repository);
            var command = new CompleteWorkflowStepCommand(streamId, workflowId, request.StepNumber);
            await handler.HandleCompleteStepAsync(command, ct);

            return Results.Ok(new { message = "Step completed" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CompleteWorkflowAsync(
        Guid streamId,
        int workflowId,
        CompleteWorkflowRequest? request,
        IDocumentSession session,
        CancellationToken ct)
    {
        try
        {
            var repository = new WorkflowRepository(session);
            var handler = new WorkflowCommandHandler(repository);
            var finalStatus = request?.FinalStatus ?? "Completed";
            var command = new CompleteWorkflowCommand(streamId, workflowId, finalStatus);
            await handler.HandleCompleteWorkflowAsync(command, ct);

            return Results.Ok(new { message = "Workflow completed" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> FailWorkflowAsync(
        Guid streamId,
        int workflowId,
        FailWorkflowRequest request,
        IDocumentSession session,
        CancellationToken ct)
    {
        try
        {
            var repository = new WorkflowRepository(session);
            var handler = new WorkflowCommandHandler(repository);
            var command = new FailWorkflowCommand(streamId, workflowId, request.FailureReason);
            await handler.HandleFailWorkflowAsync(command, ct);

            return Results.Ok(new { message = "Workflow failed" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> PauseWorkflowAsync(
        Guid streamId,
        int workflowId,
        PauseWorkflowRequest? request,
        IDocumentSession session,
        CancellationToken ct)
    {
        try
        {
            var repository = new WorkflowRepository(session);
            var handler = new WorkflowCommandHandler(repository);
            var command = new PauseWorkflowCommand(streamId, workflowId, request?.Reason);
            await handler.HandlePauseWorkflowAsync(command, ct);

            return Results.Ok(new { message = "Workflow paused" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ResumeWorkflowAsync(
        Guid streamId,
        int workflowId,
        IDocumentSession session,
        CancellationToken ct)
    {
        try
        {
            var repository = new WorkflowRepository(session);
            var handler = new WorkflowCommandHandler(repository);
            var command = new ResumeWorkflowCommand(streamId, workflowId);
            await handler.HandleResumeWorkflowAsync(command, ct);

            return Results.Ok(new { message = "Workflow resumed" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CancelWorkflowAsync(
        Guid streamId,
        int workflowId,
        CancelWorkflowRequest? request,
        IDocumentSession session,
        CancellationToken ct)
    {
        try
        {
            var repository = new WorkflowRepository(session);
            var handler = new WorkflowCommandHandler(repository);
            var command = new CancelWorkflowCommand(streamId, workflowId, request?.Reason);
            await handler.HandleCancelWorkflowAsync(command, ct);

            return Results.Ok(new { message = "Workflow cancelled" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private record StartWorkflowRequest(string WorkflowName);
    private record CompleteWorkflowStepRequest(int StepNumber);
    private record CompleteWorkflowRequest(string? FinalStatus);
    private record FailWorkflowRequest(string FailureReason);
    private record PauseWorkflowRequest(string? Reason);
    private record CancelWorkflowRequest(string? Reason);
}

