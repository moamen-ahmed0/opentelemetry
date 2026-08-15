import random
import time

from opentelemetry import metrics
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import Resource

# App identity → identifies which service produced the metrics
resource = Resource.create({"service.name": "checkout-service"})

# Exporter → sends metrics to the OTel Collector via OTLP/gRPC
exporter = OTLPMetricExporter(
    endpoint="localhost:4317",
    insecure=True,
    headers={"authorization": "Bearer 44930df933a84ab9838328d521f63e8c853b0cf5a56e5ca98a2d51b2fca294dc"},
)

# Reader → exports collected metrics every 5 seconds
reader = PeriodicExportingMetricReader(
    exporter,
    export_interval_millis=5000,
)

# Provider → manages the application's metrics
provider = MeterProvider(
    resource=resource,
    metric_readers=[reader],
)
metrics.set_meter_provider(provider)

# Meter → creates metrics/instruments for this application
meter = metrics.get_meter("checkout-service")

# Counter → counts how many requests occur
request_counter = meter.create_counter(
    "demo.requests",
    description="Number of demo requests handled",
)

# Histogram → measures request duration
latency_histogram = meter.create_histogram(
    "demo.request.duration",
    description="Duration of demo requests",
    unit="ms",
)

# Generate demo telemetry continuously
if __name__ == "__main__":
    print("Sending metrics to localhost:4317 (Ctrl+C to stop)")

    try:
        while True:
            # Record one request
            request_counter.add(1, {"route": "/demo"})

            # Record a random request duration
            latency_histogram.record(
                random.uniform(10, 200),
                {"route": "/demo"},
            )

            # Simulate requests every second
            time.sleep(1)

    except KeyboardInterrupt:
        pass

    finally:
        # Flush and shut down telemetry
        provider.shutdown()
