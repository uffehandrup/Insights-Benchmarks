using Npgsql;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public sealed class PostgresWorkflowDetailsStore : IWorkflowDetailsReadModelReader, IWorkflowDetailsProjectionWriter
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresProjectionSchemaManager _schemaManager;

    public PostgresWorkflowDetailsStore(
        NpgsqlDataSource dataSource,
        PostgresProjectionSchemaManager schemaManager)
    {
        _dataSource = dataSource;
        _schemaManager = schemaManager;
    }

    public async Task<WorkflowDetails?> GetAsync(Guid streamId, CancellationToken ct = default)
    {
        await _schemaManager.EnsureCreatedAsync(ct);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select stream_id,
                   original_workflow_id,
                   current_status,
                   started_at,
                   last_updated_at,
                   completed_at,
                   total_events_processed,
                   step_number
            from read_models.workflow_details
            where stream_id = @stream_id
            limit 1;
            """;
        command.Parameters.AddWithValue("stream_id", streamId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new WorkflowDetails
        {
            Id = reader.GetGuid(0),
            OriginalWorkflowId = reader.GetInt32(1),
            CurrentStatus = reader.GetString(2),
            StartedAt = reader.GetFieldValue<DateTime>(3),
            LastUpdatedAt = reader.GetFieldValue<DateTime>(4),
            CompletedAt = await reader.IsDBNullAsync(5, ct) ? null : reader.GetFieldValue<DateTime>(5),
            TotalEventsProcessed = reader.GetInt32(6),
            StepNumber = reader.GetInt32(7)
        };
    }

    public async Task UpsertAsync(WorkflowDetails details, ProjectionPosition position, CancellationToken ct = default)
    {
        await _schemaManager.EnsureCreatedAsync(ct);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into read_models.workflow_details (
                stream_id,
                original_workflow_id,
                current_status,
                started_at,
                last_updated_at,
                completed_at,
                total_events_processed,
                step_number,
                last_event_commit_position,
                last_event_prepare_position,
                row_version,
                updated_at
            )
            values (
                @stream_id,
                @original_workflow_id,
                @current_status,
                @started_at,
                @last_updated_at,
                @completed_at,
                @total_events_processed,
                @step_number,
                @last_event_commit_position,
                @last_event_prepare_position,
                1,
                now()
            )
            on conflict (stream_id) do update
            set original_workflow_id = excluded.original_workflow_id,
                current_status = excluded.current_status,
                started_at = excluded.started_at,
                last_updated_at = excluded.last_updated_at,
                completed_at = excluded.completed_at,
                total_events_processed = excluded.total_events_processed,
                step_number = excluded.step_number,
                last_event_commit_position = excluded.last_event_commit_position,
                last_event_prepare_position = excluded.last_event_prepare_position,
                row_version = read_models.workflow_details.row_version + 1,
                updated_at = now()
            where (read_models.workflow_details.last_event_commit_position, read_models.workflow_details.last_event_prepare_position)
                < (excluded.last_event_commit_position, excluded.last_event_prepare_position);
            """;
        command.Parameters.AddWithValue("stream_id", details.Id);
        command.Parameters.AddWithValue("original_workflow_id", details.OriginalWorkflowId);
        command.Parameters.AddWithValue("current_status", details.CurrentStatus);
        command.Parameters.AddWithValue("started_at", details.StartedAt);
        command.Parameters.AddWithValue("last_updated_at", details.LastUpdatedAt);
        command.Parameters.AddWithValue("completed_at", (object?)details.CompletedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("total_events_processed", details.TotalEventsProcessed);
        command.Parameters.AddWithValue("step_number", details.StepNumber);
        command.Parameters.AddWithValue("last_event_commit_position", position.CommitPositionInt64);
        command.Parameters.AddWithValue("last_event_prepare_position", position.PreparePositionInt64);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task BulkUpsertAsync(IReadOnlyList<(WorkflowDetails Details, ProjectionPosition Position)> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;

        await _schemaManager.EnsureCreatedAsync(ct);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var batch = connection.CreateBatch();
        batch.Transaction = transaction;

        const string sql = """
            insert into read_models.workflow_details (
                stream_id,
                original_workflow_id,
                current_status,
                started_at,
                last_updated_at,
                completed_at,
                total_events_processed,
                step_number,
                last_event_commit_position,
                last_event_prepare_position,
                row_version,
                updated_at
            )
            values (
                @stream_id,
                @original_workflow_id,
                @current_status,
                @started_at,
                @last_updated_at,
                @completed_at,
                @total_events_processed,
                @step_number,
                @last_event_commit_position,
                @last_event_prepare_position,
                1,
                now()
            )
            on conflict (stream_id) do update
            set original_workflow_id = excluded.original_workflow_id,
                current_status = excluded.current_status,
                started_at = excluded.started_at,
                last_updated_at = excluded.last_updated_at,
                completed_at = excluded.completed_at,
                total_events_processed = excluded.total_events_processed,
                step_number = excluded.step_number,
                last_event_commit_position = excluded.last_event_commit_position,
                last_event_prepare_position = excluded.last_event_prepare_position,
                row_version = read_models.workflow_details.row_version + 1,
                updated_at = now()
            where (read_models.workflow_details.last_event_commit_position, read_models.workflow_details.last_event_prepare_position)
                < (excluded.last_event_commit_position, excluded.last_event_prepare_position);
            """;

        foreach (var (details, position) in entries)
        {
            var bc = batch.CreateBatchCommand();
            bc.CommandText = sql;
            bc.Parameters.AddWithValue("stream_id", details.Id);
            bc.Parameters.AddWithValue("original_workflow_id", details.OriginalWorkflowId);
            bc.Parameters.AddWithValue("current_status", details.CurrentStatus);
            bc.Parameters.AddWithValue("started_at", details.StartedAt);
            bc.Parameters.AddWithValue("last_updated_at", details.LastUpdatedAt);
            bc.Parameters.AddWithValue("completed_at", (object?)details.CompletedAt ?? DBNull.Value);
            bc.Parameters.AddWithValue("total_events_processed", details.TotalEventsProcessed);
            bc.Parameters.AddWithValue("step_number", details.StepNumber);
            bc.Parameters.AddWithValue("last_event_commit_position", position.CommitPositionInt64);
            bc.Parameters.AddWithValue("last_event_prepare_position", position.PreparePositionInt64);
            batch.BatchCommands.Add(bc);
        }

        await batch.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
}