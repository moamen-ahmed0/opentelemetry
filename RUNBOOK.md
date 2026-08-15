# Demo Runbook

## 1. Create the kind cluster

kind create cluster --config kind/kind-config.yaml --name otel
kubectl get nodes

## 2. Install cert-manager (required by the OTel Operator's admission webhook)

kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.21.1/cert-manager.yaml
kubectl -n cert-manager wait --for=condition=Available deployment --all --timeout=120s

## 3. Install the OpenTelemetry Operator

helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts
helm repo update open-telemetry
helm install opentelemetry-operator open-telemetry/opentelemetry-operator \
  --version 0.121.0 \
  --namespace opentelemetry-operator-system --create-namespace
kubectl -n opentelemetry-operator-system wait --for=condition=Available deployment --all --timeout=120s

## 4. Deploy the collector

kubectl create namespace otel
kubectl apply -f manifests/otel-collector.yaml
kubectl -n otel get opentelemetrycollector
kubectl -n otel get pods

## 5. Run the demo

# Terminal 1 - expose the collector's OTLP port
kubectl -n otel port-forward svc/otel-collector-collector 4317:4317

# Terminal 2 - tail the collector's received metrics
kubectl -n otel logs -f deployment/otel-collector-collector
# ...or with k9s: `k9s -n otel`, select the otel-collector-collector pod, press `l` for logs

# Terminal 3 - send metrics (pick one)
cd python-app
python3 -m venv .venv
./.venv/bin/pip install -r requirements.txt
./.venv/bin/python app.py

# ...or the .NET equivalent
cd dotnet-app
dotnet run

## Teardown

kubectl delete -f manifests/otel-collector.yaml
kind delete cluster --name otel
