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

> ### ⓘ Naming rule: the name encodes the REGION, not the COUNTRY/market (ADR-0017)
>
> Every resource + GitHub Environment is named `…-<region>-<stage>` (e.g. `…-weu-dev`, env `dev-weu`).
> The token is the **Azure region** (`weu` = West Europe), **NOT** a country/market.
>
> **Multiple markets (CZ, SK, …) running in the same region SHARE one deployment** — one set of
> `*-weu-dev` resources, one Postgres, one `dev-weu` Environment. They are *not* separate deployments and
> must *not* get `cz`/`sk` in any resource or environment name. The difference between markets —
> currency, language, VAT/fiscal rules, payment gateway, tax-id formats — is **application data**
> (`CountryConfiguration` rows + the `tenant_id` JWT claim + the row-scoped `TenantId` filter), never
> infrastructure. This is the deliberate **tenancy = app / region = infra** separation from ADR-0017.
>
> **So:** CZ + SK both in West Europe → correctly share `dev-weu`. ✅ Putting a country in the name would
> conflate "where the servers physically are" with "which market a customer belongs to" — the exact
> mistake the seam prevents.
>
> **When a new name *is* needed:** only if a market must run in a **physically different Azure region**
> (e.g. a future market with a data-residency law requiring its own datacenter). Then you add a *second*
> region — say `dev-neu` (North Europe) — by changing the `region` Bicep param + adding a one-line matrix
> entry + a `dev-neu`/`prod-neu` Environment, and `CountryConfiguration.HomeRegion` routes that market's
> tenant there. The existing `weu` resources are never renamed. (See `agents/architecture/decisions/multi-tenancy-and-region.md`.)

---

## 1. Prerequisites (one-time, before anything else)

- [ ] **Azure subscription** with **Owner** or **Contributor + User Access Administrator** on it
      (you need to create role assignments, which Contributor alone can't do).
- [ ] **Azure CLI** with the Bicep extension. **Azure Cloud Shell** (the `>_` bash in the portal) already
      has both — you can run every `az` command in this runbook from there, no local install needed.
- [ ] **GitHub repo admin** rights (to create Environments + secrets).
- [ ] Your **own machine's public IP** — for the Postgres admin firewall rule (direct `psql`/SQL access).
      Get it from a normal browser ("what is my IP") or `curl ifconfig.me` **on your laptop**.
      ⚠️ **Do NOT use the IP of Azure Cloud Shell** — its container IP changes every session, so the rule
      would immediately go stale. This rule is only for *you* connecting to the DB directly; the app and
      the CI migration use other paths (CI opens a temporary per-run rule for the GitHub runner — see §7).
- [ ] Decide the **Postgres admin password** (strong; you'll store it as a GitHub secret, never in code).
      ⚠️ **Use letters + digits ONLY — no `;` `&` `]` `=` or other special characters.** The password goes
      into an Npgsql connection string where `;` is the key/value delimiter and `=` separates key from
      value, so a special char in the password corrupts parsing (`Couldn't set …;ssl mode`). Generate a
      safe one in Cloud Shell: `LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 32`.

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
- [ ] Create **`prod-weu`** — **Required reviewers** = you; **Wait timer** optional. This protection **is**
      the prod gate: `deploy-pro.yml` has no typed-confirmation step anymore — a prod run pauses for your
      approval before any prod secret is released. Full prod setup: **§11 (PROD)** below.

Add these **environment secrets** to `dev-weu` (Settings → Environments → dev-weu → Add secret):

| Secret | Value | Used by |
|---|---|---|
| `AZURE_CLIENT_ID` | the OIDC app id (step 2) | `azure/login` |
| `AZURE_TENANT_ID` | tenant id | `azure/login` |
| `AZURE_SUBSCRIPTION_ID` | subscription id | `azure/login` |
| `POSTGRES_ADMIN_PASSWORD` | your chosen Postgres password | the Bicep `@secure()` param |
| `ADMIN_IP_ADDRESS` | **your laptop's** public IP (not Cloud Shell's — see §1) | the Postgres admin firewall rule |
| `ACR_NAME` | `acrcleansiaweudev` (see note ↓) | the Functions `az acr build` step |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER` | (filled after step 6 — the SWA deploy token) | partner SPA deploy |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN` | (filled after step 6) | admin SPA deploy |

> **`ACR_NAME` — where it comes from:** the Bicep *creates* the registry and names it itself —
> `acrcleansia<region><env>` (no separators; Azure registry names are alphanumeric-only). For dev that is
> deterministically **`acrcleansiaweudev`** — set the secret to exactly that string (no `.azurecr.io`
> suffix; the workflow appends it). It only needs to exist before the *Functions build* step (which runs
> on a deploy, after the registry is provisioned), so setting it upfront is fine. To read it back instead:
> `az acr list -g rg-cleansia-weu-dev --query "[0].name" -o tsv`.
>
> The two **SWA tokens** don't exist until the Static Web Apps are created (step 6), so add placeholders now
> and fill them after the first provision. Migrate any old flat `*_DEV` secrets into this scope and delete
> the flat copies.

---

## 4b. Where do you run steps 5–7? (Azure Cloud Shell — clone the repo first)

You can run **everything from this point in Azure Cloud Shell** (the `>_` bash icon in the portal). It
already has `az` + bicep authenticated as you — no local install needed. But two of the commands read the
**repo's Bicep files** (`deploy/bicep/...`), so **clone the repo into Cloud Shell once**:

```bash
# In Azure Cloud Shell — clone the repo (into your Cloud Shell home / clouddrive)
git clone https://github.com/VM-s-Solutions/cleansia.git
cd cleansia
# (private repo → use a GitHub Personal Access Token as the password when prompted,
#  or `gh auth login` if the GitHub CLI is available.)
```

**Which commands need the repo vs run anywhere:**

| Step | Command | Needs the repo? | Run in Cloud Shell? |
|---|---|---|---|
| 5 | `az deployment group create --template-file deploy/bicep/...` | **Yes** (Bicep files) | ✅ from inside the cloned `cleansia/` |
| 5 | `az deployment group show ... outputs` | No (pure `az`) | ✅ |
| 6 | All the Key Vault / `az keyvault secret set` / `az webapp restart` | No (pure `az`) | ✅ from anywhere |
| 7 | The **manual** EF-bundle (`dotnet ef ...`) | **Yes** + needs the **.NET 10 SDK** | ⚠️ see §7 (CI does this automatically — you rarely run it by hand) |
| 8 | The deploy itself | n/a — runs in **GitHub Actions**, not your shell | — (trigger from the Actions tab) |

So: `cd cleansia` first, then run step 5; steps 6 are plain `az` you can paste anywhere; step 8 is a
button in GitHub, not a shell command.

---

## 5. First provision — create the infrastructure  *(ticket T-0318, part 1)*

Run the Bicep once (the only manual `az deployment`; afterwards GitHub Actions does it). **Run this from
inside the cloned `cleansia/` directory in Cloud Shell** (§4b) — the `--template-file` path is relative to
the repo root. The Key Vault is created here **empty** — you fill the secret values in step 6.

```bash
# adminIpAddress = YOUR LAPTOP's public IP (get it from a browser "what is my IP", or
# `curl ifconfig.me` ON YOUR LAPTOP). Do NOT use $(curl ifconfig.me) here if you're in
# Cloud Shell — that returns Cloud Shell's ephemeral IP, which goes stale.
az deployment group create \
  --resource-group rg-cleansia-weu-dev \
  --template-file deploy/bicep/main.bicep \
  --parameters deploy/bicep/weu.dev.bicepparam \
  --parameters postgresAdministratorPassword="<your-postgres-password>" \
               adminIpAddress="<your-laptop-public-ip>"
```

> **Tip:** run the same command with `what-if` first — `az deployment group what-if --resource-group … --template-file … --parameters …`
> — it shows exactly what will be created and changes nothing.

When it finishes, capture the outputs (you'll need the SWA names + the API hostnames):

```bash
az deployment group show -g rg-cleansia-weu-dev -n main --query properties.outputs
```

---

## 6. Key Vault secrets — mostly automated now  *(ticket T-0318, part 2)*

You no longer hand-populate all 10 secrets. The deploy automates most of it:

- **4 DERIVED by the Bicep** (`derivedSecrets` module — nothing for you to do): `Storage--ConnectionString`,
  `ConnectionStrings--cleansia-db`, `Jwt--Issuer`, `Jwt--Audience`.
- **EXTERNAL pushed by CI from GitHub-Environment secrets** — set these in the `dev-weu` GitHub
  Environment once and every deploy syncs them into Key Vault:

  | GitHub `dev-weu` secret | → Key Vault secret | Value |
  |---|---|---|
  | `JWT_KEY` | `Jwt--Key` | a strong random 256-bit key |
  | `CSRF_SECRET` | `Csrf--Secret` | a random string (only enforced when Csrf:Enabled=true; safe to set now) |
  | `STRIPE_SECRET_KEY` | `Stripe--SecretKey` | `sk_test_…` (TEST, never live) |
  | `STRIPE_WEBHOOK_SECRET` | `Stripe--WebhookSecret` | `whsec_…` |
  | `STRIPE_PUBLISHABLE_KEY` | `Stripe--PublishableKey` | `pk_test_…` (client-safe, but routed through KV) |
  | `SENDGRID_API_KEY` | `SendGrid--ApiKey` | `SG.…` ← **the one that was blocking email** |
  | `SENDGRID_RESET_PASSWORD_TEMPLATE_ID` | `SendGrid--ResetPasswordTemplateId` | `d-c475f44d635f40569aa8b5171dc63270` |
  | `SENDGRID_ORDER_RECEIPT_TEMPLATE_ID` | `SendGrid--OrderReceiptTemplateId` | `d-2e4f0bcc8af54b3d88c471d7e0cd507a` |
  | `SENDGRID_EMAIL_CONFIRMATION_TEMPLATE_ID` | `SendGrid--EmailConfirmationTemplateId` | `d-eb7daac9cbe94f01beb2ee1bb0ec5c29` |
  | `SENDGRID_PERIOD_CLOSED_TEMPLATE_ID` | `SendGrid--PeriodClosedTemplateId` | `d-75a0f9cfdcc44eabb617de12e28d784d` |
  | `SENDGRID_PERIOD_END_REMINDER_TEMPLATE_ID` | `SendGrid--PeriodEndReminderTemplateId` | `d-d8428c5ffff14355a59d0a35023445da` |
  | `SENDGRID_ORDER_STATUS_UPDATE_TEMPLATE_ID` | `SendGrid--OrderStatusUpdateTemplateId` | your `d-…` id (from your user-secrets) |
  | `SENTRY_DSN` | `Sentry--Dsn` | leave EMPTY for dev (Sentry off); real DSN in prod |
  | `MAPBOX_TOKEN` | `Mapbox--GeocodingAccessToken` | `pk.…` (rotate the exposed one first) |

  (The 5 template-id values above are the ones committed in `appsettings.json`; the 6th
  — OrderStatusUpdate — has no committed value, so paste your real `d-…` id from your user-secrets. If
  any of the 5 differ in your user-secrets, use YOUR values.)

  That's the whole owner Key-Vault step now. After the next deploy, the secrets are populated and the App
  Service Key-Vault references resolve green. (A missing/empty GitHub secret is skipped, not fatal.)
  Non-secret config (SendGrid URLs, Stripe redirect URLs, the Fiscal placeholders) is set
  directly by the Bicep app settings — nothing to do. **`SendGrid:OrderStatusUpdateTemplateId`** — set a
  real `d-…` id via
  a GitHub secret once that template exists.

The manual `az keyvault secret set` block below is now only a **fallback** (e.g. setting a value out of
band before the first CI deploy). Grant yourself access first, then set whatever you need.

```bash
# 6.1 — Give yourself Secrets Officer on the vault (so you can write values)
ME=$(az ad signed-in-user show --query id -o tsv)
KV_ID=$(az keyvault show -n kv-cleansia-weu-dev -g rg-cleansia-weu-dev --query id -o tsv)
az role assignment create --assignee "$ME" --role "Key Vault Secrets Officer" --scope "$KV_ID"

# 6.2 — Build the Postgres connection string from the deployment output
# NOTE: the password must be letters+digits only (see §1) — a ; or = in it breaks Npgsql parsing.
PG_FQDN=$(az postgres flexible-server show -g rg-cleansia-weu-dev -n pg-cleansia-weu-dev \
  --query fullyQualifiedDomainName -o tsv)
DB_CONN="Host=$PG_FQDN;Database=Cleansia;Username=cleansia_admin;Password=<your-postgres-password>;Ssl Mode=Require;Trust Server Certificate=true"

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

> **If an API won't boot with `ForwardedHeaders trust is unset or over-broad (ADR-0003 D3)`:** the app
> fail-closes in non-Development unless told which proxy network to trust for X-Forwarded-For. The Bicep
> now sets `ForwardedHeaders__KnownNetworks = 100.64.0.0/10` (the App Service internal range) +
> `ForwardedHeaders__ForwardLimit = 1` on all 5 API hosts. If a host still fails, confirm those two app
> settings are present (a redeploy applies them). If client-IP resolution later looks wrong (rate-limit
> bucketing off), the KnownNetworks range is the one value to adjust to the live App Service ingress.
>
> **If an API won't boot referencing Sentry / an empty DSN:** an empty `Sentry--Dsn` is fixed in code
> (empty = disabled); leave the dev secret empty. If you see it, redeploy to pick up the fix.
>
> **If App Service Key-Vault references show red ❌ even though the secrets EXIST in the vault** (e.g.
> some hosts green, others red): two causes. (1) **Stale cache** — App Service caches reference
> resolution; secrets added after the app started aren't re-read until a **restart**:
> ```bash
> for h in partner admin customer partner-mobile customer-mobile; do
>   az webapp restart -g rg-cleansia-weu-dev -n "api-cleansia-$h-weu-dev"; done
> ```
> (2) **Missing MI grant** — each host's managed identity needs `Key Vault Secrets User`. Verify per host:
> ```bash
> KV_ID=$(az keyvault show -n kv-cleansia-weu-dev -g rg-cleansia-weu-dev --query id -o tsv)
> for h in partner admin customer partner-mobile customer-mobile; do
>   MI=$(az webapp identity show -g rg-cleansia-weu-dev -n "api-cleansia-$h-weu-dev" --query principalId -o tsv)
>   echo "$h: $(az role assignment list --assignee "$MI" --scope "$KV_ID" --query "length([?roleDefinitionName=='Key Vault Secrets User'])" -o tsv)"
> done   # each should print 1; if 0, re-run the deploy (the Bicep grants it) or grant manually.
> ```
>
> **If migrate fails with `extension "citext" is not allow-listed`:** Azure Postgres blocks
> `CREATE EXTENSION` unless the extension is in the server's `azure.extensions` parameter. The Bicep now
> sets `azure.extensions = CITEXT,PG_TRGM` (postgres module), applied by the `provision` job before
> migrate. If the server was provisioned *before* that Bicep change, set it once on the live server, then
> re-run the deploy:
> ```bash
> az postgres flexible-server parameter set -g rg-cleansia-weu-dev -s pg-cleansia-weu-dev \
>   --name azure.extensions --value CITEXT,PG_TRGM
> ```
>
> **If migrate fails with `Couldn't set …;ssl mode` (or similar):** your Postgres password contains a
> special char (`;` `=` `&` …) that corrupts the connection string. **Reset to an alphanumeric password**
> and update it in THREE places that must match:
> ```bash
> NEW_PW=$(LC_ALL=C tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 32); echo "$NEW_PW"
> az postgres flexible-server update -g rg-cleansia-weu-dev -n pg-cleansia-weu-dev --admin-password "$NEW_PW"
> # then set, all to the new value:
> #  1) GitHub dev-weu secret  POSTGRES_ADMIN_PASSWORD   = $NEW_PW
> #  2) GitHub dev-weu secret  DB_CONNECTION_STRING      = the full Host=…;Password=$NEW_PW;Ssl Mode=Require;… string
> #  3) Key Vault secret       ConnectionStrings--cleansia-db = the same full string
> ```
> Then re-run the deploy.

After setting values, restart the API hosts so they pick up the Key Vault references:
```bash
for h in partner admin customer partner-mobile customer-mobile; do
  az webapp restart -g rg-cleansia-weu-dev -n "api-cleansia-$h-weu-dev"; done
```

---

## 7. Apply database migrations

The CI pipeline applies committed migrations via the EF bundle on every deploy (the `migrate-database`
job), before the app deploys — no manual step needed. The runner's IP is **not** your admin IP and is
**not** an Azure service, so the `migrate-database` job **opens a temporary, per-run Postgres firewall
rule for the runner's own IP, applies the migration, then always removes the rule** (even on failure).
You don't manage that — it's automatic.

**You normally never run migrations by hand** — the CI deploy does it. The command below is only a
fallback, and unlike steps 5–6 it needs the **.NET 10 SDK** (bare Cloud Shell does **not** have it — you'd
`dotnet tool install --global dotnet-ef` first, or run it on your laptop). The connecting machine's IP must
be in the firewall (your `ADMIN_IP_ADDRESS` rule). From inside the cloned `cleansia/` repo:

```bash
dotnet ef migrations bundle \
  --project src/Cleansia.Infra.Database/Cleansia.Infra.Database.csproj \
  --startup-project src/Cleansia.Web.Partner/Cleansia.Web.Partner.csproj \
  --configuration Release --output ./efbundle --force
./efbundle --connection "$DB_CONN"
```

---

## 8. Build the Functions container + first app deploy

### Workflow structure — ONE reusable pipeline, two thin callers

Since the deploy refactor there is a **single** pipeline definition,
[`deploy-azure.yml`](../.github/workflows/deploy-azure.yml) (`on: workflow_call`), and the per-stage
workflows are **thin callers** that own only their trigger, concurrency group and inputs. Dev and prod
run the *identical* job graph — they cannot drift:

```
.github/workflows/
├── deploy-dev.yml ─── trigger: push to master (auto) + manual button (mode deploy|what-if);
│                      resolves mode, then calls ▼ with env=dev, githubEnvironment=dev-weu,
│                      bicepparamFile=deploy/bicep/weu.dev.bicepparam, secrets: inherit
│
├── deploy-azure.yml ─ the REUSABLE pipeline: build-dotnet + build-angular → provision (Bicep,
│                      + Key-Vault secret sync) → migrate-database (temp firewall rule) →
│                      5 APIs + SSR + Functions (ACR) + 2 SPAs. Every stage-touching job runs
│                      in `environment: <githubEnvironment>`, so the caller's GitHub Environment
│                      supplies the secrets AND (for prod) the required-reviewers gate.
│
└── deploy-pro.yml ─── trigger: manual ONLY (mode deploy|what-if, default what-if); calls ▲ with
                       env=prod, githubEnvironment=prod-weu,
                       bicepparamFile=deploy/bicep/weu.prod.bicepparam, secrets: inherit
```

The [`deploy-dev.yml`](../.github/workflows/deploy-dev.yml) trigger is **hybrid** (dev tracks master):
- **Auto** — **every push/merge to `master` auto-deploys to dev** (always a full `deploy`). So after the
  first manual provision below, you normally don't deploy by hand — merging keeps dev current.
- **Manual** — the **Run workflow** button is still there (**GitHub → Actions → "Deploy to DEV" → Run
  workflow**) with a **mode** choice: `deploy` (full provision + migrate + deploy) or `what-if` (a
  non-mutating Bicep preview — no migrate, no deploy — to see what a deploy would change).

It **never** runs inside a PR. (Prod — `deploy-pro.yml` — is manual-only behind the `prod-weu`
required-reviewers gate, defaults to `what-if`, and never auto-deploys — see §11.)

For the **first** deploy specifically, fill the 2 SWA deploy tokens (below) first, then either merge to
master or click Run workflow.

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
[ ] 1.  Prereqs: subscription Owner, GitHub admin, your LAPTOP's public IP, Postgres password
        (use Azure Cloud Shell — it already has az + bicep; no local install needed)
[ ] 2.  OIDC app registration + federate to dev-weu + grant Contributor + User Access Administrator
[ ] 3.  az group create rg-cleansia-weu-dev (westeurope)
[ ] 4.  GitHub Environments dev-weu (open) + prod-weu (protected); add the dev-weu secrets
        (ACR_NAME = acrcleansiaweudev ; ADMIN_IP_ADDRESS = your laptop IP)
[ ] 4b. In Cloud Shell: git clone the repo, cd cleansia  (steps 5 + the manual EF bundle need the files)
[ ] 5.  First provision: az deployment group create (from inside cleansia/) → all 11 resources + KV empty
[ ] 6.  Grant self Secrets Officer; set every Key Vault secret value; restart API hosts  (plain az, anywhere)
[ ] 7.  (Migrations apply automatically on the CI deploy; manual bundle needs the .NET SDK — rarely)
[ ] 8.  Fill the 2 SWA deploy tokens; first deploy = merge to master (auto) OR Actions → Run workflow.
        Thereafter every merge to master auto-deploys dev; the manual button stays for re-runs / what-if.
[ ] 9.  Smoke: 5 APIs + both mobile tokens + SSR + 2 SPAs + the Functions PDF pipeline
[ ] 10. Green → tell Claude → iOS Phase 0 points at dev
```

---

## 11. PROD — provisioning + first deploy (all MANUAL_STEPs)

Prod is the **same pipeline** (`deploy-azure.yml`) pointed at the `*-weu-prod` footprint by
[`deploy-pro.yml`](../.github/workflows/deploy-pro.yml) — dispatch-only, **mode defaults to
`what-if`**, so the lazy path is always the non-mutating preview. Everything below is owner UI/CLI
work the YAML cannot do. Do it **in this order**:

> **MANUAL_STEP P1 — create the `prod-weu` GitHub Environment with Required reviewers.**
> GitHub → repo → **Settings → Environments → New environment → `prod-weu`** → add **Required
> reviewers** = you (+ a second approver if available); **Wait timer** optional; optionally restrict
> *Deployment branches* to `master`. This **replaces** the old typed-"deploy" confirmation gate: a
> `Deploy to PRO` run now pauses at the first prod job until a reviewer approves — **no prod secret is
> released before that approval.**

> **MANUAL_STEP P2 — federate the OIDC app to `prod-weu`** (repeat §2.2 with the prod subject):
>
> ```bash
> az ad app federated-credential create --id "$APP_ID" --parameters '{
>   "name": "cleansia-prod-weu",
>   "issuer": "https://token.actions.githubusercontent.com",
>   "subject": "repo:VM-s-Solutions/cleansia:environment:prod-weu",
>   "audiences": ["api://AzureADTokenExchange"]
> }'
> ```

> **MANUAL_STEP P3 — create the prod resource group:**
>
> ```bash
> az group create --name rg-cleansia-weu-prod --location westeurope
> ```

> **MANUAL_STEP P4 — add the `prod-weu` Environment secrets.** Same **names** as `dev-weu`
> (the reusable workflow reads identical names in both stages — only the Environment differs),
> prod **values**. Never reuse a dev secret value in prod.

| `prod-weu` secret (same name as dev-weu) | Prod value | Used by |
|---|---|---|
| `AZURE_CLIENT_ID` | the OIDC app id (after P2 federation) | `azure/login` |
| `AZURE_TENANT_ID` | tenant id | `azure/login` |
| `AZURE_SUBSCRIPTION_ID` | subscription id | `azure/login` |
| `POSTGRES_ADMIN_PASSWORD` | a **new** strong alphanumeric password (§1 rules; never dev's) | the Bicep `@secure()` param |
| `ADMIN_IP_ADDRESS` | your laptop's public IP (§1 caveats apply) | the Postgres admin firewall rule |
| `CI_PRINCIPAL_ID` | the OIDC SP object id — optional; empty skips the Bicep grant (the deploy self-grants Secrets Officer) | the Bicep `ciPrincipalId` param |
| `DB_CONNECTION_STRING` | full Npgsql string for `pg-cleansia-weu-prod` (build as §6.2, prod names) | `migrate-database` |
| `ACR_NAME` | `acrcleansiaweuprod` (deterministic — same naming rule as §4's note) | the Functions `az acr build` step |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_PARTNER` | `swa-cleansia-partner-weu-prod` deploy token (fill after P5) | partner SPA deploy |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_ADMIN` | `swa-cleansia-admin-weu-prod` deploy token (fill after P5) | admin SPA deploy |
| `JWT_KEY` | a **new** strong random 256-bit key | KV push → `Jwt--Key` |
| `CSRF_SECRET` | a new random string | KV push → `Csrf--Secret` |
| `STRIPE_SECRET_KEY` | `sk_live_…` (**live** key — prod only) | KV push → `Stripe--SecretKey` |
| `STRIPE_WEBHOOK_SECRET` | the **live** `whsec_…` (from the prod Stripe webhook endpoint) | KV push → `Stripe--WebhookSecret` |
| `STRIPE_PUBLISHABLE_KEY` | `pk_live_…` | KV push → `Stripe--PublishableKey` |
| `SENDGRID_API_KEY` | the prod SendGrid key | KV push → `SendGrid--ApiKey` |
| `SENDGRID_RESET_PASSWORD_TEMPLATE_ID` | `d-…` (same template ids as dev unless prod gets its own set) | KV push |
| `SENDGRID_ORDER_RECEIPT_TEMPLATE_ID` | `d-…` | KV push |
| `SENDGRID_EMAIL_CONFIRMATION_TEMPLATE_ID` | `d-…` | KV push |
| `SENDGRID_PERIOD_CLOSED_TEMPLATE_ID` | `d-…` | KV push |
| `SENDGRID_PERIOD_END_REMINDER_TEMPLATE_ID` | `d-…` | KV push |
| `SENDGRID_ORDER_STATUS_UPDATE_TEMPLATE_ID` | `d-…` | KV push |
| `SENTRY_DSN` | the **real** prod DSN (dev leaves this empty; prod is where Sentry is on) | KV push → `Sentry--Dsn` |
| `MAPBOX_TOKEN` | the prod Mapbox token | KV push → `Mapbox--GeocodingAccessToken` |

> The 4 derivable Key Vault secrets (`Storage--ConnectionString`, `ConnectionStrings--cleansia-db`,
> `Jwt--Issuer`, `Jwt--Audience`) are written by the Bicep `derivedSecrets` module on the first
> provision, exactly as in dev (§6) — nothing to set.

> **MANUAL_STEP P5 — first provision.** Preferred: dispatch **Actions → "Deploy to PRO" → Run
> workflow → mode = `what-if`**, review the preview, then re-dispatch with **mode = `deploy`** and
> approve the environment gate — the pipeline provisions `rg-cleansia-weu-prod` from
> [`weu.prod.bicepparam`](bicep/weu.prod.bicepparam) (its `adminIpAddress`/`ciPrincipalId`
> placeholders are overridden by the P4 secrets at deploy time, same as dev). The CLI fallback is
> §5 with the prod names: `-g rg-cleansia-weu-prod … --parameters deploy/bicep/weu.prod.bicepparam`.
> The two SPA deploy jobs fail on this first run (empty SWA tokens) — expected; continue to P6.

> **MANUAL_STEP P6 — fill the two SWA tokens, then redeploy:**
>
> ```bash
> az staticwebapp secrets list -g rg-cleansia-weu-prod -n swa-cleansia-partner-weu-prod --query properties.apiKey -o tsv
> az staticwebapp secrets list -g rg-cleansia-weu-prod -n swa-cleansia-admin-weu-prod   --query properties.apiKey -o tsv
> ```
>
> Paste into the two `AZURE_STATIC_WEB_APPS_API_TOKEN_*` secrets (P4), dispatch
> **Deploy to PRO → mode = `deploy`** again, approve, and smoke-test the §9 checklist against the
> `*-weu-prod` hostnames.

Provision order recap: **P1 environment+reviewers → P2 OIDC federation → P3 resource group →
P4 secrets → P5 what-if, then deploy → P6 SWA tokens + final deploy + smoke test.**

---

## 12. Custom domains under `cleansia.cz` — deployed web cookie auth (T-0400)

> **Why:** the web apps authenticate with HttpOnly **`SameSite=Strict`** cookies, which only flow when
> the frontend and the API share a **registrable domain**. The Azure default hostnames
> (`*.azurewebsites.net`, `*.azurestaticapps.net`) are all Public-Suffix-List-separated sites, so a
> **deployed** web URL can never authenticate on them — by design. Local dev uses the shipped
> `devremote` proxy (`npx nx serve <app> --configuration=devremote`) and needs none of this. This
> section is the enabler for **any deployed web URL**: put the frontends + browser-facing APIs on
> `cleansia.cz` subdomains and the existing cookies just work — no cookie attribute changes
> (the cookies are host-only; no `Domain` attribute exists or is needed).
>
> Everything is **default-off**: the Bicep `customDomains` param defaults to `{}` (zero change); each
> `.bicepparam` carries the recommended set commented out. **The DNS records must exist BEFORE the
> deploy that sets them** — that ordering is the only "two-phase" you own; the template itself
> sequences the App Service three-step (hostname binding → free managed certificate → SNI flip) inside
> one deployment (`modules/appServiceCustomDomain.bicep`).

### The per-env hostname set (mirrors the committed prod config shape)

| `customDomains` key | Site | **dev** hostname | **prod** hostname (already assumed by `appsettings.Production.json` / `environment.prod.ts`) |
|---|---|---|---|
| `ssr` | `web-cleansia-customer-weu-<env>` | `dev.cleansia.cz` | `cleansia.cz` *(apex — A record, see below)* |
| `ssr-www` | same SSR site | — (skip) | `www.cleansia.cz` |
| `swa-partner` | `swa-cleansia-partner-weu-<env>` | `partner.dev.cleansia.cz` | `partner.cleansia.cz` |
| `swa-admin` | `swa-cleansia-admin-weu-<env>` | `admin.dev.cleansia.cz` | `admin.cleansia.cz` |
| `api-partner` | `api-cleansia-partner-weu-<env>` | `api.dev.cleansia.cz` | `api.cleansia.cz` |
| `api-admin` | `api-cleansia-admin-weu-<env>` | `api-admin.dev.cleansia.cz` | `api-admin.cleansia.cz` |
| `api-customer` | `api-cleansia-customer-weu-<env>` | `api-customer.dev.cleansia.cz` | `api-customer.cleansia.cz` |
| `api-partner-mobile` / `api-customer-mobile` | the two mobile hosts | **not needed** | **not needed** (body-token — no cookies, no browser CORS) |

### 12.1 — DNS records (at the `cleansia.cz` DNS provider, BEFORE deploying)

Fetch the App Service **domain verification id** once — it is the same for every app in the
subscription (SWAs don't need it; their CNAME is the validation):

```bash
az webapp show -g rg-cleansia-weu-dev -n web-cleansia-customer-weu-dev \
  --query customDomainVerificationId -o tsv
```

Per hostname:

| Hostname type | Records |
|---|---|
| **Subdomain on an App Service** (`dev.cleansia.cz`, `api*.dev.cleansia.cz`, prod `www`/`api*`) | `CNAME <hostname>` → the site's default hostname (e.g. `dev` → `web-cleansia-customer-weu-dev.azurewebsites.net`) **and** `TXT asuid.<hostname>` → the verification id |
| **Subdomain on a Static Web App** (`partner[.dev]`, `admin[.dev]`) | `CNAME <hostname>` → the SWA default hostname (read it from the deployment outputs `partnerSpaHostName` / `adminSpaHostName`) — the CNAME **is** the validation, nothing else |
| **Apex `cleansia.cz`** (prod SSR only) | `A @` → the SSR's inbound IP (`az webapp show -g rg-cleansia-weu-prod -n web-cleansia-customer-weu-prod --query inboundIpAddress -o tsv`) **and** `TXT asuid` → the verification id. ⚠️ the inbound IP can change if the site is deleted/recreated — re-check after any re-provision |

Managed certificates are free and **auto-renew only while these records stay in place** — never
delete them after cut-over. No wildcards: one hostname = one binding = one cert.

### 12.2 — Deploy (the one-deploy sequence)

1. Wait for DNS propagation (`nslookup dev.cleansia.cz` etc. resolves to Azure).
2. Uncomment/adjust the `customDomains` block in the stage's param file
   ([`weu.dev.bicepparam`](bicep/weu.dev.bicepparam) / [`weu.prod.bicepparam`](bicep/weu.prod.bicepparam))
   — any subset of keys works; start with one host if you want to derisk.
3. Merge to `master` (dev auto-deploys) or dispatch the workflow (`what-if` first shows exactly the
   bindings/certs it would add). Prod: the usual `Deploy to PRO` what-if → deploy → approve flow (§11).
4. **If the provision step fails at a `hostNameBindings`/certificate/customDomains resource:** the DNS
   record is missing or not yet propagated. Fix DNS and simply **re-run the deploy** — the whole
   sequence is idempotent (already-bound hostnames and issued certs are no-ops).
5. Known transient: every re-deploy briefly re-PUTs each App Service binding without SSL before the
   SNI flip restores it — a few seconds of HTTPS blip on the **custom** hostnames per deploy (the
   default hostnames are unaffected). Expected; not a failure.

Setting a frontend key auto-aligns, in the same deploy: the App Service **platform CORS** on the
browser-facing APIs, the **app-level `CorsOrigins__n`** app settings (the deployed hosts otherwise run
`appsettings.Production.json`, whose origins are the prod set), and — when `ssr` is set — the
**SendGrid link base + Stripe success/cancel redirect bases** (`customerWebBaseUrl`).

### 12.3 — Google OAuth authorized origins (IMP-1)

In **Google Cloud Console → APIs & Services → Credentials → the OAuth 2.0 client**, add every new
**frontend** origin to *Authorized JavaScript origins* (e.g. `https://dev.cleansia.cz`,
`https://partner.dev.cleansia.cz`, `https://admin.dev.cleansia.cz`; prod: `https://cleansia.cz`,
`https://www.cleansia.cz`, `https://partner.cleansia.cz`, `https://admin.cleansia.cz`) — otherwise
GSI fails with an "origin not allowed" 403. API hostnames are not needed there.

### 12.4 — What does NOT auto-align (flag to the team, not shell work)

- **Deployed web build configs (T-0400 AC3)** — the SPA/SSR builds deployed to the custom domains must
  call the same-site API origins: `environment.prod.ts` already carries the prod table above;
  a *deployed-dev* web build needs a build config pointing at the `*.dev.cleansia.cz` API origins
  (today's `environment.staging.ts` targets the raw `azurewebsites.net` hosts — correct for the local
  devremote proxy, cross-site if served deployed).
- **Admin auth cross-host** — `environment.prod.ts` (admin) sends auth to `api.cleansia.cz` (the
  partner API), whose committed prod `CorsOrigins` does **not** include `admin.cleansia.cz`; the
  architect must ratify or fix that pairing in T-0400 AC1/AC3 before prod cut-over.
- **Cookies** — nothing to change: host-only (no `Domain` attribute), `HttpOnly`/`Secure`/`Strict`
  untouched; same-site is exactly what the subdomains provide.
- **JWT issuer** (`Jwt--Issuer` derived secret) stays on the partner API default hostname — it is an
  opaque matched string, not a reachable URL; changing it is optional and must be done on both
  issue + validate sides at once.

### 12.5 — Smoke (T-0400 AC4)

- [ ] `https://<frontend custom domain>` loads over TLS (managed cert, no warning).
- [ ] Log in there in a **stock** browser (no third-party-cookie exceptions): the response `Set-Cookie`
      lands, and subsequent API calls send it (DevTools → the API request → Cookies) — 200s, no 401.
- [ ] A password-reset email links to the custom customer domain (proves `customerWebBaseUrl` flipped).

---

## Related owner steps (separate from this runbook, do when convenient)

- **Custom domains under `cleansia.cz` (§12)** — required before **any deployed web URL** can
  authenticate (T-0400); dev is optional (local `devremote` covers dev testing), prod is required.
- **Rotate the exposed Mapbox token** before putting it in Key Vault (it was live-exposed earlier).
- **Mobile-spec regen** — needed before the iOS *feature* waves (not Phase 0).
- **Admin client regen** — unblocks T-0295 (employee-page audit drill-in).
- **IMP-3 admin regen** — unblocks T-0279 (pay-config client swap).
