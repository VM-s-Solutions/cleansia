#!/bin/bash
set -euo pipefail

# ============================================================
# Azure Functions Infrastructure Setup for Cleansia
# ============================================================
# This script creates all Azure resources needed to run the
# Cleansia.Functions project (background jobs + PDF generation).
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - Existing resource group and storage account
#
# Usage:
#   chmod +x setup-azure-functions.sh
#   ./setup-azure-functions.sh
# ============================================================

# === Configuration — adjust these for your environment ===
ENV="dev"                                     # "dev" or "pro"
RG="rg-cleansia-${ENV}"
LOCATION="westeurope"
STORAGE_ACCOUNT="stcleansiasdev"              # existing storage account (used for blobs + queues)
ACR_NAME="crcleansia${ENV}"                   # Azure Container Registry name
FUNC_APP_NAME="func-cleansia-${ENV}"
FUNC_PLAN_NAME="asp-func-cleansia-${ENV}"     # separate plan for Functions

# Key Vault (existing)
KV_NAME="kv-cleansia-${ENV}"

# === 0. Register required resource providers ===
echo ""
echo "=== 0. Registering resource providers ==="
az provider register --namespace Microsoft.ContainerRegistry --wait
az provider register --namespace Microsoft.Web --wait
echo "  Resource providers registered"

# === 1. Create Azure Container Registry (if not exists) ===
echo ""
echo "=== 1. Creating Azure Container Registry ==="
if az acr show --name "$ACR_NAME" --resource-group "$RG" &>/dev/null; then
  echo "  ACR '$ACR_NAME' already exists, skipping."
else
  az acr create \
    --name "$ACR_NAME" \
    --resource-group "$RG" \
    --location "$LOCATION" \
    --sku Basic \
    --admin-enabled true \
    --output none
  echo "  Created ACR: $ACR_NAME"
fi

# === 2. Create Azure Functions Container App Plan ===
echo ""
echo "=== 2. Creating Functions App Service Plan (Linux, B1) ==="
if az appservice plan show --name "$FUNC_PLAN_NAME" --resource-group "$RG" &>/dev/null; then
  echo "  Plan '$FUNC_PLAN_NAME' already exists, skipping."
else
  az appservice plan create \
    --name "$FUNC_PLAN_NAME" \
    --resource-group "$RG" \
    --location "$LOCATION" \
    --sku B1 \
    --is-linux \
    --output none
  echo "  Created plan: $FUNC_PLAN_NAME"
fi

# === 3. Create Azure Functions App (Docker container) ===
echo ""
echo "=== 3. Creating Azure Functions App ==="
ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --resource-group "$RG" --query loginServer --output tsv)
STORAGE_CONN_STRING=$(az storage account show-connection-string --name "$STORAGE_ACCOUNT" --resource-group "$RG" --query connectionString --output tsv)

if az functionapp show --name "$FUNC_APP_NAME" --resource-group "$RG" &>/dev/null; then
  echo "  Function app '$FUNC_APP_NAME' already exists, skipping creation."
else
  # Create with a placeholder image (will be replaced by CI/CD)
  az functionapp create \
    --name "$FUNC_APP_NAME" \
    --resource-group "$RG" \
    --plan "$FUNC_PLAN_NAME" \
    --storage-account "$STORAGE_ACCOUNT" \
    --functions-version 4 \
    --runtime custom \
    --deployment-container-image-name "${ACR_LOGIN_SERVER}/cleansia-functions:latest" \
    --output none
  echo "  Created function app: $FUNC_APP_NAME"
fi

# === 4. Enable System Managed Identity ===
echo ""
echo "=== 4. Enabling Managed Identity ==="
PRINCIPAL_ID=$(az functionapp identity assign \
  --name "$FUNC_APP_NAME" \
  --resource-group "$RG" \
  --query principalId --output tsv)
echo "  Principal ID: $PRINCIPAL_ID"

# === 5. Grant ACR Pull permission ===
echo ""
echo "=== 5. Granting ACR Pull role ==="
ACR_ID=$(az acr show --name "$ACR_NAME" --resource-group "$RG" --query id --output tsv)
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "AcrPull" \
  --scope "$ACR_ID" \
  --output none 2>/dev/null || echo "  (Role already assigned)"
echo "  AcrPull role assigned"

# Configure Functions App to pull from ACR with Managed Identity
az functionapp config set \
  --name "$FUNC_APP_NAME" \
  --resource-group "$RG" \
  --linux-fx-version "DOCKER|${ACR_LOGIN_SERVER}/cleansia-functions:latest" \
  --output none

az webapp config appsettings set \
  --name "$FUNC_APP_NAME" \
  --resource-group "$RG" \
  --settings \
    "DOCKER_REGISTRY_SERVER_URL=https://${ACR_LOGIN_SERVER}" \
    "DOCKER_ENABLE_CI=true" \
  --output none

# === 6. Grant Storage Blob Data Contributor role ===
echo ""
echo "=== 6. Granting Storage Blob Data Contributor role ==="
STORAGE_ID=$(az storage account show --name "$STORAGE_ACCOUNT" --resource-group "$RG" --query id --output tsv)
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Storage Blob Data Contributor" \
  --scope "$STORAGE_ID" \
  --output none 2>/dev/null || echo "  (Role already assigned)"
echo "  Storage Blob Data Contributor role assigned"

# === 7. Grant Storage Queue Data Contributor role ===
echo ""
echo "=== 7. Granting Storage Queue Data Contributor role ==="
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Storage Queue Data Contributor" \
  --scope "$STORAGE_ID" \
  --output none 2>/dev/null || echo "  (Role already assigned)"
echo "  Storage Queue Data Contributor role assigned"

# === 8. Grant Key Vault Secrets User role ===
echo ""
echo "=== 8. Granting Key Vault Secrets User role ==="
KV_ID=$(az keyvault show --name "$KV_NAME" --resource-group "$RG" --query id --output tsv)
az role assignment create \
  --assignee "$PRINCIPAL_ID" \
  --role "Key Vault Secrets User" \
  --scope "$KV_ID" \
  --output none 2>/dev/null || echo "  (Role already assigned)"
echo "  Key Vault Secrets User role assigned"

# === 9. Configure App Settings ===
echo ""
echo "=== 9. Configuring App Settings ==="
BLOB_URL="https://${STORAGE_ACCOUNT}.blob.core.windows.net/"

az functionapp config appsettings set \
  --name "$FUNC_APP_NAME" \
  --resource-group "$RG" \
  --settings \
    "AzureWebJobsStorage=${STORAGE_CONN_STRING}" \
    "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" \
    "BlobContainerConfiguration__AccountUrl=${BLOB_URL}" \
    "PUPPETEER_EXECUTABLE_PATH=/usr/local/bin/chromium-stable" \
  --output none
echo "  Base settings configured"

# Key Vault references for secrets
az functionapp config appsettings set \
  --name "$FUNC_APP_NAME" \
  --resource-group "$RG" \
  --settings \
    "ConnectionStrings__ConnectionString=@Microsoft.KeyVault(VaultName=${KV_NAME};SecretName=ConnectionStrings--ConnectionString)" \
    "ConnectionStrings__QueueStorageConnectionString=${STORAGE_CONN_STRING}" \
    "ConnectionStrings__BlobContainerConfigurationConnectionString=@Microsoft.KeyVault(VaultName=${KV_NAME};SecretName=ConnectionStrings--BlobContainerConfigurationConnectionString)" \
    "SendGrid__ApiKey=@Microsoft.KeyVault(VaultName=${KV_NAME};SecretName=SendGrid--ApiKey)" \
    "JwtSettings__Secret=@Microsoft.KeyVault(VaultName=${KV_NAME};SecretName=JwtSettings--Secret)" \
  --output none
echo "  Key Vault references configured"

# === 9b. Connect Application Insights ===
echo ""
echo "=== 9b. Connecting Application Insights ==="
AI_NAME="ai-cleansia-${ENV}"
AI_CONN_STRING=$(az monitor app-insights component show \
  --app "$AI_NAME" \
  --resource-group "$RG" \
  --query connectionString --output tsv 2>/dev/null || echo "")

if [ -n "$AI_CONN_STRING" ]; then
  az functionapp config appsettings set \
    --name "$FUNC_APP_NAME" \
    --resource-group "$RG" \
    --settings \
      "APPLICATIONINSIGHTS_CONNECTION_STRING=${AI_CONN_STRING}" \
    --output none
  echo "  Application Insights connected: $AI_NAME"
else
  echo "  WARNING: Application Insights '$AI_NAME' not found. Skipping."
  echo "  Create it manually: az monitor app-insights component create --app $AI_NAME --resource-group $RG --location $LOCATION"
fi

# === 10. Add Queue connection string to all API App Services ===
echo ""
echo "=== 10. Adding Queue connection string to API App Services ==="
APPS=(
  "api-cleansia-partner-${ENV}"
  "api-cleansia-admin-${ENV}"
  "api-cleansia-customer-${ENV}"
  "api-cleansia-mobile-${ENV}"
)

for APP in "${APPS[@]}"; do
  echo "  $APP"
  az webapp config appsettings set \
    --name "$APP" \
    --resource-group "$RG" \
    --settings "ConnectionStrings__QueueStorageConnectionString=${STORAGE_CONN_STRING}" \
    --output none
done
echo "  Queue connection string added to all APIs"

# === 11. Create required blob containers ===
echo ""
echo "=== 11. Creating blob containers ==="
CONTAINERS=(
  "generated-receipts"
  "generated-invoices"
)

for CONTAINER in "${CONTAINERS[@]}"; do
  az storage container create \
    --name "$CONTAINER" \
    --account-name "$STORAGE_ACCOUNT" \
    --auth-mode login \
    --output none 2>/dev/null || true
  echo "  Container: $CONTAINER"
done

# === 12. Create required storage queues ===
echo ""
echo "=== 12. Creating storage queues ==="
QUEUES=(
  "generate-receipt"
  "generate-receipt-poison"
  "generate-invoice"
  "generate-invoice-poison"
)

for QUEUE in "${QUEUES[@]}"; do
  az storage queue create \
    --name "$QUEUE" \
    --account-name "$STORAGE_ACCOUNT" \
    --auth-mode login \
    --output none 2>/dev/null || true
  echo "  Queue: $QUEUE"
done

# === Done ===
echo ""
echo "============================================"
echo "  Azure Functions setup complete!"
echo "============================================"
echo ""
echo "  Function App:  $FUNC_APP_NAME"
echo "  ACR:           $ACR_NAME ($ACR_LOGIN_SERVER)"
echo "  Plan:          $FUNC_PLAN_NAME (B1 Linux)"
echo ""
echo "  Next steps:"
echo "    1. Add 'ACR_NAME=$ACR_NAME' to GitHub Actions secrets"
echo "    2. Push code to master to trigger CI/CD"
echo "    3. Verify functions appear in Azure Portal"
echo "    4. Upload receipt/invoice HTML templates to blob storage"
echo ""
