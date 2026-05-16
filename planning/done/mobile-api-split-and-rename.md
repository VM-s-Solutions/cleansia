# API host split + rename — Customer Mobile API + role-clear names

## Current state

5 web hosts (project / folder / port / role):

| Project (csproj) | Folder | Port | Today's role | Should be |
|---|---|---|---|---|
| `Cleansia.Web.Partner.csproj` | `Cleansia.Web/` | 5000 | **Partner web** | rename folder to `Cleansia.Web.Partner/` (project name already correct) |
| `Cleansia.Web.Admin.csproj` | `Cleansia.Web.Admin/` | 5001 | **Admin web** | already correct |
| `Cleansia.Web.Mobile.csproj` | `Cleansia.Web.Mobile/` | 5002 | **Partner mobile** (uses `PartnerLogin.Command`) | rename to `Cleansia.Web.Mobile.Partner/` |
| `Cleansia.Web.Customer.csproj` | `Cleansia.Web.Customer/` | 5003 | **Customer web** (HttpOnly cookies + CSRF) | already correct |
| (new) | (new) | 5004 | **Customer mobile** | new `Cleansia.Web.Mobile.Customer/` |

## The bug that drove this

Customer Android currently points to port 5003 (Customer **web** host). That host strips body tokens per the HttpOnly cookie migration. Mobile clients can't read cookies, see `token=""` in the JSON response, interpret it as "email unconfirmed", land on the verify screen even when the user's email is confirmed. Tapping Resend then 400s with `user.email_confirmed`.

The architecturally clean fix: give the customer Android its own backend host (no cookies, body tokens like the partner Mobile host).

## Plan

### Step 1: Create new Cleansia.Web.Mobile.Customer project

- Copy `Cleansia.Web.Mobile/` as the starting template (closest analog — partner mobile already has the body-token + no-cookie shape we want).
- Folder: `src/Cleansia.Web.Mobile.Customer/`
- Csproj: `Cleansia.Web.Mobile.Customer.csproj`
- Namespace: `Cleansia.Web.Mobile.Customer`
- Replace controller set with the customer-aligned subset:
  - From `Cleansia.Web.Customer` copy: `OrderController`, `ExtraController`, `PackageController`, `ServiceController`, `LoyaltyController`, `MembershipController`, `PromoCodeController`, `ReferralController`, `SavedAddressController`, `DisputeController`, `RecurringBookingController`, `NotificationPreferencesController`, `DeviceController`, `UserController`, `PaymentController`, `CountryController`, `LanguageController`, `GdprController`, `FeatureFlagController`.
  - Auth controller: hand-written following the **Mobile** pattern (no `AuthCookieWriter`, return body tokens via plain `HandleResult<JwtTokenResponse>`).
  - Auth controller uses `Login.Command` (the customer-permissive version), NOT `PartnerLogin.Command`.
- Startup: inherit from `CleansiaStartupBase` like the other hosts. Don't override `UseHostAuthMiddleware` (mobile = no CSRF surface).
- Aspire registration: port 5004.
- Reference list mirrors `Cleansia.Web.Mobile`.

### Step 2: Rename existing `Cleansia.Web.Mobile` → `Cleansia.Web.Mobile.Partner`

- Rename folder `Cleansia.Web.Mobile/` → `Cleansia.Web.Mobile.Partner/`
- Rename csproj + namespaces (`Cleansia.Web.Mobile` → `Cleansia.Web.Mobile.Partner`)
- Update AppHost reference (`Projects.Cleansia_Web_Mobile` → `Projects.Cleansia_Web_Mobile_Partner`) and the Aspire identifier (`mobile-api` → `mobile-partner-api`).
- Update `Cleansia.Api.sln` paths.
- Partner Android (`cleansia_android/app/build.gradle.kts`) keeps the port 5002 default — no change needed unless we also want to move that host's port. (Recommendation: keep ports stable, only rename projects.)

### Step 3: Rename `Cleansia.Web/` folder → `Cleansia.Web.Partner/`

The csproj is already named `Cleansia.Web.Partner.csproj` (file naming was done before). Just align the folder name + the AppHost path:
- Folder: `Cleansia.Web/` → `Cleansia.Web.Partner/`
- Already-correct csproj filename stays.
- Namespace check: confirm `namespace Cleansia.Web.Partner` is used everywhere (or `Cleansia.Web` historical leftover that needs sweep).
- Update `Cleansia.Api.sln` path.
- AppHost reference (`Projects.Cleansia_Web_Partner`) is already correct.

### Step 4: AppHost wiring for the new host

Add to `Cleansia.AppHost/Program.cs`:
```csharp
var customerMobileApi = builder.AddProject<Projects.Cleansia_Web_Mobile_Customer>("customer-mobile-api")
    .WithEndpoint("http", e => { e.Port = 5004; e.IsProxied = false; })
    .WithReference(cleansiaDb)
    .WithReference(queues)
    .WaitFor(cleansiaDb);
```

Also rename `mobile-api` → `partner-mobile-api` for symmetry. Customer-Web stays `customer-api`.

### Step 5: Update customer Android API_BASE_URL

In `src/cleansia_customer_android/app/build.gradle.kts`:
- Default `API_BASE_URL` from `http://10.0.2.2:5003/` → `http://10.0.2.2:5004/`
- Documentation comment updated to reflect new host.

### Step 6: Verification

- `dotnet build src/Cleansia.Api.sln` clean (0 CS errors).
- Aspire AppHost runs all 5 hosts on their respective ports.
- Customer Android: login + register + email verify works.
- Partner Android: still works on 5002.
- Customer web: still works on 5003 with cookies.
- Partner web: still works on 5000.
- Admin web: still works on 5001.

## Order of execution

I'll do this in two phases — **functional fix first** (new Customer Mobile host so login works), **rename second** (cosmetic, can be a separate commit).

**Phase 1 (ships the fix):**
1. Create `Cleansia.Web.Mobile.Customer/` project.
2. Wire it into AppHost on port 5004.
3. Update customer Android default to 5004.
4. Verify login works end-to-end.

**Phase 2 (the rename — pure cleanup):**
5. Rename `Cleansia.Web/` folder → `Cleansia.Web.Partner/`.
6. Rename `Cleansia.Web.Mobile/` → `Cleansia.Web.Mobile.Partner/` (+ csproj + namespace + AppHost ref).
7. Rebuild solution, verify all 5 hosts still come up.

I'll execute Phase 1 in this session and stop at Phase 2 unless you tell me to keep going — the rename is a high-churn touches-everything change that deserves its own focused pass.

## Risks

- **Customer endpoints duplicated** between Customer-Web (5003) and Customer-Mobile (5004). The handlers (MediatR commands) are shared, so the logic stays in one place — controllers are thin shells. Drift risk is low.
- **NSwag**: the customer Android does NOT use NSwag — its DTOs are hand-written. So no Android-side client regen needed. The customer web's existing NSwag client unaffected (still points at 5003).
- **Auth audience claim**: the Mobile.Customer Login should issue tokens with the customer audience (`JwtAudiences.Customer`), same as 5003 does. RefreshToken validator uses `RequiredAudience` to gate which host can refresh which token — so a token issued by 5004 should refresh only on 5004 to avoid cross-host token reuse.
- **Functions worker**: doesn't change. It already references the shared core projects.
