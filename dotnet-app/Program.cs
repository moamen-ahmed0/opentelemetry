using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;

// --- Settings --------------------------------------------------------------

var settings = LoadSettings();
var authHeader = $"authorization=Bearer {settings.BearerToken}";

// App identity → identifies which service produced the telemetry
var resourceBuilder = ResourceBuilder.CreateDefault().AddService(settings.ServiceName);

// --- Metrics / Logs --------------------------------------------------------

// Exports via OTLP/gRPC to the collector; requires `kubectl -n otel port-forward
// svc/otel-collector-collector 4317:4317` running so CollectorUrl is reachable.
// The token must match the bearertokenauth extension in manifests/otel-collector.yaml.
using var meterProvider = CreateMeterProvider(settings, resourceBuilder, authHeader);
using var loggerFactory = CreateLoggerFactory(settings, resourceBuilder, authHeader);
var logger = loggerFactory.CreateLogger(settings.ServiceName);

var meter = new System.Diagnostics.Metrics.Meter(settings.ServiceName);
var requestCounter = meter.CreateCounter<long>(
    "demo.requests",
    description: "Number of demo requests handled");
var latencyHistogram = meter.CreateHistogram<double>(
    "demo.request.duration",
    unit: "ms",
    description: "Duration of demo requests");

// --- Run ---------------------------------------------------------------

Console.WriteLine($"Sending metrics and logs to {settings.CollectorUrl} (Ctrl+C to stop)");
await RunDemoLoopAsync(logger, requestCounter, latencyHistogram);

// --- Functions ---------------------------------------------------------

// Bound from the ObservabilitySettings__* environment variables (double
// underscore is .NET's IConfiguration convention for nested sections), e.g.
//   ObservabilitySettings__ServiceName=simple-service
//   ObservabilitySettings__CollectorUrl=http://localhost:4317
//   ObservabilitySettings__BearerToken=<token>
static ObservabilitySettings LoadSettings()
{
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ObservabilitySettings:ServiceName"] = "simple-service",
            ["ObservabilitySettings:CollectorUrl"] = "http://localhost:4317",
            ["ObservabilitySettings:BearerToken"] = "44930df933a84ab9838328d521f63e8c853b0cf5a56e5ca98a2d51b2fca294dc",
        })
        .AddEnvironmentVariables()
        .Build();

    return configuration.GetSection("ObservabilitySettings").Get<ObservabilitySettings>()
        ?? throw new InvalidOperationException("Missing ObservabilitySettings configuration.");
}

// Provider → wires resource + OTLP/gRPC exporter + the meter below together.
static MeterProvider CreateMeterProvider(ObservabilitySettings settings, ResourceBuilder resourceBuilder, string authHeader)
{
    return OpenTelemetry.Sdk.CreateMeterProviderBuilder()
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
}

// LoggerFactory bridges Microsoft.Extensions.Logging into OTel's OTLP/gRPC exporter.
static ILoggerFactory CreateLoggerFactory(ObservabilitySettings settings, ResourceBuilder resourceBuilder, string authHeader)
{
    return LoggerFactory.Create(builder =>
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
}

// Generates demo telemetry continuously until Ctrl+C.
static async Task RunDemoLoopAsync(
    ILogger logger,
    System.Diagnostics.Metrics.Counter<long> requestCounter,
    System.Diagnostics.Metrics.Histogram<double> latencyHistogram)
{
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
}

record ObservabilitySettings
{
    public required string ServiceName { get; init; }
    public required string CollectorUrl { get; init; }
    public required string BearerToken { get; init; }
}
