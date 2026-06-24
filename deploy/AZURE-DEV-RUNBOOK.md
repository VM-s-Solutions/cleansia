# Azure DEV environment — provisioning runbook

> Everything the **owner** must do to take the Cleansia platform from "Bicep authored" to
> "dev environment live and serving the iOS/web clients." Source of truth: **ADR-0015** (Azure deploy),
> **ADR-0017** (region seam). The Bicep lives at [`deploy/bicep/`](bicep/). Region: **West Europe** (`weu`).
>
> **Hard rule:** Claude/agents author the declarative artifacts (Bicep, YAML). Only the **owner** runs
> `az`, creates GitHub Environments, and populates real secret values. This runbook is the owner's part.

---

## 0. What gets created (the full footprint)

One subscription, one resource group `rg-cleansia-weu-dev`, containing:

| # | Resource | Name | Notes |
|---|---|---|---|
| 1 | App Service Plan | `plan-cleansia-weu-dev` | B2 Linux |
| 2 | API App Service ×5 | `api-cleansia-{partner,admin,customer,partner-mobile,customer-mobile}-weu-dev` | .NET 10 |
| 3 | SSR App Service | `web-cleansia-customer-weu-dev` | Node 20 (Angular SSR) |
| 4 | Static Web App ×2 | `swa-cleansia-{partner,admin}-weu-dev` | Free tier |
| 5 | Function App | `func-cleansia-weu-dev` | container (ACR), dotnet-isolated |
| 6 | Container Registry | `acrcleansiaweudev` | Basic, admin disabled (MI pull) |
| 7 | PostgreSQL Flexible | `pg-cleansia-weu-dev` | Burstable B1ms, PG 16, TLS-required |
| 8 | Storage Account | `stcleansiaweudev` | LRS — blob containers + queues + Functions store |
| 9 | Key Vault | `kv-cleansia-weu-dev` | RBAC mode, secret values owner-populated |
| 10 | App Insights | `appi-cleansia-weu-dev` | workspace-backed |
| 11 | Log Analytics | `log-cleansia-weu-dev` | 30-day retention (dev) |

The deployment also creates: 5 blob containers (`order-photos`, `employee-documents`,
`generated-receipts`, `generated-invoices`, `user-files`), 12 queues (6 base + 6 `-poison`), the
`Cleansia` database, the Postgres firewall rules, and the managed-identity role grants.

---

## 1. Prerequisites (one-time, before anything else)

- [ ] **Azure subscription** with **Owner** or **Contributor + User Access Administrator** on it
      (you need to create role assignments, which Contributor alone can't do).
- [ ] **Azure CLI** installed locally (`az version` ≥ 2.60) with the Bicep extension (`az bicep version`).
- [ ] **GitHub repo admin** rights (to create Environments + secrets).
- [ ] Your **public IP** (for the Postgres admin firewall rule): `curl -s ifconfig.me`.
- [ ] Decide the **Postgres admin password** (strong; you'll store it as a GitHub secret, never in code).

---

## 2. Azure OIDC federation (so CI logs in without a stored password)

The workflows use `azure/login@v2` with federated credentials — no client secret is stored. Create an
app registration (service principal) and federate it to this repo's environments.

```bash
# 2.1 — Create the app registration + service principal
az ad app create --display-name "cleansia-github-oidc"
APP_ID=$(az ad app list --display-name "cleansia-github-oidc" --query "[0].appId" -o tsv)
az ad sp create --id "$APP_ID"
SP_OBJECT_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv)

# 2.2 — Federate it to the dev-weu GitHub Environment (one credential per environment)
az ad app federated-credential create --id "$APP_ID" --parameters '{
  "name": "cleansia-dev-weu",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:VM-s-Solutions/cleansia:environment:dev-weu",
  "audiences": ["api://AzureADTokenExchange"]
}'
# (Repeat with subject ...:environment:prod-weu when you provision prod.)

# 2.3 — Grant the SP rights on the subscription (Contributor to create resources +
#        User Access Administrator so the deployment can create the MI role assignments).
SUB_ID=$(az account show --query id -o tsv)
az role assignment create --assignee "$APP_ID" --role "Contributor" \
  --scope "/subscriptions/$SUB_ID"
az role assignment create --assignee "$APP_ID" --role "User Access Administrator" \
  --scope "/subscriptions/$SUB_ID"
```

Note the three OIDC ids for the GitHub secrets in step 4: `AZURE_CLIENT_ID` = `$APP_ID`,
`AZURE_TENANT_ID` = `az account show --query tenantId -o tsv`, `AZURE_SUBSCRIPTION_ID` = `$SUB_ID`.

---

## 3. Resource group

```bash
az group create --name rg-cleansia-weu-dev --location westeurope
```

---

## 4. GitHub Environments + secrets  *(ticket T-0317)*

In **GitHub → repo → Settings → Environments**:

- [ ] Create **`dev-weu`** — no protection rules (auto-deploys on merge to `master`).
- [ ] Create **`prod-weu`** — **Required reviewers** = you; **Wait timer** optional. (Stays unused this wave.)

Add these **environment secrets** to `dev-weu` (Settings → Environments → dev-weu → Add secret):

| Secret | Value | Used by |
|---|---|---|
| `AZURE_CLIENT_ID` | the OIDC app id (step 2) | `azure/login` |
| `AZURE_TENANT_ID` | tenant id | `azure/login` |
| `AZURE_SUBSCRIPTION_ID` | subscription id | `azure/login` |
| `POSTGRES_ADMIN_PASSWORD` | your chosen Postgres password | the Bicep `@secure()` param |
| `ADMIN_IP_ADDRESS` | your public IP | the Postgres firewall rule |
| `ACR_NAME` | `acrcleansiaweudev` | the Functions `az acr build` step |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER` | (filled after step 6 — the SWA deploy token) | partner SPA deploy |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN` | (filled after step 6) | admin SPA deploy |

> The two SWA tokens don't exist until the Static Web Apps are created (step 6), so add placeholders now
> and fill them after the first provision. Migrate any old flat `*_DEV` secrets into this scope and delete
> the flat copies.

---

## 5. First provision — create the infrastructure  *(ticket T-0318, part 1)*

Run the Bicep once from your machine (this is the only manual `az deployment`; afterwards CI does it).
The Key Vault is created here **empty** — you fill the secret values in step 6.

```bash
az deployment group create \
  --resource-group rg-cleansia-weu-dev \
  --template-file deploy/bicep/main.bicep \
  --parameters deploy/bicep/weu.dev.bicepparam \
  --parameters postgresAdministratorPassword="<your-postgres-password>" \
               adminIpAddress="$(curl -s ifconfig.me)"
```

> **Tip:** run `az deployment group what-if` with the same args first — it shows exactly what will be
> created and changes nothing. (This is also what CI runs on a PR.)

When it finishes, capture the outputs (you'll need the SWA names + the API hostnames):

```bash
az deployment group show -g rg-cleansia-weu-dev -n main --query properties.outputs
```

---

## 6. Populate Key Vault secret values  *(ticket T-0318, part 2)*

The Bicep created `kv-cleansia-weu-dev` with **no values**. The App Services reference these by name and
will not start healthy until they're set. Grant yourself access, then set each value.

```bash
# 6.1 — Give yourself Secrets Officer on the vault (so you can write values)
ME=$(az ad signed-in-user show --query id -o tsv)
KV_ID=$(az keyvault show -n kv-cleansia-weu-dev -g rg-cleansia-weu-dev --query id -o tsv)
az role assignment create --assignee "$ME" --role "Key Vault Secrets Officer" --scope "$KV_ID"

# 6.2 — Build the Postgres connection string from the deployment output
PG_FQDN=$(az postgres flexible-server show -g rg-cleansia-weu-dev -n pg-cleansia-weu-dev \
  --query fullyQualifiedDomainName -o tsv)
DB_CONN="Host=$PG_FQDN;Database=Cleansia;Username=<admin-login>;Password=<your-postgres-password>;Ssl Mode=Require;Trust Server Certificate=true"

# 6.3 — Storage connection string
ST_CONN=$(az storage account show-connection-string -g rg-cleansia-weu-dev \
  -n stcleansiaweudev --query connectionString -o tsv)

# 6.4 — Set every secret the platform reads (names are fixed by the Bicep)
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "ConnectionStrings--cleansia-db" --value "$DB_CONN"
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Storage--ConnectionString"      --value "$ST_CONN"
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Jwt--Key"                         --value "<a-strong-random-256-bit-key>"
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Jwt--Issuer"                      --value "https://api-cleansia-partner-weu-dev.azurewebsites.net"
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Jwt--Audience"                    --value "cleansia"
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Stripe--SecretKey"                --value "sk_test_..."     # TEST key, never live
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Stripe--WebhookSecret"            --value "whsec_..."       # TEST webhook
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "SendGrid--ApiKey"                 --value "SG...."
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Sentry--Dsn"                      --value "https://...@sentry.io/..."
az keyvault secret set --vault-name kv-cleansia-weu-dev --name "Mapbox--GeocodingAccessToken"     --value "pk.eyJ..."       # rotate the exposed token first
```

> The managed-identity role grants (each host → Secrets User, Storage data roles, AcrPull) are created
> **by the Bicep** in step 5 — you don't grant those manually. Only the human Secrets-Officer grant (6.1)
> and the SP grants (step 2) are manual.

After setting values, restart the API hosts so they pick up the Key Vault references:
```bash
for h in partner admin customer partner-mobile customer-mobile; do
  az webapp restart -g rg-cleansia-weu-dev -n "api-cleansia-$h-weu-dev"; done
```

---

## 7. Apply database migrations

The CI pipeline applies committed migrations via the EF bundle on every deploy (the `migrate-database`
job). For the **very first** deploy, the same bundle runs before the app deploys — no manual step needed,
**provided your admin IP is in the Postgres firewall** (it is, from step 5's `adminIpAddress`). If you
ever need to apply manually:

```bash
dotnet ef migrations bundle \
  --project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --startup-project src/Cleansia.Web/Cleansia.Web.Partner.csproj \
  --configuration Release --output ./efbundle --force
./efbundle --connection "$DB_CONN"
```

---

## 8. Build the Functions container + first app deploy

The [`deploy-dev.yml`](../.github/workflows/deploy-dev.yml) workflow is **manual-only** (`workflow_dispatch`)
— it never runs on a PR or automatically on push. Trigger it from **GitHub → Actions → "Deploy to DEV" →
Run workflow**, choosing the **mode**:
- **`deploy`** — provision/update via Bicep → migrate → build+push the Functions image → deploy the 5 APIs
  + SSR (parallel) + the 2 SPAs.
- **`what-if`** — a non-mutating Bicep preview only (no migrate, no deploy) — safe to run any time to see
  what a deploy would change.

The SWA deploy tokens (step 4) must be filled first — get them after step 5:

```bash
az staticwebapp secrets list -g rg-cleansia-weu-dev -n swa-cleansia-partner-weu-dev --query properties.apiKey -o tsv
az staticwebapp secrets list -g rg-cleansia-weu-dev -n swa-cleansia-admin-weu-dev   --query properties.apiKey -o tsv
```
Paste those into `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER` / `_ADMIN` (step 4), then run the workflow.

---

## 9. Smoke test — confirm dev is live  *(ticket T-0320)*

- [ ] All 5 APIs healthy: `https://api-cleansia-{partner,admin,customer,partner-mobile,customer-mobile}-weu-dev.azurewebsites.net` respond.
- [ ] **Both** mobile hosts issue a token on a login POST (the iOS apps depend on these).
- [ ] SSR renders: `https://web-cleansia-customer-weu-dev.azurewebsites.net`.
- [ ] Both SPAs load + reach their API (CORS correct).
- [ ] Storage pipeline: a payment → `generate-receipt` queue → Functions → a PDF lands in `generated-receipts`.
- [ ] Migrations applied (the app starts without a schema error).

When green: the five `api-cleansia-*-weu-dev.azurewebsites.net` hosts are the stable base URLs the
**iOS apps point at** — the whole reason for this wave.

---

## 10. Owner checklist (tick-through)

```
[ ] 1.  Prereqs: subscription Owner, az CLI + bicep, GitHub admin, public IP, Postgres password
[ ] 2.  OIDC app registration + federate to dev-weu + grant Contributor + User Access Administrator
[ ] 3.  az group create rg-cleansia-weu-dev (westeurope)
[ ] 4.  GitHub Environments dev-weu (open) + prod-weu (protected); add the dev-weu secrets
[ ] 5.  First provision: az deployment group create (creates all 11 resources + KV empty)
[ ] 6.  Grant self Secrets Officer; set every Key Vault secret value; restart API hosts
[ ] 7.  (Migrations apply automatically on first CI deploy; manual bundle only if needed)
[ ] 8.  Fill the 2 SWA deploy tokens; merge to master to run the full deploy
[ ] 9.  Smoke: 5 APIs + both mobile tokens + SSR + 2 SPAs + the Functions PDF pipeline
[ ] 10. Green → tell Claude → iOS Phase 0 points at dev
```

---

## Related owner steps (separate from this runbook, do when convenient)

- **Rotate the exposed Mapbox token** before putting it in Key Vault (it was live-exposed earlier).
- **Mobile-spec regen** — needed before the iOS *feature* waves (not Phase 0).
- **Admin client regen** — unblocks T-0295 (employee-page audit drill-in).
- **IMP-3 admin regen** — unblocks T-0279 (pay-config client swap).
