using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using EventStore.Client;
using XFlow.Insights.Benchmarks.EventStoreDB.Domain;
using XFlow.Insights.Benchmarks.EventStoreDB.Repositories;
using XFlow.Insights.Benchmarks.EventStoreDB.Repositories.EventStoreDB;

namespace XFlow.Insights.Benchmarks.EventStoreDB.Benchmarks;

/// <summary>
/// BenchmarkDotNet class for measuring EventStoreDB event append throughput.
/// Tests both inline (synchronous) and batched append patterns.
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
public class EventStoreDB_WorkflowEventAppendBenchmark
{
    private const int EventCount = 10_000;

    private EventStoreClient? _client;
    private IWorkflowRepository? _repository;
    private Guid _streamId;
    private List<WorkflowStartedDomainEvent>? _generatedEvents;

    /// <summary>
    /// Global setup: Initialize EventStoreDB client, create repository, and pre-generate events.
    /// Runs once per benchmark run before any [Benchmark] methods are executed.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        // EventStoreDB connection string for local Docker container
        var connectionString = "esdb://admin:changeit@localhost:2113?tls=false";

        // Initialize EventStoreDB client with gRPC
        var settings = EventStoreClientSettings.Create(connectionString);
        _client = new EventStoreClient(settings);

        // Create repository
        _repository = new EventStoreDbWorkflowRepository(_client);

        // Pre-generate 10,000 domain events for benchmarking
        _streamId = Guid.NewGuid();
        _generatedEvents = GenerateEvents(EventCount);

        Console.WriteLine($"[GlobalSetup] Initialized EventStoreDB benchmark with {EventCount} pre-generated events.");
        Console.WriteLine($"[GlobalSetup] Stream ID: {_streamId}");
        Console.WriteLine($"[GlobalSetup] Connection: {connectionString}");
    }

    /// <summary>
    /// Global cleanup: Dispose of EventStoreDB client and clean up resources.
    /// Runs once per benchmark run after all [Benchmark] methods are executed.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _client?.Dispose();
        Console.WriteLine("[GlobalCleanup] EventStoreDB client disposed.");
    }

    /// <summary>
    /// Benchmark: Inline append pattern.
    /// Appends 10,000 events sequentially to a single stream.
    /// Each event triggers AppendToStreamAsync() immediately (inline persistence).
    /// 
    /// Measures:
    /// - Total time to append all 10,000 events
    /// - Per-event latency
    /// - Throughput (events/sec)
    /// </summary>
    [Benchmark(Description = "EventStoreDB_InlineAppend - 10k events to single stream")]
    public async Task EventStoreDB_InlineAppend()
    {
        if (_repository == null || _generatedEvents == null)
            throw new InvalidOperationException("Benchmark not properly initialized.");

        foreach (var @event in _generatedEvents)
        {
            await _repository.AppendEventAsync(_streamId, @event);
        }
    }

    /// <summary>
    /// Benchmark: Batched append pattern.
    /// Appends 10,000 events to a stream, batching AppendToStreamAsync() calls.
    /// Uses a batch size of 100 to minimize network roundtrips.
    /// 
    /// Measures:
    /// - Total time with batched persistence
    /// - Per-event latency with batch optimization
    /// - Throughput improvement from batching
    /// </summary>
    [Benchmark(Description = "EventStoreDB_BatchedAppend - 10k events batched")]
    public async Task EventStoreDB_BatchedAppend()
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

        // Handle remaining events (if any)
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
