using Npgsql;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public sealed class PostgresProjectionCheckpointStore : IProjectionCheckpointStore
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresProjectionSchemaManager _schemaManager;

    public PostgresProjectionCheckpointStore(
        NpgsqlDataSource dataSource,
        PostgresProjectionSchemaManager schemaManager)
    {
        _dataSource = dataSource;
        _schemaManager = schemaManager;
    }

    public async Task<ProjectionCheckpoint?> GetAsync(string projectionName, CancellationToken ct = default)
    {
        await _schemaManager.EnsureCreatedAsync(ct);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select projection_name,
                   last_commit_position,
                   last_prepare_position,
                   updated_at
            from read_models.projection_checkpoints
            where projection_name = @projection_name
            limit 1;
            """;
        command.Parameters.AddWithValue("projection_name", projectionName);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new ProjectionCheckpoint(
            reader.GetString(0),
            ProjectionPosition.FromDatabase(reader.GetInt64(1), reader.GetInt64(2)),
            reader.GetFieldValue<DateTime>(3));
    }

    public async Task StoreAsync(string projectionName, ProjectionPosition position, CancellationToken ct = default)
    {
        await _schemaManager.EnsureCreatedAsync(ct);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into read_models.projection_checkpoints (
                projection_name,
                last_commit_position,
                last_prepare_position,
                updated_at
            )
            values (
                @projection_name,
                @last_commit_position,
                @last_prepare_position,
                now()
            )
            on conflict (projection_name) do update
            set last_commit_position = excluded.last_commit_position,
                last_prepare_position = excluded.last_prepare_position,
                updated_at = now()
            where (read_models.projection_checkpoints.last_commit_position, read_models.projection_checkpoints.last_prepare_position)
                < (excluded.last_commit_position, excluded.last_prepare_position);
            """;
        command.Parameters.AddWithValue("projection_name", projectionName);
        command.Parameters.AddWithValue("last_commit_position", position.CommitPositionInt64);
        command.Parameters.AddWithValue("last_prepare_position", position.PreparePositionInt64);

        await command.ExecuteNonQueryAsync(ct);
    }
}