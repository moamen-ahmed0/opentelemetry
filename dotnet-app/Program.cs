using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;

// --- Settings --------------------------------------------------------------

// Bound from the ObservabilitySettings__* environment variables (double
// underscore is .NET's IConfiguration convention for nested sections), e.g.
//   ObservabilitySettings__ServiceName=simple-service
//   ObservabilitySettings__CollectorUrl=http://localhost:4317
//   ObservabilitySettings__BearerToken=<token>
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ObservabilitySettings:ServiceName"] = "simple-service",
        ["ObservabilitySettings:CollectorUrl"] = "http://localhost:4317",
        ["ObservabilitySettings:BearerToken"] = "44930df933a84ab9838328d521f63e8c853b0cf5a56e5ca98a2d51b2fca294dc",
    })
    .AddEnvironmentVariables()
    .Build();

var settings = configuration.GetSection("ObservabilitySettings").Get<ObservabilitySettings>()
    ?? throw new InvalidOperationException("Missing ObservabilitySettings configuration.");
var authHeader = $"authorization=Bearer {settings.BearerToken}";

// App identity → identifies which service produced the telemetry
var resourceBuilder = ResourceBuilder.CreateDefault().AddService(settings.ServiceName);

// --- Metrics -----------------------------------------------------------

// Provider → wires resource + OTLP/gRPC exporter + the meter below together.
// Exports via OTLP/gRPC to the collector; requires `kubectl -n otel port-forward
// svc/otel-collector-collector 4317:4317` running so CollectorUrl is reachable.
// The token must match the bearertokenauth extension in manifests/otel-collector.yaml.
using var meterProvider = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter(settings.ServiceName)
    .AddOtlpExporter((exporterOptions, readerOptions) =>
    {
        exporterOptions.Endpoint = new Uri(settings.CollectorUrl);
        exporterOptions.Protocol = OtlpExportProtocol.Grpc;
        exporterOptions.Headers = authHeader;
        // Exports collected metrics every 5 seconds.
        readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
    })
    .Build();

// --- Logs ----------------------------------------------------------------

// LoggerFactory bridges Microsoft.Extensions.Logging into OTel's OTLP/gRPC exporter.
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder);
        options.AddOtlpExporter(exporterOptions =>
        {
            exporterOptions.Endpoint = new Uri(settings.CollectorUrl);
            exporterOptions.Protocol = OtlpExportProtocol.Grpc;
            exporterOptions.Headers = authHeader;
        });
    });
});
var logger = loggerFactory.CreateLogger(settings.ServiceName);

// Meter → creates metrics/instruments for this application
var meter = new System.Diagnostics.Metrics.Meter(settings.ServiceName);

// Counter → counts how many requests occur
var requestCounter = meter.CreateCounter<long>(
    "demo.requests",
    description: "Number of demo requests handled");

// Histogram → measures request duration
var latencyHistogram = meter.CreateHistogram<double>(
    "demo.request.duration",
    unit: "ms",
    description: "Duration of demo requests");

// Generate demo telemetry continuously
Console.WriteLine($"Sending metrics and logs to {settings.CollectorUrl} (Ctrl+C to stop)");

var random = new Random();
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    while (!cts.IsCancellationRequested)
    {
        // Record one request
        requestCounter.Add(1, new KeyValuePair<string, object?>("route", "/demo"));

        // Record a random request duration
        var durationMs = 10 + random.NextDouble() * 190;
        latencyHistogram.Record(durationMs, new KeyValuePair<string, object?>("route", "/demo"));

        // Log the same request
        logger.LogInformation("Handled request {Route} in {DurationMs}ms", "/demo", durationMs);

        // Simulate requests every second
        await Task.Delay(1000, cts.Token);
    }
}
catch (TaskCanceledException)
{
    // Ctrl+C
}

record ObservabilitySettings
{
    public required string ServiceName { get; init; }
    public required string CollectorUrl { get; init; }
    public required string BearerToken { get; init; }
}
