# Azure Deployment Infrastructure for mlx-pep

**Author:** Dozer (Platform Engineer)  
**Date:** 2026-08-12  
**Phase:** Phase 1b of deployment strategy  
**Status:** Implementation in progress

---

## Overview

This document describes the Azure infrastructure setup for mlx-pep service, including:
- Azure Resource Group, Container Registry, and Container Apps Environment
- Configuration and secrets management
- Cost estimates and scaling policies
- Troubleshooting and monitoring

**Target Architecture:** ASP.NET Core 10 service running in Azure Container Apps (serverless), with images stored in Azure Container Registry and profile data in Azure Blob Storage.

---

## Azure Resource Hierarchy

```
Subscription (Azure account)
└── Resource Group: mlx-pep-rg
    ├── Container Registry: mlxpepregistry.azurecr.io
    ├── Container Apps Environment: mlx-pep-env
    │   └── Container App: mlx-pep-api
    │       ├── Revisions (auto-managed)
    │       └── Ingress: https://mlx-pep-api.xxx.azurecontainerapps.io
    └── Storage Account: <existing or new>
        └── Blob Container: profiles
```

---

## Prerequisites

- **Azure Subscription:** Active subscription with at least $50 credit
- **Azure CLI:** Version 2.50+ installed and authenticated (`az login`)
- **GitHub Repository:** Access to https://github.com/matthewcorven/mlx-pep
- **Docker:** (optional for local testing; CI/CD handles building)

---

## Infrastructure Setup

### 1. Automated Setup (Recommended)

Run the provided setup script:

```bash
chmod +x scripts/setup-azure-infra.sh
./scripts/setup-azure-infra.sh
```

This script will:
1. Create resource group `mlx-pep-rg` in `eastus`
2. Create Azure Container Registry `mlxpepregistry`
3. Create Container Apps environment `mlx-pep-env`
4. Enable ACR admin user for GitHub Actions
5. Output secrets needed for GitHub Actions configuration

### 2. Manual Setup (Advanced)

If you prefer to set up resources individually:

#### Create Resource Group
```bash
az group create --name mlx-pep-rg --location eastus
```

#### Create Azure Container Registry
```bash
az acr create \
  --resource-group mlx-pep-rg \
  --name mlxpepregistry \
  --sku Basic

# Enable admin user for CI/CD authentication
az acr update --resource-group mlx-pep-rg --name mlxpepregistry --admin-enabled true
```

#### Create Container Apps Environment
```bash
az containerapp env create \
  --resource-group mlx-pep-rg \
  --name mlx-pep-env \
  --location eastus
```

#### Create Container App Instance
After the CI/CD workflow pushes an image to ACR:

```bash
az containerapp create \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api \
  --environment mlx-pep-env \
  --image mlxpepregistry.azurecr.io/mlx-pep:latest \
  --target-port 5000 \
  --ingress external \
  --registry-server mlxpepregistry.azurecr.io \
  --registry-username <acr-username> \
  --registry-password <acr-password> \
  --secrets blob-connection=<blob-connection-string> \
  --env-vars ASPNETCORE_ENVIRONMENT=Production \
             ConnectionStrings__AzureBlobStorage=secretRef:blob-connection
```

---

## GitHub Actions Secrets Configuration

After running the setup script, configure the following GitHub Actions secrets at  
https://github.com/matthewcorven/mlx-pep/settings/secrets/actions:

| Secret Name | Source | Purpose |
|---|---|---|
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` | Azure subscription identifier for workflow authentication |
| `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` | Azure AD tenant for login |
| `AZURE_CLIENT_ID` | (Optional; set if using service principal) | Service principal app ID |
| `AZURE_CLIENT_SECRET` | (Optional; set if using service principal) | Service principal secret |
| `AZURE_REGISTRY_LOGIN_SERVER` | ACR login server (e.g., `mlxpepregistry.azurecr.io`) | Docker registry URL |
| `AZURE_REGISTRY_USERNAME` | `az acr credential show --name mlxpepregistry --query username -o tsv` | ACR admin username |
| `AZURE_REGISTRY_PASSWORD` | `az acr credential show --name mlxpepregistry --query passwords[0].value -o tsv` | ACR admin password |
| `AZURE_STORAGE_CONNECTION_STRING` | Blob Storage connection string | Blob Storage access for profile data |

---

## Secrets and Configuration Management

### Application Configuration

The mlx-pep service reads configuration from multiple sources (in precedence order):

1. **Environment variables** (CI/CD sets these)
2. **`appsettings.json`** (checked into repo, no secrets)
3. **`appsettings.Development.json`** (local dev only, not in repo)
4. **Azure Key Vault** (optional, Phase 2)

### Connection Strings in Container Apps

Container Apps environment secrets are passed as environment variables to the container:

```bash
--secrets blob-connection=<actual-connection-string> \
--env-vars ConnectionStrings__AzureBlobStorage=secretRef:blob-connection
```

The ASP.NET Core configuration system automatically converts `ConnectionStrings__AzureBlobStorage` environment variable to the `ConnectionStrings:AzureBlobStorage` configuration key.

### Blob Storage Connection String

Obtain from Azure portal or CLI:

```bash
STORAGE_ACCOUNT_NAME="<your-storage-account>"
STORAGE_KEY=$(az storage account keys list \
  --resource-group mlx-pep-rg \
  --account-name "$STORAGE_ACCOUNT_NAME" \
  --query "[0].value" -o tsv)

CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=$STORAGE_ACCOUNT_NAME;AccountKey=$STORAGE_KEY;EndpointSuffix=core.windows.net"
```

Store this in GitHub secret `AZURE_STORAGE_CONNECTION_STRING`.

---

## Cost Estimates (MVP Tier)

| Component | Tier | Est. Monthly Cost | Notes |
|-----------|------|-------------------|-------|
| **Container Apps** | 2 vCPU, pay-per-millisecond | $15–25 | Scales from zero; charges only when running |
| **Azure Container Registry** | Basic | $5–10 | Small artifact storage |
| **Blob Storage** | Standard, pay-per-GB | $1–3 | Small profile database |
| **Total** | — | **~$20–40/month** | Development/MVP tier; production may cost more |

**Cost Optimization Tips:**
- Set Container Apps to scale to zero when idle (configured via auto-scaling policy)
- Use Azure Blob Storage lifecycle policies to archive old data
- Monitor usage monthly via Azure Cost Management

---

## Health Checks and Monitoring

### Health Endpoint

The mlx-pep service exposes a `/health` endpoint (see `src/MlxPep.Service/Program.cs`):

```csharp
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
```

Container Apps uses this for:
- **Startup probe:** Validates service is ready (30s timeout, 3 retries)
- **Liveness probe:** Validates service is healthy (10s interval)
- **Readiness probe:** Validates service can accept traffic

### Viewing Logs

#### Azure Container Apps Logs
```bash
az containerapp logs show \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api \
  --follow
```

#### Azure Portal Console
Visit https://portal.azure.com → Container Apps → mlx-pep-api → Monitoring → Log stream

#### Application Insights (Phase 2)
Once instrumented, query traces and metrics at https://portal.azure.com → Application Insights

---

## Deployment Process

### Automatic Deployment (GitHub Actions)

When you push to `main` branch:

1. **Trigger:** `.github/workflows/publish-and-deploy.yml` starts
2. **Build:** Publishes single-file binaries (linux-x64, linux-arm64)
3. **Docker:** Builds multi-arch Docker image
4. **Push:** Pushes image to ACR with tags (`:latest`, `:commit-sha`, `:timestamp`)
5. **Deploy:** Updates Container Apps to new image
6. **Health Check:** Waits 30s, then probes `/health` endpoint
7. **Rollback:** If health check fails, reverts to previous image

**Timeline:** ~5–10 minutes from push to live

### Manual Deployment (Testing)

Trigger workflow manually:

```bash
gh workflow run publish-and-deploy.yml
```

Or via GitHub UI: Actions → Publish and Deploy → Run workflow

---

## Troubleshooting

### Deployment Fails: "Image not found in registry"

**Cause:** ACR credentials not set, or image push failed

**Fix:**
1. Check GitHub secrets are set: `AZURE_REGISTRY_LOGIN_SERVER`, `AZURE_REGISTRY_USERNAME`, `AZURE_REGISTRY_PASSWORD`
2. Check workflow run output for Docker build errors
3. Verify ACR login: `az acr login --name mlxpepregistry`

### Health Check Fails After Deploy

**Cause:** Service not responding to `/health`, or Container Apps not fully initialized

**Fix:**
1. Check service logs: `az containerapp logs show --resource-group mlx-pep-rg --name mlx-pep-api --follow`
2. SSH into container: (Not directly available; use Exec in portal if enabled)
3. Verify `/health` locally: `curl http://localhost:5000/health`
4. Check Container Apps ingress FQDN: `az containerapp show --resource-group mlx-pep-rg --name mlx-pep-api --query properties.configuration.ingress.fqdn -o tsv`

### Container Apps Revision Stuck in "Provisioning"

**Cause:** Image pull timeout or resource constraints

**Fix:**
1. Check image size: `az acr repository show --name mlxpepregistry --repository mlx-pep --query imageSizeBytes -o tsv`
2. Check ACR pull rate limits (Basic tier: 3 concurrent pulls)
3. Check Container Apps environment logs in portal

### Cost Overages

**Cause:** Service running continuously at high compute tier

**Fix:**
1. Check auto-scaling policy: `az containerapp show --resource-group mlx-pep-rg --name mlx-pep-api --query properties.template.scale -o json`
2. Set scale policy to min 0, max 2 instances: Covered in Phase 2 hardening
3. Monitor usage: Azure portal → Cost Management

---

## Rollback Procedure

If a deployment breaks production:

### Automatic Rollback (Implemented in CI/CD)
Health check failure → Workflow automatically reverts to previous Container Apps revision

### Manual Rollback
```bash
# List revisions
az containerapp revision list \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api

# Activate previous revision
az containerapp revision activate \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api \
  --revision <revision-name>
```

---

## Next Steps

1. **Run setup script:** `./scripts/setup-azure-infra.sh`
2. **Configure GitHub secrets** with output from script
3. **Wait for PR #54 to merge** (Tank's Dockerfile + health endpoint)
4. **First deployment:** Push to `main` → CI/CD workflow triggers automatically
5. **Verify:** Check `/health` endpoint at Container Apps public URL
6. **Monitor:** Use `az containerapp logs show` or portal Log stream

---

## References

- [Azure Container Apps Docs](https://learn.microsoft.com/azure/container-apps/)
- [Azure Container Registry Docs](https://learn.microsoft.com/azure/container-registry/)
- [GitHub Actions Azure Login](https://github.com/Azure/login)
- [ASP.NET Core Configuration](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/)
