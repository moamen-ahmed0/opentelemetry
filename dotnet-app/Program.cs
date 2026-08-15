using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Exporter;

// App identity → identifies which service produced the metrics
var resourceBuilder = ResourceBuilder.CreateDefault().AddService("checkout-service");

// Provider → wires resource + OTLP/gRPC exporter + the meter below together.
// Exports via OTLP/gRPC to the collector; requires `kubectl -n otel port-forward
// svc/otel-collector-collector 4317:4317` running so localhost:4317 is reachable.
// The token must match the bearertokenauth extension in manifests/otel-collector.yaml.
using var meterProvider = OpenTelemetry.Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter("checkout-service")
    .AddOtlpExporter((exporterOptions, readerOptions) =>
    {
        exporterOptions.Endpoint = new Uri("http://localhost:4317");
        exporterOptions.Protocol = OtlpExportProtocol.Grpc;
        exporterOptions.Headers = "authorization=Bearer 44930df933a84ab9838328d521f63e8c853b0cf5a56e5ca98a2d51b2fca294dc";
        // Exports collected metrics every 5 seconds.
        readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
    })
    .Build();

// Meter → creates metrics/instruments for this application
var meter = new System.Diagnostics.Metrics.Meter("checkout-service");

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
Console.WriteLine("Sending metrics to localhost:4317 (Ctrl+C to stop)");

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
        latencyHistogram.Record(
            10 + random.NextDouble() * 190,
            new KeyValuePair<string, object?>("route", "/demo"));

        // Simulate requests every second
        await Task.Delay(1000, cts.Token);
    }
}
catch (TaskCanceledException)
{
    // Ctrl+C
}
