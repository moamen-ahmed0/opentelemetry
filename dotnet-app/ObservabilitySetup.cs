using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

// Wires up OTel metrics + logs export for the app. Exports via OTLP/gRPC to the
// collector; requires `kubectl -n otel port-forward svc/otel-collector-collector
// 4317:4317` running so CollectorUrl is reachable. The bearer token must match
// the bearertokenauth extension in manifests/otel-collector.yaml.
sealed class ObservabilitySetup : IDisposable
{
    public ObservabilitySettings Settings { get; }
    public ILogger Logger { get; }
    public System.Diagnostics.Metrics.Counter<long> RequestCounter { get; }
    public System.Diagnostics.Metrics.Histogram<double> LatencyHistogram { get; }

    private readonly MeterProvider _meterProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly System.Diagnostics.Metrics.Meter _meter;

    public ObservabilitySetup(ObservabilitySettings settings)
    {
        Settings = settings;

        var authHeader = $"authorization=Bearer {settings.ObservabilitySettings__BearerToken}";
        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(settings.ObservabilitySettings__ServiceName);

        _meterProvider = CreateMeterProvider(settings, resourceBuilder, authHeader);
        _loggerFactory = CreateLoggerFactory(settings, resourceBuilder, authHeader);
        Logger = _loggerFactory.CreateLogger(settings.ObservabilitySettings__ServiceName);

        _meter = new System.Diagnostics.Metrics.Meter(settings.ObservabilitySettings__ServiceName);
        RequestCounter = _meter.CreateCounter<long>(
            "demo.requests",
            description: "Number of demo requests handled");
        LatencyHistogram = _meter.CreateHistogram<double>(
            "demo.request.duration",
            unit: "ms",
            description: "Duration of demo requests");
    }

    public static ObservabilitySettings LoadSettings()
    {
        // appsettings.json keys are flat and already match the env var names
        // below, so it's read as-is; AddEnvironmentVariables() would instead
        // translate "__" into ":" and break that match, so env var overrides
        // are applied by reading the raw process environment directly.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        return new ObservabilitySettings
        {
            ObservabilitySettings__ServiceName = GetValue(configuration, "ObservabilitySettings__ServiceName"),
            ObservabilitySettings__CollectorUrl = GetValue(configuration, "ObservabilitySettings__CollectorUrl"),
            ObservabilitySettings__BearerToken = GetValue(configuration, "ObservabilitySettings__BearerToken"),
        };
    }

    private static string GetValue(IConfiguration configuration, string name)
    {
        return Environment.GetEnvironmentVariable(name)
            ?? configuration[name]
            ?? throw new InvalidOperationException($"Missing {name} configuration.");
    }

    // Provider → wires resource + OTLP/gRPC exporter + the meter together.
    private static MeterProvider CreateMeterProvider(ObservabilitySettings settings, ResourceBuilder resourceBuilder, string authHeader)
    {
        return OpenTelemetry.Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter(settings.ObservabilitySettings__ServiceName)
            .AddOtlpExporter((exporterOptions, readerOptions) =>
            {
                exporterOptions.Endpoint = new Uri(settings.ObservabilitySettings__CollectorUrl);
                exporterOptions.Protocol = OtlpExportProtocol.Grpc;
                exporterOptions.Headers = authHeader;
                // Exports collected metrics every 5 seconds.
                readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 5000;
            })
            .Build();
    }

    // LoggerFactory bridges Microsoft.Extensions.Logging into OTel's OTLP/gRPC exporter.
    private static ILoggerFactory CreateLoggerFactory(ObservabilitySettings settings, ResourceBuilder resourceBuilder, string authHeader)
    {
        return LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = new Uri(settings.ObservabilitySettings__CollectorUrl);
                    exporterOptions.Protocol = OtlpExportProtocol.Grpc;
                    exporterOptions.Headers = authHeader;
                });
            });
        });
    }

    public void Dispose()
    {
        _meterProvider.Dispose();
        _loggerFactory.Dispose();
        _meter.Dispose();
    }
}
