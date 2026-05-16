# Post-Android follow-ups — open items

Slim active tracker. The full archive of shipped work (Waves 0–5, LOY-001/002/003/005, REC-001/002, MOB-C-001 through 008/011/012, BE-001/003/004/005, WEB-P-002, etc.) lives at [`planning/done/post-android-followups-shipped.md`](../done/post-android-followups-shipped.md).

---

## Owner-gated (code wired, blocked on external setup)

### Google OAuth wiring (MOB-C-009 / WEB-C-004 / MOB-C-009b)
**Blocker:** Google Cloud Console project + OAuth client IDs (web + Android).

**State of the code:**
- Customer mobile: Credential Manager flow wired in `SignInScreen.kt` / `SignUpScreen.kt`, `GoogleSignInController.kt`, `AuthViewModel.signInWithGoogle()`. `GOOGLE_WEB_CLIENT_ID` BuildConfig field reads from `~/.gradle/gradle.properties`.
- Customer web: Google Identity SDK + `#googleBtn` template ref shipped in `login.component.ts` / `register.component.ts`. Reads `environment.googleClientId`.
- Customer mobile forgot-password: fully wired (`requestPasswordChange` + `changePassword` on `AuthApi`/`AuthRepository`/`AuthViewModel`; `ForgotPasswordScreen` UI wired).

**Owner actions when ready:**
1. Provision Cloud Console project + OAuth web client + Android client (SHA-1 fingerprint from debug keystore for dev, release keystore for prod).
2. Drop the web client ID into:
   - `~/.gradle/gradle.properties`: `GOOGLE_WEB_CLIENT_ID=...` (customer Android).
   - `apps/cleansia.app/src/environments/environment*.ts`: `googleClientId: '...'` (customer web — also partner web if you want them sharing the project).
3. Test sign-in on each surface.

### Csrf:Enabled flip
See [`httponly-cookie-auth-migration.md`](httponly-cookie-auth-migration.md) Step 6.

---

## Open engineering work

### WEB-P-001 — Mapbox autocomplete in partner address forms [DONE]

Shipped this session (env files + provider + profile-personal-info wiring). Promoting to closed; mention here only for the audit trail.

### INFRA-001 — Bicep IaC [DEFERRED — see decisions-infra-arch.md]

Trigger: next time provisioning a second env from scratch. 3–5 days when triggered.

### ARCH-001 — Android `:core` shared module [DEFERRED — see decisions-infra-arch.md]

Trigger: next time adding the same primitive to both apps. 2–3 days.

### BE-002 — `HandlePaymentNotification` refactor [DONE]

Subscription branch extracted to `StripeSubscriptionWebhookHandler`, `IsOrderEvent`/`IsSubscriptionEvent` moved to `Constants.StripeEventType`, `ExtractOrderId` helper extracted. File dropped from 453 → 296 LOC. Promoted to closed.

---

## Deferred with documented triggers

These have explicit reopen conditions; no action until triggered:

| Item | Trigger |
|---|---|
| MOB-C-002 (tier mascots) | Design assets land |
| MOB-C-008 / MOB-P-001 (>700 LOC file splits) | Next feature work touching those files |
| MOB-C-010 / MOB-P-002 (snackbar→VM injection, 9 sites) | Incremental during feature work |
| WEB-C-001 (perf regression) | Reopen with concrete Lighthouse data |
| WEB-C-002 (duplication) | When a duplication candidate actually surfaces |
| LOY-004 (Plus discount didn't apply) | Retest with LOY-001/002 chips live in user retest |

---

## Cross-references

- HttpOnly cookie auth → [`httponly-cookie-auth-migration.md`](httponly-cookie-auth-migration.md)
- Frontend cleanup waves → [`frontend-cleanup-plan.md`](frontend-cleanup-plan.md)
- Push notifications Phase B → [`push-notifications-phase-b.md`](push-notifications-phase-b.md)
- Mobile theming + locales → [`mobile-theming-i18n.md`](mobile-theming-i18n.md)
- Infra + Android arch decisions → [`decisions-infra-arch.md`](decisions-infra-arch.md)
