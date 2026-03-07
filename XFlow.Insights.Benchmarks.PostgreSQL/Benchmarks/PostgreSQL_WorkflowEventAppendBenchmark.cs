using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using Marten;
using XFlow.Insights.Benchmarks.PostgreSQL.Domain;
using XFlow.Insights.Benchmarks.PostgreSQL.Repositories;
using XFlow.Insights.Benchmarks.PostgreSQL.Repositories.PostgreSQL;

namespace XFlow.Insights.Benchmarks.PostgreSQL.Benchmarks;

/// <summary>
/// BenchmarkDotNet class for measuring PostgreSQL event append throughput.
/// Tests both inline (synchronous) and async write-to-read-model pipelines.
/// 
/// Metrics:
/// - Mean execution time per benchmark
/// - Throughput (events per second)
/// - Memory allocation
/// - Min/Max latency
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 3, targetCount: 5)]
public class PostgreSQL_WorkflowEventAppendBenchmark
{
    private const int EventCount = 10_000;

    private IDocumentStore? _documentStore;
    private IWorkflowRepository? _repository;
    private Guid _streamId;
    private List<WorkflowStartedDomainEvent>? _generatedEvents;

    /// <summary>
    /// Global setup: Initialize Marten DocumentStore, create repository, and pre-generate events.
    /// Runs once per benchmark run before any [Benchmark] methods are executed.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        // PostgreSQL connection string for local Docker container
        var connectionString = "Host=localhost;Port=5432;Database=event_sourcing;Username=es_admin;Password=StrongForNow1";

        // Initialize Marten DocumentStore
        _documentStore = DocumentStore.For(options =>
        {
            options.Connection(connectionString);
            options.Events.DatabaseSchemaName = "event_store";
        });

        // Create document session and repository
        var session = _documentStore.LightweightSession();
        _repository = new MartenWorkflowRepository(session);

        // Pre-generate 10,000 domain events for benchmarking
        _streamId = Guid.NewGuid();
        _generatedEvents = GenerateEvents(EventCount);

        Console.WriteLine($"[GlobalSetup] Initialized PostgreSQL benchmark with {EventCount} pre-generated events.");
        Console.WriteLine($"[GlobalSetup] Stream ID: {_streamId}");
        Console.WriteLine($"[GlobalSetup] Connection: {connectionString}");
    }

    /// <summary>
    /// Global cleanup: Dispose of DocumentStore and clean up resources.
    /// Runs once per benchmark run after all [Benchmark] methods are executed.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _documentStore?.Dispose();
        Console.WriteLine("[GlobalCleanup] DocumentStore disposed.");
    }

    /// <summary>
    /// Benchmark: Inline append pattern.
    /// Appends 10,000 events sequentially to a single stream.
    /// Each event triggers SaveChangesAsync() immediately (inline persistence).
    /// 
    /// Measures:
    /// - Total time to append all 10,000 events
    /// - Per-event latency
    /// - Throughput (events/sec)
    /// </summary>
    [Benchmark(Description = "PostgreSQL_InlineAppend - 10k events to single stream")]
    public async Task PostgreSQL_InlineAppend()
    {
        if (_repository == null || _generatedEvents == null)
            throw new InvalidOperationException("Benchmark not properly initialized.");

        foreach (var @event in _generatedEvents)
        {
            await _repository.AppendEventAsync(_streamId, @event);
        }
    }

    /// <summary>
    /// Benchmark: Async append pattern.
    /// Appends 10,000 events to a stream, batching SaveChangesAsync() calls.
    /// Uses a batch size of 100 to simulate asynchronous processing.
    /// 
    /// Measures:
    /// - Total time with batched persistence
    /// - Per-event latency with batch optimization
    /// - Throughput improvement from batching
    /// </summary>
    [Benchmark(Description = "PostgreSQL_BatchedAppend - 10k events batched")]
    public async Task PostgreSQL_BatchedAppend()
    {
        if (_repository == null || _generatedEvents == null)
            throw new InvalidOperationException("Benchmark not properly initialized.");

        const int batchSize = 100;
        var batchEvents = new List<WorkflowStartedDomainEvent>(batchSize);

        foreach (var @event in _generatedEvents)
        {
            batchEvents.Add(@event);

            if (batchEvents.Count >= batchSize)
            {
                // Execute ONE network roundtrip for the entire batch
                await _repository.AppendEventsAsync(_streamId, batchEvents);
                batchEvents.Clear();
            }
        }

        if (batchEvents.Count > 0)
        {
            await _repository.AppendEventsAsync(_streamId, batchEvents);
        }
    }

    /// <summary>
    /// Generates a list of pre-fabricated WorkflowStartedDomainEvent objects.
    /// </summary>
    private List<WorkflowStartedDomainEvent> GenerateEvents(int count)
    {
        var events = new List<WorkflowStartedDomainEvent>(count);

        for (int i = 0; i < count; i++)
        {
            var @event = new WorkflowStartedDomainEvent(
                StreamId: _streamId,
                WorkflowId: i + 1,
                WorkflowName: $"Workflow_{i + 1}",
                StartedAt: DateTime.UtcNow
            );

            events.Add(@event);
        }

        return events;
    }
}
