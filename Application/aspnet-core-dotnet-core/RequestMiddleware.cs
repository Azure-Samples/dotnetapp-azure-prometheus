using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Prometheus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

public class RequestMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger _logger;
    private TelemetryClient _telemetryClient;

    public RequestMiddleware(
        RequestDelegate next, ILoggerFactory loggerFactory, TelemetryClient telemetryClient
        )
    {
        this._next = next;
        this._logger = loggerFactory.CreateLogger<RequestMiddleware>();
        this._telemetryClient = telemetryClient;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value;
        var method = httpContext.Request.Method;

        //GetMetric - Custom Metrics Configuration
        Metric countRequests = _telemetryClient.GetMetric("getmetric_count_requests", "path", "method");
        Metric sumMemory = _telemetryClient.GetMetric("getmetric_sum_memory");

        //Prometheus - Metrics Configuration
        TimeSpan elapsed = new TimeSpan();

        var counter = Metrics.CreateCounter("prom_counter_request_total", "requests Total", new CounterConfiguration
        {
            LabelNames = new[] { "path", "method", "status" }
        });
        var histogram = Metrics.CreateHistogram("prom_histogram_request_duration",
                "The duration in seconds between the response to a request.", new HistogramConfiguration
                {
                    Buckets = Histogram.ExponentialBuckets(0.01, 2, 10),
                    LabelNames = new[] { "path", "method", "status" }
                });

        var summary = Metrics.CreateSummary("prom_summary_memory", "summary of allocated memory", new SummaryConfiguration
        {
            Objectives = new[]
            {
                new QuantileEpsilonPair(0.5, 0.05),
                new QuantileEpsilonPair(0.9, 0.05),
                new QuantileEpsilonPair(0.95, 0.01),
                new QuantileEpsilonPair(0.99, 0.005),
            }
        });

        var gauge = Metrics.CreateGauge("prom_gauge_memory", "allocated memory");

        var statusCode = 200;

        try
        {
            await _next.Invoke(httpContext);
        }
        catch (Exception)
        {
            statusCode = 500;
            counter.Labels(path, method, statusCode.ToString()).Inc();

            throw;
        }

        if (path is not "/metrics" and not "/health")
        {
            //Logger
            _logger.LogWarning("Request: {path} {method}", path, method);

            //GetMetric
            using (_telemetryClient.StartOperation<RequestTelemetry>("operation"))
            {
                countRequests.TrackValue(1, path, method);
                sumMemory.TrackValue(GC.GetAllocatedBytesForCurrentThread());
            }

            //Prometheus
            statusCode = httpContext.Response.StatusCode;
            counter.Labels(path, method, statusCode.ToString()).Inc();
            histogram.Labels(path, method, statusCode.ToString()).Observe(elapsed.TotalSeconds);
            summary.Observe(GC.GetAllocatedBytesForCurrentThread());
            gauge.Set(GC.GetTotalMemory(false));
        }
    }
}

public static class RequestMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestMiddleware>();
    }
}