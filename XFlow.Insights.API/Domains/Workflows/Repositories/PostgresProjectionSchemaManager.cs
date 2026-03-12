using Npgsql;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

public sealed class PostgresProjectionSchemaManager
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public PostgresProjectionSchemaManager(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task EnsureCreatedAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                create schema if not exists read_models;

                create table if not exists read_models.workflow_details (
                    stream_id uuid primary key,
                    original_workflow_id integer not null,
                    current_status text not null,
                    started_at timestamptz not null,
                    last_updated_at timestamptz not null,
                    completed_at timestamptz null,
                    total_events_processed integer not null,
                    step_number integer not null,
                    last_event_commit_position bigint not null,
                    last_event_prepare_position bigint not null,
                    row_version bigint not null default 0,
                    updated_at timestamptz not null default now()
                );

                create table if not exists read_models.projection_checkpoints (
                    projection_name text primary key,
                    last_commit_position bigint not null,
                    last_prepare_position bigint not null,
                    updated_at timestamptz not null default now()
                );
                """;

            await command.ExecuteNonQueryAsync(ct);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }
}