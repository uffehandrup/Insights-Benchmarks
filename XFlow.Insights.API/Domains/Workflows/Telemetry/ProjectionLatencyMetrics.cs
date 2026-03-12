using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace XFlow.Insights.API.Domains.Workflows.Telemetry;

public static class ProjectionLatencyMetrics
{
    private static readonly Meter Meter = new("XFlow.Insights.WorkflowProjection", "1.0.0");
    private static readonly Histogram<double> ProjectionLatencyMs =
        Meter.CreateHistogram<double>("workflow_projection_latency_ms", unit: "ms");

    public static void Record(
        ILogger logger,
        string backend,
        string eventType,
        Guid streamId,
        int workflowId,
        TimeSpan projectionLatency)
    {
        var latencyMs = projectionLatency.TotalMilliseconds;

        ProjectionLatencyMs.Record(
            latencyMs,
            KeyValuePair.Create<string, object?>("backend", backend),
            KeyValuePair.Create<string, object?>("event_type", eventType));

        logger.LogInformation(
            "ProjectionLatency backend={Backend} eventType={EventType} streamId={StreamId} workflowId={WorkflowId} latencyMs={LatencyMs}",
            backend,
            eventType,
            streamId,
            workflowId,
            latencyMs);
    }
}
