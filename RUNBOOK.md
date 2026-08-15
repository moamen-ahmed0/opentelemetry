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

## 5. Build and deploy the demo apps in-cluster

# Build the images
docker build -t python-app:local ./python-app
docker build -t dotnet-app:local ./dotnet-app

# Load them into kind (kind nodes can't see the local Docker image store otherwise)
kind load docker-image python-app:local --name otel
kind load docker-image dotnet-app:local --name otel

# Deploy both apps as pods in the otel namespace
kubectl apply -f manifests/python-app.yaml
kubectl apply -f manifests/dotnet-app.yaml
kubectl -n otel get pods

## 6. Watch the telemetry flow

# Tail the collector's received metrics/logs
kubectl -n otel logs -f deployment/otel-collector-collector
# ...or with k9s: `k9s -n otel`, select the otel-collector-collector pod, press `l` for logs

# Tail an app's own output
kubectl -n otel logs -f deployment/python-app
kubectl -n otel logs -f deployment/dotnet-app

## Teardown

kubectl delete -f manifests/python-app.yaml -f manifests/dotnet-app.yaml -f manifests/otel-collector.yaml
kind delete cluster --name otel
