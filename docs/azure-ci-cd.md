# CI/CD Workflow: Publish and Deploy

**Author:** Dozer (Platform Engineer)  
**Date:** 2026-08-12  
**Workflow File:** `.github/workflows/publish-and-deploy.yml`  
**Status:** Ready for implementation

---

## Overview

The `publish-and-deploy` workflow automates the build, publish, and deployment of mlx-pep service to Azure Container Apps.

**Trigger:** Push to `main` branch or manual `workflow_dispatch`  
**Duration:** ~5–10 minutes  
**Cost:** Free (GitHub Actions included with public repo)

---

## Workflow Stages

### 1. **build-and-publish** (Parallel, ~3–5 min)

**Inputs:** Source code on push to `main`

**Steps:**

| Step | Duration | Purpose |
|------|----------|---------|
| Checkout | 5s | Clone source code |
| Setup .NET | 10s | Install dotnet CLI 10.0.x |
| Publish linux-x64 | 60s | Single-file publish for x64 architecture |
| Publish linux-arm64 | 60s | Single-file publish for ARM64 architecture |
| Setup Docker Buildx | 5s | Enable multi-arch Docker building |
| Login to ACR | 5s | Authenticate Docker to Azure Container Registry |
| Build & push image | 120s | Build multi-arch Docker image, push to ACR |

**Outputs:**
- Docker images tagged with:
  - `:latest` (always points to main)
  - `:commit-sha` (e.g., `:a1b2c3d`) — immutable reference
  - `:timestamp` (e.g., `:20260812_173234`) — human-readable

**Artifacts:**
- Image in ACR: `mlxpepregistry.azurecr.io/mlx-pep:<tag>`

---

### 2. **deploy-to-container-apps** (Sequential, ~2 min)

**Inputs:** Docker image from step 1

**Dependency:** Waits for `build-and-publish` to complete

**Steps:**

| Step | Duration | Purpose |
|------|----------|---------|
| Checkout | 5s | Clone source code (for reference) |
| Azure CLI login | 15s | Authenticate to Azure using secrets |
| Update Container Apps | 30s | Deploy new image to Container Apps environment |
| Wait for stabilization | 30s | Give app time to initialize |
| Health check | 5–25s | Poll `/health` endpoint up to 5 times (5s intervals) |
| Rollback on failure | 30s | Revert to previous revision if health check fails |

**Outputs:**
- ✅ Success: Deployment live and healthy
- ❌ Failure: Previous revision restored, workflow marked failed

**Health Check Details:**
```bash
# Polls 5 times with 5s interval between attempts
curl https://{CONTAINER_APP_URL}/health

# Expected response:
# 200 OK
# { "status": "healthy" }
```

---

## Configuration and Secrets

### GitHub Actions Secrets Required

Set at https://github.com/matthewcorven/mlx-pep/settings/secrets/actions:

| Secret | Source | Used In |
|--------|--------|---------|
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id` | Azure login (deploy stage) |
| `AZURE_TENANT_ID` | `az account show --query tenantId` | Azure login (deploy stage) |
| `AZURE_CLIENT_ID` | Service principal (optional for MVP) | Azure login (deploy stage) |
| `AZURE_CLIENT_SECRET` | Service principal (optional for MVP) | Azure login (deploy stage) |
| `AZURE_REGISTRY_LOGIN_SERVER` | ACR FQDN (e.g., `mlxpepregistry.azurecr.io`) | Docker login (build stage) |
| `AZURE_REGISTRY_USERNAME` | `az acr credential show --query username` | Docker login (build stage) |
| `AZURE_REGISTRY_PASSWORD` | `az acr credential show --query passwords[0].value` | Docker login (build stage) |
| `AZURE_STORAGE_CONNECTION_STRING` | Blob Storage connection string | (Future use in Container Apps) |

### Environment Variables

The workflow sets these at runtime:

| Variable | Value | Purpose |
|----------|-------|---------|
| `REGISTRY` | `${{ secrets.AZURE_REGISTRY_LOGIN_SERVER }}` | Docker registry URL |
| `IMAGE_NAME` | `mlx-pep` | Image name in registry |
| `DOTNET_CLI_TELEMETRY_OPTOUT` | `true` | Disable telemetry during builds |

---

## Manual Triggering

### Option 1: Via GitHub CLI
```bash
gh workflow run publish-and-deploy.yml
```

### Option 2: Via GitHub UI
1. Go to https://github.com/matthewcorven/mlx-pep/actions
2. Select "Publish and Deploy" workflow
3. Click "Run workflow" → "Run workflow"

### Option 3: Dispatch with Parameters
```bash
# Test build without deploying
gh workflow run publish-and-deploy.yml -f skip_deploy=true
```

---

## Monitoring and Debugging

### View Workflow Runs

```bash
# List all runs
gh run list --workflow publish-and-deploy.yml

# Show latest run details
gh run view --workflow publish-and-deploy.yml --log

# Follow a running workflow
gh run watch --workflow publish-and-deploy.yml
```

### Common Failures

#### ❌ "Image not found in registry"
**Cause:** ACR credentials wrong, or Docker build failed

**Debug:**
```bash
# Check Docker build step output
gh run view <run-id> --log | grep -A 20 "Build and push"

# Verify ACR login manually
az acr login --name mlxpepregistry
az acr repository list --name mlxpepregistry
```

#### ❌ "Health check failed"
**Cause:** Service not responding to `/health`, network timeout, or Container Apps slow to start

**Debug:**
```bash
# Check Container Apps logs
az containerapp logs show \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api \
  --follow

# Test endpoint manually
curl -v https://<CONTAINER_APP_URL>/health

# Check Container Apps revisions
az containerapp revision list \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api
```

#### ❌ "Azure login failed"
**Cause:** Secrets not set, or subscription changed

**Debug:**
```bash
# Verify GitHub secrets are set
gh secret list | grep AZURE

# Test Azure CLI locally
az account show
az containerapp show --resource-group mlx-pep-rg --name mlx-pep-api
```

---

## Performance and Cost

### Execution Time

| Stage | Min | Max | Typical |
|-------|-----|-----|---------|
| **Build & publish** | 2m | 5m | 3m 30s |
| **Docker build & push** | 1m | 3m | 2m |
| **Deploy & health check** | 1m | 3m | 1m 30s |
| **Total** | 4m | 11m | 7m |

### GitHub Actions Cost

- **Free tier:** 2,000 minutes/month on `ubuntu-latest`
- **Typical usage:** ~10 deployments/month = ~70 minutes → **always free**
- **Enterprise usage:** Upgrade if >2,000 minutes/month

### Azure Cost Impact

- **Per-deploy:** $0 (already running Container Apps)
- **Data egress:** ~50 MB per Docker image push → ~$0.01 per deploy

---

## Rollback Strategy

### Automatic Rollback

If health check fails:
1. Workflow detects unhealthy `/health` response
2. Retrieves previous Container Apps revision
3. Activates previous revision (automatic traffic switch)
4. Workflow marked as failed (visible in GitHub Actions UI)

**Effect:** Production automatically reverted within ~1 min of failed health check

### Manual Rollback

If automatic rollback doesn't work:

```bash
# List revisions
az containerapp revision list \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api \
  --query "[*].[name, properties.template.containers[0].image, properties.provisioning.state]" \
  -o table

# Activate previous revision
az containerapp revision activate \
  --resource-group mlx-pep-rg \
  --name mlx-pep-api \
  --revision mlx-pep-api--<revision-id>
```

---

## Future Improvements (Phase 2+)

- [ ] **Staging environment:** Separate Container Apps for pre-prod testing
- [ ] **Canary deployments:** Route 10% traffic to new image, verify, then full rollout
- [ ] **Manual approval gate:** Require review before deploy to production
- [ ] **Scheduled deploys:** Deploy only during business hours
- [ ] **Azure Key Vault integration:** Store secrets in Key Vault instead of GitHub
- [ ] **OIDC authentication:** Replace static credentials with federated identity
- [ ] **Application Insights:** Auto-instrument with telemetry and dashboards

---

## Workflow YAML Reference

**File:** `.github/workflows/publish-and-deploy.yml`

**Key configuration:**

```yaml
# Trigger on main push + manual dispatch
on:
  push:
    branches: [main]
  workflow_dispatch:

# Build stage: ubuntu-latest, .NET 10.0.x
jobs:
  build-and-publish:
    runs-on: ubuntu-latest
    
    # Publish for x64 and arm64
    - dotnet publish -r linux-x64
    - dotnet publish -r linux-arm64
    
    # Build multi-arch Docker image
    - docker/build-push-action@v5
      platforms: linux/amd64,linux/arm64

  # Deploy stage: waits for build, requires production environment
  deploy-to-container-apps:
    needs: build-and-publish
    environment: production
    
    # Azure login with service principal
    - azure/login@v1
    
    # Update Container Apps with new image
    - az containerapp update --image ...
    
    # Health check loop
    - curl https://{url}/health (retry 5x)
    
    # Rollback on failure
    - az containerapp revision activate (previous)
```

---

## References

- [Workflow syntax](https://docs.github.com/actions/using-workflows/workflow-syntax-for-github-actions)
- [Docker build-push-action](https://github.com/docker/build-push-action)
- [Azure login action](https://github.com/Azure/login)
- [Azure Container Apps CLI](https://learn.microsoft.com/cli/azure/containerapp)
