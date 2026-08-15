# Prerequisites

Tools needed to run this demo, tested with the versions below (older/newer patch versions
should work fine too).

| Tool | Tested version | Install |
|---|---|---|
| [Docker](https://www.docker.com/) | 27.5.1 | `brew install --cask docker` |
| [kind](https://kind.sigs.k8s.io/) | 0.32.0 | `brew install kind` |
| [kubectl](https://kubernetes.io/docs/tasks/tools/) | 1.33 | `brew install kubectl` |
| [Helm](https://helm.sh/) | 3.16 | `brew install helm` |
| [Python](https://www.python.org/) | 3.14 | `brew install python3` |
| [.NET SDK](https://dotnet.microsoft.com/) | 10.0 | `brew install --cask dotnet-sdk` |

Optional:

| Tool | Purpose |
|---|---|
| [k9s](https://k9scli.io/) | Terminal UI for browsing the cluster/pod logs instead of raw `kubectl` |

Docker must be running (`open -a Docker`) before creating the kind cluster.
