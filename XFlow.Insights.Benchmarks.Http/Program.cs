using NBomber.CSharp;
using NBomber.Http.CSharp;

namespace XFlow.Insights.Benchmarks.Http;

class Program
{
    static void Main(string[] args)
    {
        using var httpClient = new HttpClient();
        
        // Target URLs based on your docker-compose mappings
        var postgresApiUrl = "http://localhost:8080";
        var eventStoreApiUrl = "http://localhost:8081";

        // Swap to test different DB
        var baseUrl = postgresApiUrl; 

        var step = Step.Create("start_workflow", clientFactory: HttpClientFactory.Create(), execute: async context =>
        {
            var streamId = Guid.NewGuid();
            var workflowId = new Random().Next(1, 10000);
            
            var request = Http.CreateRequest("POST", $"{baseUrl}/api/workflows/start/{streamId}/{workflowId}")
                              .WithHeader("Content-Type", "application/json")
                              .WithBody(new StringContent("{\"workflowName\": \"Load Test Workflow\"}", System.Text.Encoding.UTF8, "application/json"));

            var response = await Http.Send(httpClient, request);

            return response.IsSuccessStatusCode 
                ? Response.Ok(statusCode: (int)response.StatusCode)
                : Response.Fail(statusCode: (int)response.StatusCode);
        });

        var scenario = ScenarioBuilder.CreateScenario("Workflow_Start_Load_Test", step)
            .WithWarmUpDuration(TimeSpan.FromSeconds(5))
            .WithLoadSimulations(
                // 350 Requests Per Second (RPS) - 10 x the expected average rps
                Simulation.Inject(rate: 350, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromMinutes(2))
            );

        NBomberRunner
            .RegisterScenarios(scenario)
            .Run();
    }
}