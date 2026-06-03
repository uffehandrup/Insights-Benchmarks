using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using NBomber.CSharp;
using NBomber.Contracts;
using NBomber.Http.CSharp;
using NbHttp = NBomber.Http.CSharp.Http;

namespace XFlow.Insights.Benchmarks.Http;

internal static class Program
{
    private const int SeedWorkflowCount = 200;
    private const int ActiveWorkflowPoolCapacity = 4096;
    private const int ProjectionPollWorkerCount = 10;
    private const int ProjectionPollAttempts = 20;

    private const int InjectRate = 30;
    private const int ReadRate = 30;
    
    private static readonly TimeSpan ProjectionPollDelay = TimeSpan.FromMilliseconds(50);

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

        var activeWorkflowStreams = new ActiveWorkflowPool(ActiveWorkflowPoolCapacity);
        using var projectionPoller = new ProjectionReadinessPoller(httpClient, apiUrl, activeWorkflowStreams);

        await SeedWorkflowPoolAsync(httpClient, apiUrl, activeWorkflowStreams);

        // Let async projections catch up before dashboard traffic starts.
        await Task.Delay(TimeSpan.FromSeconds(3));

        var writeScenario = CreateWorkflowLifecycleScenario(apiUrl, httpClient, projectionPoller);
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
        ProjectionReadinessPoller projectionPoller)
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
                    // Queue projection readiness checks through a bounded worker pool so
                    // warm-up does not create one long-lived task per completed workflow.
                    projectionPoller.TryEnqueue(streamId);
                }

                return completeResponse;
            });
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(20))
        .WithLoadSimulations(
            // Sustained write pressure with concurrent stream lifecycles.
            Simulation.Inject(rate: InjectRate, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
        );
    }

    private static ScenarioProps CreateDashboardReadScenario(
        string apiUrl,
        HttpClient httpClient,
        ActiveWorkflowPool activeWorkflowStreams)
    {
        return Scenario.Create("Dashboard_Read_Load_Test", async context =>
        {
            return await Step.Run("dashboard_get_workflow", context, async () =>
            {
                var streamId = activeWorkflowStreams.TryGetRandom(out var activeStreamId)
                    ? activeStreamId
                    : Guid.NewGuid();
                var request = NbHttp.CreateRequest("GET", $"{apiUrl}/api/workflows/{streamId}");
                return await NbHttp.Send(httpClient, request);
            });
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(20))
        .WithLoadSimulations(
            // Simulates heavy dashboard reads while writes keep flowing.
            Simulation.Inject(rate: ReadRate, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
        );
    }

    private static async Task SeedWorkflowPoolAsync(
        HttpClient httpClient,
        string apiUrl,
        ActiveWorkflowPool activeWorkflowStreams)
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
                response.Dispose();
                if (response.IsSuccessStatusCode)
                {
                    activeWorkflowStreams.Add(streamId);
                }
            });

        await Task.WhenAll(seedTasks);
        Console.WriteLine($"Seed complete. Active workflow pool size: {activeWorkflowStreams.Count}");
    }

    private sealed class ActiveWorkflowPool
    {
        private readonly Guid[] _buffer;
        private int _count;
        private long _nextIndex = -1;

        public ActiveWorkflowPool(int capacity)
        {
            _buffer = new Guid[capacity];
        }

        public int Count => Math.Min(Volatile.Read(ref _count), _buffer.Length);

        public void Add(Guid streamId)
        {
            var index = (int)(Interlocked.Increment(ref _nextIndex) % _buffer.Length);
            _buffer[index] = streamId;

            while (true)
            {
                var current = Volatile.Read(ref _count);
                if (current >= _buffer.Length)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _count, current + 1, current) == current)
                {
                    return;
                }
            }
        }

        public bool TryGetRandom(out Guid streamId)
        {
            var count = Count;
            if (count == 0)
            {
                streamId = Guid.Empty;
                return false;
            }

            streamId = _buffer[Random.Shared.Next(count)];
            return streamId != Guid.Empty;
        }
    }

    private sealed class ProjectionReadinessPoller : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;
        private readonly ActiveWorkflowPool _activeWorkflowPool;
        private readonly CancellationTokenSource _cts = new();
        private readonly Channel<Guid> _queue;
        private readonly Task[] _workers;

        public ProjectionReadinessPoller(HttpClient httpClient, string apiUrl, ActiveWorkflowPool activeWorkflowPool)
        {
            _httpClient = httpClient;
            _apiUrl = apiUrl;
            _activeWorkflowPool = activeWorkflowPool;
            _queue = Channel.CreateBounded<Guid>(new BoundedChannelOptions(ActiveWorkflowPoolCapacity)
            {
                SingleReader = false,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _workers = Enumerable.Range(0, ProjectionPollWorkerCount)
                .Select(_ => Task.Run(ProcessQueueAsync))
                .ToArray();
        }

        public bool TryEnqueue(Guid streamId) => _queue.Writer.TryWrite(streamId);

        public void Dispose()
        {
            _queue.Writer.TryComplete();
            _cts.Cancel();

            try
            {
                Task.WaitAll(_workers, TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Best-effort shutdown for benchmark helpers.
            }

            _cts.Dispose();
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                await foreach (var streamId in _queue.Reader.ReadAllAsync(_cts.Token))
                {
                    for (var i = 0; i < ProjectionPollAttempts; i++)
                    {
                        try
                        {
                            await Task.Delay(ProjectionPollDelay, _cts.Token);
                            using var check = await _httpClient.GetAsync(
                                $"{_apiUrl}/api/workflows/{streamId}",
                                _cts.Token);

                            if (check.IsSuccessStatusCode)
                            {
                                _activeWorkflowPool.Add(streamId);
                                break;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            return;
                        }
                        catch
                        {
                            // Ignore transient errors during projection warm-up.
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
        }
    }
}
