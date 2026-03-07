using BenchmarkDotNet.Running;
using XFlow.Insights.Benchmarks.PostgreSQL.Benchmarks;

/// <summary>
/// PostgreSQL Event Sourcing Benchmark Runner
/// 
/// Purpose: Measure baseline performance of PostgreSQL-backed event sourcing
/// using Marten, testing both inline (synchronous) and async write-to-read-model pipelines.
/// 
/// Usage:
///   dotnet run -c Release
/// 
/// This will run comprehensive performance benchmarks using BenchmarkDotNet,
/// measuring append throughput, latency, and memory allocation.
/// </summary>

try
{
    var summary = BenchmarkRunner.Run<PostgreSQL_WorkflowEventAppendBenchmark>();
    Environment.Exit(summary != null ? 0 : 1);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Benchmark failed: {ex.Message}");
    Environment.Exit(1);
}
