---
page_type: sample
description: Sample .NET Core Web app that demonstrates different implementations for pre-aggregated metrics.
languages:
  - csharp
name: Pre-aggregated Metrics - .NET Core app with Prometheus and Azure Monitor
products:
  - azure
  - dotnet-core
urlFragment: dotnet-azure-prometheus
---

# Pre-aggregated Metrics - .NET Core app with Prometheus and Azure Monitor

![workflow](https://github.com/Azure-Samples/dotnetapp-azure-prometheus/actions/workflows/devops-starter-workflow.yml/badge.svg)

- [Pre-aggregated Metrics - .NET Core app with Prometheus and Azure Monitor](#pre-aggregated-metrics---net-core-app-with-prometheus-and-azure-monitor)
  - [Overview](#overview)
  - [Getting Started](#getting-started)
    - [Prerequisites](#prerequisites)
    - [Quickstart - Running the App Locally](#quickstart---running-the-app-locally)
    - [Deploy Application to Azure Kubernetes Service to Collect Metrics](#deploy-application-to-azure-kubernetes-service-to-collect-metrics)
    - [Install the Prometheus Server](#install-the-prometheus-server)
  - [Prometheus scraping with Azure Monitor](#prometheus-scraping-with-azure-monitor)
  - [Pod Annotations for Scraping](#pod-annotations-for-scraping)
  - [Run the Application and Collect Metrics](#run-the-application-and-collect-metrics)
  - [Optionally Install Grafana](#optionally-install-grafana)
    - [Setup Configuration on Grafana](#setup-configuration-on-grafana)
  - [Resources](#resources)

## Overview

Sample .NET Core Web app that demonstrates different implementations for pre-aggregated metrics. Prometheus and Azure Monitor are two popular choices. However, they each offer differing capabilities. This repository offers examples for 3 different options. It is possible to use just one or all three depending on the scenario.

1. The Prometheus-Net .NET library is used to export [Prometheus-specific metrics](https://prometheus.io/docs/concepts/metric_types/).
2. Agent configuration is used to scrape Prometheus metrics with Azure Monitor. These metrics then populate Container logs [InsightsMetrics](https://docs.microsoft.com/en-us/azure/azure-monitor/reference/tables/insightsmetrics).
3. Application Insights .NET Core SDK is used to populate CustomMetrics using the [GetMetric method](https://docs.microsoft.com/en-us/azure/azure-monitor/app/get-metric).

A couple of steps to take special note of:

- A Prometheus server installed on the cluster is configured to collect metrics from all pods.
- The RequestMiddleware.cs class in the sample application contains the metrics configuration for both Prometheus and GetMetric.

## Getting Started

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli?view=azure-cli-latest): Create and manage Azure resources.
- [Kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/): Kubernetes command-line tool which allows you to run commands against Kubernetes clusters.
- [Helm](https://helm.sh/docs/intro/install/): Package manager for Kubernetes
- [Docker](https://docs.docker.com/desktop/)
- [GitHub](https://github.com/) account

### Quickstart - Running the App Locally

Verify the sample application is able to run locally. In order to collect metrics, please continue to the [next section](#deploy-application-to-azure-kubernetes-service-to-collect-metrics) to deploy the app to AKS.

1. Fork [this repo](https://github.com/Azure-Samples/dotnetapp-azure-prometheus/) to your github account and git clone
2. cd `dotnetapp-azure-prometheus/Application`
3. Run `docker-compose up` and go to <http://localhost:8080> to interact with the application.

### Deploy Application to Azure Kubernetes Service to Collect Metrics

1. Create a resource group that will hold all the created resources and a service principal to manage and access those resources

   ```bash
   # Set your variables
   #Resource group to hold the resources for this application
   RESOURCEGROUPNAME="insert-resource-group-name-here"
   LOCATION="insert-location-here"
   #Azure subscription ID. Can be located in the Azure portal.
   SUBSCRIPTIONID="insert-subscription-id-here"
   SERVICEPRINCIPAL="insert-service-principal-here"

   # login to azure if not already logged in from the cli
   az login

   # Create resource group
   az group create --name $RESOURCEGROUPNAME --location $LOCATION

   # Create a service principal with Contributor role to the resource group
   az ad sp create-for-rbac --name $SERVICEPRINCIPAL --role contributor --scopes /subscriptions/$SUBSCRIPTIONID/resourceGroups/$RESOURCEGROUPNAME --sdk-auth
   ```

   **CAUTION:** There is a known bug with git bash. Git Bash will attempt to auto-translate resource IDs. If you encounter this issue, it can be fixed by appending MSYS_NO_PATHCONV=1 to the command. [See this link for further information.](https://github.com/fengzhou-msft/azure-cli/blob/ea149713de505fa0f8ae6bfa5d998e12fc8ff509/doc/use_cli_with_git_bash.md)

2. Use the output of the last command as a secret named `AZURE_CREDENTIALS` in the repository settings (Settings -> Secrets -> New repository secret). Set this as a secret on the repository not on the environment. For more details on configuring the github repository secrets, please see [this guide](https://docs.microsoft.com/en-us/azure/azure-resource-manager/templates/deploy-github-actions#configure-the-github-secrets)

3. [Github Actions](https://docs.github.com/en/actions) will be used to automate the workflow and deploy all the necessary resources to Azure. Open the [.github\workflows\devops-starter-workflow.yml](.github\workflows\devops-starter-workflow.yml) and change the environment variables accordingly. Use the `RESOURCEGROUPNAME` and value that you created above. Be sure to change at a minimum the named variables, such as the `RESOURCEGROUPNAME` and the `REGISTRYNAME`. The `REGISTRYNAME` identifies the container registry, and it is a globally unique name. The deployment will fail if this value is not unique. [This resource can guide you with naming conventions.](https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules)

4. Commit your changes. The commit will trigger the build and deploy jobs within the workflow and will provision all the resources to run the sample application.

### Install the Prometheus Server

```bash

# Define variables
RESOURCE_GROUP="insert-resource-group-here"
CLUSTER_NAME="insert-cluster-name-here"
NAMESPACE="insert-namespace-here"

# Connect to Cluster
az aks get-credentials --resource-group $RESOURCE_GROUP --name $CLUSTER_NAME

# Set the default namespace to the application namespace
kubectl config set-context --current --namespace=$NAMESPACE

helm repo add stable https://charts.helm.sh/stable

helm repo add prometheus-community https://prometheus-community.github.io/helm-charts

helm repo add kube-state-metrics https://kubernetes.github.io/kube-state-metrics

helm repo update

helm install my-prometheus prometheus-community/prometheus --set server.service.type=LoadBalancer --set rbac.create=false

# Verify the installation by looking at your services
kubectl get services

# Connect your service with Prometheus
helm upgrade my-prometheus prometheus-community/prometheus --set server.service.type=LoadBalancer --set rbac.create=false -f Application/manifests/prometheus.values.yaml
```

## Prometheus scraping with Azure Monitor

For Prometheus scraping with Azure Monitor, a Prometheus server is not required. The configMap `container-azm-ms-agentconfig.yaml`, enables scraping of Prometheus metrics from each pod in the cluster and has been configured according to the following:

```yml
prometheus-data-collection-settings: |-
# Custom Prometheus metrics data collection settings
[prometheus_data_collection_settings.cluster]
interval = "1m"
# Metrics for Prometheus scraping
fieldpass=["prom_counter_request_total", "prom_histogram_request_duration", "prom_summary_memory", "prom_gauge_memory"]
monitor_kubernetes_pods = true
```

Run the following command to apply this configMap configuration to the cluster:

```bash
kubectl apply -f Application/manifests/container-azm-ms-agentconfig.yaml
```

### Pod Annotations for Scraping

To configure Prometheus to collect metrics from all pods the following annotations were added to the app [deployment.yaml](Application/charts/sampleapp/templates/deployment.yaml)

```yml
annotations:
  prometheus.io/scrape: "true"
  prometheus.io/port: "80"
```

## Collect Metrics

1. Get the IP addresses of the sampleapp and the prometheus-server:

   ```bash
   kubectl get services sampleapp
   ```

2. Load the sampleapp endpoint and interact with the menu items (Home, About, Contact). Pre-aggregated metrics are configured in the [RequestMiddleware.cs](Application/aspnet-core-dotnet-core/RequestMiddleware.cs). They are available with the following implementations:

   - **CustomMetrics**: Implementation of metrics using the AppInsights .NET Core SDK and `TelemetryClient.GetMetric`:

     ```kql
     # Example query that gets the metric for total requests

     customMetrics
     | where name == "getmetric_count_requests"
     | extend customDimensions.path
     | order by timestamp desc
     ```

     ![custom-metrics](./assets/custom-metrics.png)

   - **Prometheus metrics**: Implementation of Prometheus metrics using the [prometheus-net](https://github.com/prometheus-net/prometheus-net) .NET library and the `/metrics` endpoint:

     ![prometheus-metrics](./assets/prometheus-metrics.png)

   Prometheus metrics are scraped using the following:

   - **InsightsMetrics**: Agent configuration for scraping with Azure Monitor:

     ```kql
     # Example query that gets the prometheus metric for total requests

     InsightMetrics
     | where name == "prom_counter_request_total"
     | where parse_json(Tags).method == "GET"
     | extend path = parse_json(Tags).path
     ```

     ![insights-metrics](./assets/insights-metrics.png)

   - **Prometheus Server**:

     - Get the prometheus server IP address:

       ```bash
       kubectl get services my-prometheus-server
       ```

     - Load the prometheus server endpoint. The cluster is configured to collect metrics from all pods:

       ![prometheus-server](./assets/prometheus-server.png)

## Optionally Install Grafana

Grafana can be optionally installed to visualize the web application data and metrics collected once connected with the data source.

```bash
helm repo add grafana https://grafana.github.io/helm-charts

helm repo update

helm install my-grafana grafana/grafana  --set rbac.create=false --set service.type=LoadBalancer  --set persistence.enabled=true

# Verify
kubectl get services

```

### Setup Configuration on Grafana

1. Get the IP address of the Grafana Dashboard
2. Login with user `admin`. Get the password:

   ```bash
   kubectl get secret my-grafana -o jsonpath="{.data.admin-password}" | base64 --decode ; echo
   ```

3. Follow the [setup guide](https://medium.com/faun/monitoring-with-prometheus-and-grafana-in-kubernetes-42727866562c) to get a starter dashboard for Kubernetes

## License:

See [LICENSE](LICENSE).

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Contributing

See [CONTRIBUTING](CONTRIBUTING.MD)
