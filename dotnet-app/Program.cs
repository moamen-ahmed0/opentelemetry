using Microsoft.Extensions.Logging;

var settings = ObservabilitySetup.LoadSettings();
using var observability = new ObservabilitySetup(settings);

Console.WriteLine($"Sending metrics and logs to {settings.ObservabilitySettings__CollectorUrl} (Ctrl+C to stop)");
await RunDemoLoopAsync(observability);

// Generates demo telemetry continuously until Ctrl+C.
static async Task RunDemoLoopAsync(ObservabilitySetup observability)
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
            observability.RequestCounter.Add(1, new KeyValuePair<string, object?>("route", "/demo"));

            // Record a random request duration
            var durationMs = 10 + random.NextDouble() * 190;
            observability.LatencyHistogram.Record(durationMs, new KeyValuePair<string, object?>("route", "/demo"));

            // Log the same request
            observability.Logger.LogInformation("Handled request {Route} in {DurationMs}ms", "/demo", durationMs);

            // Simulate requests every second
            await Task.Delay(1000, cts.Token);
        }
    }
    catch (TaskCanceledException)
    {
        // Ctrl+C
    }
}
