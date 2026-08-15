import logging
import os
import random
import time

from opentelemetry import metrics
from opentelemetry._logs import set_logger_provider
from opentelemetry.exporter.otlp.proto.grpc._log_exporter import OTLPLogExporter
from opentelemetry.exporter.otlp.proto.grpc.metric_exporter import OTLPMetricExporter
from opentelemetry.sdk._logs import LoggerProvider, LoggingHandler
from opentelemetry.sdk._logs.export import BatchLogRecordProcessor
from opentelemetry.sdk.metrics import MeterProvider
from opentelemetry.sdk.metrics.export import PeriodicExportingMetricReader
from opentelemetry.sdk.resources import Resource

# Same env var names as dotnet-app/Program.cs (ObservabilitySettings__*), e.g.
#   ObservabilitySettings__ServiceName=simple-service
#   ObservabilitySettings__CollectorUrl=localhost:4317
#   ObservabilitySettings__BearerToken=<token>
SERVICE_NAME = os.environ.get("ObservabilitySettings__ServiceName", "checkout-service")
COLLECTOR_ENDPOINT = os.environ.get("ObservabilitySettings__CollectorUrl", "localhost:4317")
BEARER_TOKEN = os.environ.get(
    "ObservabilitySettings__BearerToken",
    "44930df933a84ab9838328d521f63e8c853b0cf5a56e5ca98a2d51b2fca294dc",
)
AUTH_HEADERS = {"authorization": f"Bearer {BEARER_TOKEN}"}


def setup_metrics(resource: Resource) -> tuple[MeterProvider, metrics.Counter, metrics.Histogram]:
    """Wires up the OTLP metric exporter and creates this app's instruments."""
    exporter = OTLPMetricExporter(
        endpoint=COLLECTOR_ENDPOINT,
        insecure=True,
        headers=AUTH_HEADERS,
    )
    # Exports collected metrics every 5 seconds.
    reader = PeriodicExportingMetricReader(exporter, export_interval_millis=5000)

    provider = MeterProvider(resource=resource, metric_readers=[reader])
    metrics.set_meter_provider(provider)

    meter = metrics.get_meter(SERVICE_NAME)
    request_counter = meter.create_counter(
        "demo.requests",
        description="Number of demo requests handled",
    )
    latency_histogram = meter.create_histogram(
        "demo.request.duration",
        description="Duration of demo requests",
        unit="ms",
    )
    return provider, request_counter, latency_histogram


def setup_logging(resource: Resource) -> tuple[LoggerProvider, logging.Logger]:
    """Wires up the OTLP log exporter and bridges Python's `logging` module into it."""
    exporter = OTLPLogExporter(
        endpoint=COLLECTOR_ENDPOINT,
        insecure=True,
        headers=AUTH_HEADERS,
    )

    provider = LoggerProvider(resource=resource)
    provider.add_log_record_processor(BatchLogRecordProcessor(exporter))
    set_logger_provider(provider)

    # Attach to the root logger so any `logging.getLogger(...)` call in this
    # process gets exported, not just the one returned below.
    handler = LoggingHandler(level=logging.INFO, logger_provider=provider)
    logging.getLogger().addHandler(handler)
    logging.getLogger().setLevel(logging.INFO)

    return provider, logging.getLogger(SERVICE_NAME)


def run(request_counter: metrics.Counter, latency_histogram: metrics.Histogram, logger: logging.Logger) -> None:
    """Simulates one demo request per second until interrupted."""
    print(f"Sending metrics and logs to {COLLECTOR_ENDPOINT} (Ctrl+C to stop)")
    while True:
        request_counter.add(1, {"route": "/demo"})

        duration_ms = random.uniform(10, 200)
        latency_histogram.record(duration_ms, {"route": "/demo"})

        logger.info("Handled request", extra={"route": "/demo", "duration_ms": duration_ms})

        time.sleep(1)


def main() -> None:
    resource = Resource.create({"service.name": SERVICE_NAME})
    meter_provider, request_counter, latency_histogram = setup_metrics(resource)
    logger_provider, logger = setup_logging(resource)

    try:
        run(request_counter, latency_histogram, logger)
    except KeyboardInterrupt:
        pass
    finally:
        meter_provider.shutdown()
        logger_provider.shutdown()


if __name__ == "__main__":
    main()
