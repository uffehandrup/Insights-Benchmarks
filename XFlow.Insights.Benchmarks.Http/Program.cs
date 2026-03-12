using NBomber.CSharp;
using NBomber.Http.CSharp;
using NbHttp = NBomber.Http.CSharp.Http;

namespace XFlow.Insights.Benchmarks.Http;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== XFlow Insights HTTP Benchmark ===");
        Console.WriteLine("This benchmark compares Marten (PostgreSQL) vs EventStoreDB (KurrentDB)");
        Console.WriteLine();
        
        // Parse command line arguments
        var backend = args.Length > 0 ? args[0].ToLower() : "both";
        var workloadMode = args.Length > 1 ? args[1].ToLower() : "new-streams";
        
        Console.WriteLine($"Backend: {backend} (options: postgres, eventstoredb, both)");
        Console.WriteLine($"Workload: {workloadMode} (options: new-streams, reuse-streams)");
        Console.WriteLine();

        if (backend == "both" || backend == "postgres")
        {
            Console.WriteLine("\n[1/2] Testing Marten (PostgreSQL) backend...\n");
            RunBenchmark("Postgres", "http://localhost:8080", workloadMode);
            
            if (backend == "both")
            {
                Console.WriteLine("\nWaiting 10 seconds before next test...");
                Thread.Sleep(10000);
            }
        }

        if (backend == "both" || backend == "eventstoredb" || backend == "eventstore")
        {
            Console.WriteLine("\n[2/2] Testing EventStoreDB (KurrentDB) backend...\n");
            RunBenchmark("EventStoreDB", "http://localhost:8081", workloadMode);
        }

        Console.WriteLine("\n=== Benchmark Complete ===");
    }

    static void RunBenchmark(string backendName, string apiUrl, string workloadMode)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Pre-generate stream IDs for "reuse-streams" mode
        var streamPool = Enumerable.Range(0, 100)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var scenario = Scenario.Create($"Workflow_Start_Load_Test", async context =>
        {
            return await Step.Run("start_workflow", context, async () =>
            {
                Guid streamId;
                
                if (workloadMode == "reuse-streams")
                {
                    // Reuse streams from a pool (simulates append to existing streams)
                    streamId = streamPool[Random.Shared.Next(streamPool.Length)];
                }
                else
                {
                    // Create new stream every request (default mode)
                    streamId = Guid.NewGuid();
                }
                
                var workflowId = Random.Shared.Next(1, 1000000);

                var request = NbHttp.CreateRequest("POST", $"{apiUrl}/api/workflows/start/{streamId}/{workflowId}")
                                  .WithHeader("Content-Type", "application/json")
                                  .WithBody(new StringContent("{\"workflowName\": \"Load Test Workflow\"}", System.Text.Encoding.UTF8, "application/json"));

                return await NbHttp.Send(httpClient, request);
            });
        })
            .WithWarmUpDuration(TimeSpan.FromSeconds(30))
            .WithLoadSimulations(
                // 350 Requests Per Second (RPS) - 10 x the expected average rps
                Simulation.Inject(rate: 350, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
            );

        NBomberRunner
            .RegisterScenarios(scenario)
            .WithTestName($"{backendName}_{workloadMode}")
            .WithReportFileName($"{backendName}_{workloadMode}_report")
            .Run();
    }
}