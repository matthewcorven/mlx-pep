#!/bin/bash
# Phase 1b: Azure Infrastructure Setup
# Sets up resource group, ACR, Container Apps environment, and secrets for mlx-pep
# Usage: ./scripts/setup-azure-infra.sh

set -euo pipefail

# Configuration
RESOURCE_GROUP="mlx-pep-rg"
LOCATION="eastus"
REGISTRY_NAME="mlxpepregistry"
CONTAINER_APP_NAME="mlx-pep-api"
CONTAINER_APP_ENV="mlx-pep-env"
IMAGE_NAME="${REGISTRY_NAME}.azurecr.io/mlx-pep"

echo "=== Phase 1b: Azure Infrastructure Setup ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"
echo "Registry: $REGISTRY_NAME"
echo "Container App: $CONTAINER_APP_NAME"
echo ""

# 1. Create Resource Group
echo "[1/6] Creating resource group..."
if az group exists --name "$RESOURCE_GROUP" | grep -q true; then
    echo "  ✓ Resource group '$RESOURCE_GROUP' already exists"
else
    az group create --name "$RESOURCE_GROUP" --location "$LOCATION"
    echo "  ✓ Created resource group"
fi

# 2. Create Container Registry
echo "[2/6] Creating Azure Container Registry..."
if az acr show --resource-group "$RESOURCE_GROUP" --name "$REGISTRY_NAME" &>/dev/null; then
    echo "  ✓ Registry '$REGISTRY_NAME' already exists"
    REGISTRY_URL=$(az acr show --resource-group "$RESOURCE_GROUP" --name "$REGISTRY_NAME" --query loginServer -o tsv)
else
    az acr create \
        --resource-group "$RESOURCE_GROUP" \
        --name "$REGISTRY_NAME" \
        --sku Basic
    REGISTRY_URL=$(az acr show --resource-group "$RESOURCE_GROUP" --name "$REGISTRY_NAME" --query loginServer -o tsv)
    echo "  ✓ Created registry at $REGISTRY_URL"
fi

# 3. Create Container Apps Environment
echo "[3/6] Creating Container Apps environment..."
if az containerapp env show --resource-group "$RESOURCE_GROUP" --name "$CONTAINER_APP_ENV" &>/dev/null; then
    echo "  ✓ Environment '$CONTAINER_APP_ENV' already exists"
else
    az containerapp env create \
        --resource-group "$RESOURCE_GROUP" \
        --name "$CONTAINER_APP_ENV" \
        --location "$LOCATION"
    echo "  ✓ Created Container Apps environment"
fi

# 4. Enable admin user on ACR for GitHub Actions authentication
echo "[4/6] Enabling ACR admin user for CI/CD authentication..."
az acr update --resource-group "$RESOURCE_GROUP" --name "$REGISTRY_NAME" --admin-enabled true
ACR_USERNAME=$(az acr credential show --resource-group "$RESOURCE_GROUP" --name "$REGISTRY_NAME" --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --resource-group "$RESOURCE_GROUP" --name "$REGISTRY_NAME" --query passwords[0].value -o tsv)
echo "  ✓ ACR admin user enabled"
echo "  Registry URL: $REGISTRY_URL"
echo "  Username: $ACR_USERNAME"

# 5. Get subscription and tenant info for GitHub Actions secrets
echo "[5/6] Gathering Azure subscription info..."
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "  ✓ Subscription ID: $SUBSCRIPTION_ID"
echo "  ✓ Tenant ID: $TENANT_ID"

# 6. Check Blob Storage (assume it exists or will be created manually)
echo "[6/6] Checking Blob Storage configuration..."
# For MVP, we assume Blob Storage connection string is obtained separately
echo "  ℹ Blob Storage connection string must be set separately"
echo "  Store in GitHub secret: AZURE_STORAGE_CONNECTION_STRING"

# Summary
echo ""
echo "=== GitHub Actions Secrets to Configure ==="
echo "Set these in: https://github.com/matthewcorven/mlx-pep/settings/secrets/actions"
echo ""
echo "AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID"
echo "AZURE_TENANT_ID=$TENANT_ID"
echo "AZURE_REGISTRY_LOGIN_SERVER=$REGISTRY_URL"
echo "AZURE_REGISTRY_USERNAME=$ACR_USERNAME"
echo "AZURE_REGISTRY_PASSWORD=$ACR_PASSWORD"
echo ""
echo "⚠ CRITICAL: Also set these secrets:"
echo "AZURE_STORAGE_CONNECTION_STRING=<your-blob-connection-string>"
echo ""
echo "=== Azure Infrastructure Summary ==="
echo "✓ Resource Group: $RESOURCE_GROUP (location: $LOCATION)"
echo "✓ Container Registry: $REGISTRY_NAME ($REGISTRY_URL)"
echo "✓ Container Apps Environment: $CONTAINER_APP_ENV"
echo "✓ Next: Deploy first container app instance"
echo ""
echo "To deploy a container app manually:"
echo "  az containerapp create \\"
echo "    --resource-group $RESOURCE_GROUP \\"
echo "    --name $CONTAINER_APP_NAME \\"
echo "    --environment $CONTAINER_APP_ENV \\"
echo "    --image $IMAGE_NAME:latest \\"
echo "    --target-port 5000 \\"
echo "    --ingress external \\"
echo "    --registry-server $REGISTRY_URL \\"
echo "    --registry-username $ACR_USERNAME \\"
echo "    --registry-password <password> \\"
echo "    --secrets blob-conn=<blob-connection-string> \\"
echo "    --env-vars ASPNETCORE_ENVIRONMENT=Production \\"
echo "               ConnectionStrings__AzureBlobStorage=secretRef:blob-conn"
echo ""
