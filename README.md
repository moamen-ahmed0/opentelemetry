# OpenTelemetry Demo

A minimal, end-to-end OpenTelemetry demo: a local kind Kubernetes cluster running the
OpenTelemetry Collector (via the OpenTelemetry Operator), receiving metrics over
OTLP/gRPC from a sample app — available in both Python and .NET — and printing them
to the collector's logs.

```
demo app (Python or .NET)  --OTLP/gRPC, bearer token-->  OTel Collector (in kind)  --> debug exporter (pod logs)
```

## Prerequisites

See [PREREQUISITES.md](PREREQUISITES.md).

## Quick start

Full step-by-step instructions are in [RUNBOOK.md](RUNBOOK.md). Summary:

1. Create the kind cluster (`kind/kind-config.yaml`, one control-plane + one worker node).
2. Install cert-manager and the OpenTelemetry Operator.
3. Apply `manifests/otel-collector.yaml` to deploy the collector.
4. Port-forward the collector's OTLP port and tail its logs.
5. Run the Python or .NET demo app to send metrics.

## Repo layout

| Path | What it is |
|---|---|
| `kind/kind-config.yaml` | kind cluster topology (control-plane + worker) |
| `manifests/otel-collector.yaml` | `OpenTelemetryCollector` custom resource — the collector's own config |
| `manifests/otel-collector-defaults.yaml` | Reference only: every default the CRD/operator fills in that isn't set above |
| `python-app/` | Python demo app (OTel Python SDK) |
| `dotnet-app/` | .NET demo app (OTel .NET SDK), same metrics as the Python app |
| `RUNBOOK.md` | Full step-by-step commands to stand up and tear down the demo |

## What the collector requires

The collector's OTLP receiver requires a bearer token (see the `bearertokenauth`
extension in `manifests/otel-collector.yaml`) — both demo apps already send it. It also
tags every metric with a `deployment.environment` and `collector.name` resource
attribute via the `resource` processor.

## Teardown

```
kubectl delete -f manifests/otel-collector.yaml
kind delete cluster --name otel
```
