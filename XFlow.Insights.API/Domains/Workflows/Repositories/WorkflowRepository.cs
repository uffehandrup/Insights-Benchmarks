using Marten;
using Microsoft.Extensions.Logging;
using XFlow.Insights.API.Domains;
using XFlow.Insights.API.Domains.Workflows.DomainEvents;

namespace XFlow.Insights.API.Domains.Workflows.Repositories;

/// <summary>
/// Marten-based implementation for appending events directly to the event store.
/// Uses IDocumentStore to create fresh sessions per operation for optimal concurrency handling.
/// </summary>
public class WorkflowRepository : IWorkflowRepository
{
    private readonly IDocumentStore _store;
    private readonly ILogger<WorkflowRepository> _logger;

    public WorkflowRepository(IDocumentStore store, ILogger<WorkflowRepository> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task AppendEventAsync(Guid streamId, DomainEvent @event, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int maxRetries = 3; // Reduced from 5 to avoid timeout cascade
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Create a fresh lightweight session per attempt
                await using var session = _store.LightweightSession();
                
                // Append handles both new and existing streams automatically
                session.Events.Append(streamId, @event);
                await session.SaveChangesAsync(token: ct);
                
                sw.Stop();
                if (attempt > 0)
                {
                    _logger.LogInformation("[Marten] AppendEvent succeeded after {Attempts} retries in {ElapsedMs}ms for stream {StreamId}", 
                        attempt, sw.ElapsedMilliseconds, streamId);
                }
                return; // Success!
            }
            catch (JasperFx.Events.EventStreamUnexpectedMaxEventIdException) when (attempt < maxRetries - 1)
            {
                // Concurrent append conflict - retry with minimal backoff
                var delayMs = (attempt + 1) * 5; // 5ms, 10ms linear backoff (reduced from exponential)
                _logger.LogWarning("[Marten] Event version conflict on stream {StreamId}, retry {Attempt} in {DelayMs}ms", 
                    streamId, attempt + 1, delayMs);
                
                await Task.Delay(delayMs, ct);
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                // Duplicate key constraint violations - check which one
                if (ex.ConstraintName == "pkey_mt_streams_id")
                {
                    // Stream already exists - this is OK, just means another request created it first
                    // Retry immediately without delay (stream exists now)
                    if (attempt < maxRetries - 1)
                    {
                        _logger.LogDebug("[Marten] Stream {StreamId} already exists, retrying append", streamId);
                        continue;
                    }
                }
                else if (ex.ConstraintName?.Contains("stream_and_version") == true)
                {
                    // Event version conflict - retry with small delay
                    if (attempt < maxRetries - 1)
                    {
                        var delayMs = (attempt + 1) * 5;
                        _logger.LogWarning("[Marten] Event version conflict on stream {StreamId}, retry {Attempt} in {DelayMs}ms", 
                            streamId, attempt + 1, delayMs);
                        await Task.Delay(delayMs, ct);
                        continue;
                    }
                }
                
                // Unknown constraint or last retry - throw
                sw.Stop();
                _logger.LogError(ex, "[Marten] Constraint violation ({Constraint}) after {ElapsedMs}ms for stream {StreamId}", 
                    ex.ConstraintName, sw.ElapsedMilliseconds, streamId);
                throw;
            }
        }
        
        // All retries exhausted
        sw.Stop();
        _logger.LogError("[Marten] Failed to append event after {MaxRetries} attempts and {ElapsedMs}ms for stream {StreamId}", 
            maxRetries, sw.ElapsedMilliseconds, streamId);
        throw new InvalidOperationException($"Failed to append event to stream {streamId} after {maxRetries} attempts due to concurrency conflicts");
    }
}
