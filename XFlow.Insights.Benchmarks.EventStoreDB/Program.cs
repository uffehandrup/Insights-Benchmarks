using BenchmarkDotNet.Running;
using XFlow.Insights.Benchmarks.EventStoreDB.Benchmarks;

/// <summary>
/// EventStoreDB Event Sourcing Benchmark Runner
/// 
/// Purpose: Measure baseline performance of EventStoreDB-backed event sourcing
/// using the gRPC client, testing both inline (synchronous) and batched append operations.
/// 
/// Usage:
///   dotnet run -c Release
/// 
/// This will run comprehensive performance benchmarks using BenchmarkDotNet,
/// measuring append throughput, latency, and memory allocation.
/// </summary>
/// 
try
{
    var summary = BenchmarkRunner.Run<EventStoreDB_WorkflowEventAppendBenchmark>();
    Environment.Exit(summary != null ? 0 : 1);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Benchmark failed: {ex.Message}");
    Environment.Exit(1);
}
