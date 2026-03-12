using System.Collections.Concurrent;
using System.Text;
using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Http.CSharp;
using NbHttp = NBomber.Http.CSharp.Http;

namespace XFlow.Insights.Benchmarks.Http;

internal static class Program
{
    private const int SeedWorkflowCount = 200;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== XFlow Insights HTTP Benchmark ===");
        Console.WriteLine("Macro benchmark: lifecycle writes + dashboard reads (parallel)");
        Console.WriteLine();

        var backend = args.Length > 0 ? args[0].ToLowerInvariant() : "both";

        Console.WriteLine($"Backend: {backend} (options: postgres, eventstoredb, both)");
        Console.WriteLine();

        if (backend == "both" || backend == "postgres")
        {
            Console.WriteLine("\n[1/2] Testing Marten (PostgreSQL) backend...\n");
            await RunBenchmarkAsync("Postgres", "http://localhost:8080");

            if (backend == "both")
            {
                Console.WriteLine("\nWaiting 10 seconds before next test...");
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }

        if (backend == "both" || backend == "eventstoredb" || backend == "eventstore")
        {
            Console.WriteLine("\n[2/2] Testing EventStoreDB backend...\n");
            await RunBenchmarkAsync("EventStoreDB", "http://localhost:8081");
        }

        Console.WriteLine("\n=== Benchmark Complete ===");
    }

    private static async Task RunBenchmarkAsync(string backendName, string apiUrl)
    {
        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var activeWorkflowStreams = new ConcurrentBag<Guid>();

        await SeedWorkflowPoolAsync(httpClient, apiUrl, activeWorkflowStreams);

        // Let async projections catch up before dashboard traffic starts.
        await Task.Delay(TimeSpan.FromSeconds(3));

        var writeScenario = CreateWorkflowLifecycleScenario(apiUrl, httpClient, activeWorkflowStreams);
        var readScenario = CreateDashboardReadScenario(apiUrl, httpClient, activeWorkflowStreams);

        NBomberRunner
            .RegisterScenarios(writeScenario, readScenario)
            .WithTestName($"{backendName}_LifecyclePlusDashboard")
            .WithReportFileName($"{backendName}_LifecyclePlusDashboard_report")
            .Run();
    }

    private static ScenarioProps CreateWorkflowLifecycleScenario(
        string apiUrl,
        HttpClient httpClient,
        ConcurrentBag<Guid> activeWorkflowStreams)
    {
        return Scenario.Create("Workflow_Lifecycle_Write_Test", async context =>
        {
            return await Step.Run("workflow_lifecycle_write", context, async () =>
            {
                var streamId = Guid.NewGuid();
                var workflowId = Random.Shared.Next(1, 1_000_000);

                var startRequest = NbHttp.CreateRequest("POST", $"{apiUrl}/api/workflows/start/{streamId}/{workflowId}")
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(new StringContent("{\"workflowName\":\"Benchmark Workflow\"}", Encoding.UTF8, "application/json"));

                var startResponse = await NbHttp.Send(httpClient, startRequest);
                if (startResponse.IsError)
                {
                    return startResponse;
                }

                for (var stepNumber = 1; stepNumber <= 3; stepNumber++)
                {
                    var stepRequest = NbHttp.CreateRequest("POST", $"{apiUrl}/api/workflows/{streamId}/{workflowId}/step-completed")
                        .WithHeader("Content-Type", "application/json")
                        .WithBody(new StringContent($"{{\"stepNumber\":{stepNumber}}}", Encoding.UTF8, "application/json"));

                    var stepResponse = await NbHttp.Send(httpClient, stepRequest);
                    if (stepResponse.IsError)
                    {
                        return stepResponse;
                    }
                }

                var completeRequest = NbHttp.CreateRequest("POST", $"{apiUrl}/api/workflows/{streamId}/{workflowId}/complete")
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(new StringContent("{\"finalStatus\":\"Completed\"}", Encoding.UTF8, "application/json"));

                var completeResponse = await NbHttp.Send(httpClient, completeRequest);
                if (!completeResponse.IsError)
                {
                    // Fire-and-forget: add to the read pool only once the projection has caught up.
                    // This keeps write latency unaffected while preventing 404s on reads.
                    _ = Task.Run(async () =>
                    {
                        for (var i = 0; i < 40; i++)
                        {
                            await Task.Delay(50);
                            try
                            {
                                var check = await httpClient.GetAsync($"{apiUrl}/api/workflows/{streamId}");
                                if (check.IsSuccessStatusCode)
                                {
                                    activeWorkflowStreams.Add(streamId);
                                    return;
                                }
                            }
                            catch { /* ignore transient errors during projection warm-up */ }
                        }
                    });
                }

                return completeResponse;
            });
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(20))
        .WithLoadSimulations(
            // Sustained write pressure with concurrent stream lifecycles.
            Simulation.Inject(rate: 70, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
        );
    }

    private static ScenarioProps CreateDashboardReadScenario(
        string apiUrl,
        HttpClient httpClient,
        ConcurrentBag<Guid> activeWorkflowStreams)
    {
        return Scenario.Create("Dashboard_Read_Load_Test", async context =>
        {
            return await Step.Run("dashboard_get_workflow", context, async () =>
            {
                var pool = activeWorkflowStreams.ToArray();
                var streamId = pool.Length == 0
                    ? Guid.NewGuid()
                    : pool[Random.Shared.Next(pool.Length)];
                var request = NbHttp.CreateRequest("GET", $"{apiUrl}/api/workflows/{streamId}");
                return await NbHttp.Send(httpClient, request);
            });
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(20))
        .WithLoadSimulations(
            // Simulates heavy dashboard reads while writes keep flowing.
            Simulation.Inject(rate: 350, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
        );
    }

    private static async Task SeedWorkflowPoolAsync(
        HttpClient httpClient,
        string apiUrl,
        ConcurrentBag<Guid> activeWorkflowStreams)
    {
        Console.WriteLine($"Seeding {SeedWorkflowCount} active workflows for dashboard read pool...");

        var seedTasks = Enumerable.Range(0, SeedWorkflowCount)
            .Select(async _ =>
            {
                var streamId = Guid.NewGuid();
                var workflowId = Random.Shared.Next(1, 1_000_000);

                var startRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{apiUrl}/api/workflows/start/{streamId}/{workflowId}")
                {
                    Content = new StringContent("{\"workflowName\":\"Seeded Workflow\"}", Encoding.UTF8, "application/json")
                };

                var response = await httpClient.SendAsync(startRequest);
                if (response.IsSuccessStatusCode)
                {
                    activeWorkflowStreams.Add(streamId);
                }
            });

        await Task.WhenAll(seedTasks);
        Console.WriteLine($"Seed complete. Active workflow pool size: {activeWorkflowStreams.Count}");
    }
}
