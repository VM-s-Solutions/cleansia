# Pre-iOS cleanup audit findings (Gate-0 disciplined, 2026-06-22)

Consolidated 24 raw findings into 13 ranked deduped entries (3 HIGH, 7 MED, 3 LOW). Cross-lens dedup: GetAllEmployees and GetMyReferrals each appeared in two lenses; the four F1-shape paged feeds merged into 3 canonicalization entries. Verified GetAllEmployees.cs is dead, the frontend resolveErrorKey extractor is duplicated in 8 facades with a canonical home in snackbar.service.ts, and RefreshToken.Command leaks two server-overwritten init fields. The dead-constants claim was refuted and excluded; F6 and F10 items fold into existing tickets. Meta: consistency-violations.md is stale versus check-consistency.mjs for the paged-query offenders.

## Triage (ranked)

### 1. [HIGH/M] Shared error-key resolver re-implemented in 8 feature facades; canonical extractor exists in SnackbarService
- evidence: admin-order-ops.facade.ts:190-207; admin-order-refund.facade.ts:118-135; dispute-detail.facade.ts:166-183; admin-pay-period-ops.facade.ts:118-135; admin-payroll-ops.facade.ts:199; invoice-management.facade.ts; package-form.facade.ts; customer disputes.facade.ts:259-276
- canonical: libs/core/services/.../snackbar.service.ts:117-165; resolver idiom in membership-plan-list.models.ts:143-166, referrals-list.models.ts:56-77
- fix: Add one shared extractApiErrorCode in @cleansia/services; each feature exports a thin resolveXxxErrorKey with its own map; delete the 8 private copies + local ApiErrorResult interfaces.

### 2. [HIGH/S] partner-app duplicates order date/time/money formatters that exist in :core and DIVERGE from customer-app rendering
- evidence: partner-app/.../features/orders/OrderDetailFormat.kt:17/29/41; used in StatusTimeline, PaymentCard, OrderTimerCard, ScopeCard, OrderMetadataRow
- canonical: core/.../format/OrderFormatters.kt:38/53/82, already consumed by customer-app
- fix: Delete partner OrderDetailFormat.kt, point the 6 call sites at cz.cleansia.core.format.*, choose one canonical format, add formatOrderTime to :core if needed.

### 3. [HIGH/M] Push-token cluster duplicated across both Android apps; migration comments disagree on Firebase project
- evidence: customer-app and partner-app .../core/notifications/{PushTokenRepository,PushTokenSessionObserver,DeviceApi,DeviceApiDtos}.kt; partner PushTokenRepository.kt:30 admits the mirror
- canonical: :core (cz.cleansia.core), home for shared cross-app code (DeviceIdProvider, ApiResult, SessionScopedCache, TokenStore)
- fix: Hoist the four files into cz.cleansia.core.notifications behind a DeviceRegistrationClient interface each app binds; parameterize the DataStore name; reconcile the migration constant.

### 4. [MED/S] GetMyReferrals (customer) hand-rolls the paged archetype while the admin twin is canonical; ReferralSpecification/Sort exist
- evidence: Features/Referrals/GetMyReferrals.cs:10/27/36/49; ReferralRepository.cs:32-33; live Customer+Mobile ReferralController.cs:40
- canonical: Features/Referrals/Admin/GetPagedReferrals.cs:18-47
- fix: Convert to DataRangeRequest, add ReferrerUserId to ReferralSpecification.Create, page via GetPagedSort+GetCountAsync+MapToDto; retire GetByReferrerAsync.

### 5. [MED/M] Loyalty activity pair both hand-roll the paged archetype over the same bespoke repo method; no Spec/Sort exists
- evidence: Features/Loyalty/GetLoyaltyActivity.cs:11/30/40/69; Features/Loyalty/Admin/GetUserLoyaltyActivity.cs:16/36/50/79; LoyaltyTransactionRepository.GetForAccountAsync:16-17
- canonical: Features/Services/GetPagedServices.cs:16-41; PageDataMapper.cs:8-15
- fix: One ticket: add LoyaltyTransactionSpecification/Sort over LoyaltyAccountId, convert both, keep display-number enrichment post-materialization.

### 6. [MED/S] GetPromoCodeRedemptions hand-builds PagedData with manual page math + bespoke Skip/Take; no Spec/Sort exists
- evidence: Features/PromoCodes/Admin/GetPromoCodeRedemptions.cs:14/22/31/45; PromoCodeRedemptionRepository.cs:120-121; live AdminPromoCodeController.cs:118
- canonical: Features/PromoCodes/Admin/GetPagedPromoCodes.cs:20-46
- fix: Add PromoCodeRedemptionSpecification/Sort over PromoCodeId, convert to canonical paged shape, keep the empty-id short-circuit as a guard.

### 7. [MED/XS] Dead, parallel paged duplicate GetAllEmployees (bespoke Response instead of PagedData); zero dispatch sites
- evidence: Features/Employees/GetAllEmployees.cs:33-45/68-69/84; grep shows only self-reference
- canonical: Features/Employees/GetPagedEmployees.cs:14-49
- fix: Delete GetAllEmployees.cs; if the ContractStatus filter is wanted, fold into EmployeeFilter/EmployeeSpecification.

### 8. [MED/XS] Dead CQRS feature GetUserByEmail; never Send-ed, no endpoint
- evidence: Features/Users/GetUserByEmail.cs:11-41; refs only in own file, IUserRepository.cs:11 doc-comment, UserReadNoTrackingTests.cs:12 comment
- canonical: N/A dead code; GetUser.cs is the wired read; GetByEmailNoTrackingAsync stays alive via GetCurrentUser.cs:34
- fix: Delete GetUserByEmail.cs; fix the stale doc-comment and test comment; keep GetByEmailNoTrackingAsync.

### 9. [MED/S] RefreshToken.Command exposes server-only RequiredProfile/RequiredAudience on the wire; both overwritten by all 5 controllers
- evidence: Features/Auth/RefreshToken.cs:32; overwritten in Web.Customer:87-92, Mobile.Customer:102, Partner:99-104, Mobile.Partner:107, Admin:47-52; leaks into generated clients
- canonical: Features/Auth/Login.cs:26-33 (per-host params pulled server-side, kept off the wire)
- fix: Shrink to record Command(string Token); pass audience/profile pinning via IHostAudienceProvider or an internal param. Flag manual_step nswag-regen.

### 10. [MED/S] sitewide-push-form keeps HTTP/error/confirm/state in the component (no facade) and uses DestroyRef
- evidence: marketing/.../sitewide-push-form.component.ts:112-155; no facade file; DestroyRef at 6,10,56,125,143
- canonical: Every other form feature uses a facade extending UnsubscribeControlDirective owning the call+state
- fix: Add SitewidePushFormFacade extends UnsubscribeControlDirective owning submit/send/state via takeUntil; component delegates; swap inner http.post for the generated client once nswag regenerates.

### 11. [MED/S] admin-pay-config.service.ts hand-rolls HttpClient URLs + parallel DTO interfaces where a generated client exists
- evidence: pay-config-management/.../admin-pay-config.service.ts:42-96 + :7-38; used by pay-config-management.facade.ts:17 + pay-config-form.facade.ts:46
- canonical: core/admin-services/.../admin-client.ts:9131-9175 (AdminPayConfigClient) + generated command/response DTOs
- fix: Replace with inject(AdminClient).adminPayConfigClient in both facades; delete hand-declared DTOs; drop service from providers. Sequence after pending IMP-3 nswag regen.

### 12. [LOW/XS] GetEmployeeDocuments uses canonical spec path but hand-builds PagedData with manual page math (A5) + public Handler (A2)
- evidence: Features/EmployeeDocuments/GetEmployeeDocuments.cs:23/38-65; check-consistency A5; live AdminEmployeeDocumentController.cs:23
- canonical: Features/Services/GetPagedServices.cs:41; PageDataMapper.cs:8-15
- fix: Hoist spec.SatisfiedBy to one var, make Handler internal, replace lines 57-64 with return documents.MapToDto(total, request).

### 13. [LOW/S] Minor consistency drift cluster: GetPagedInvoices A6, AdminLogin B5, dead/duplicated validator helpers (fold into F6)
- evidence: Features/EmployeePayroll/GetPagedInvoices.cs:58-66; Features/Auth/AdminLogin.cs:60; Common/Validators/UserEmailValidator.cs:76-84 and :37-85
- canonical: Company/GetPagedCompanyInfo.cs:35-40; CancelOrder.cs:87; ValidationExtensions.cs:146-201; F6
- fix: Reorder GetPagedInvoices to Include then AsNoTracking then Select; replace the AdminLogin literal with nameof(command.Email); delete dead GetPropertyName and collapse validator bases onto ValidationExtensions under F6.

## Coverage (checked + clean)
Five lenses, Gate-0-disciplined. Backend archetypes and paged-query consistency (24 features) CLEAN: GetPagedOrders, GetCustomerOrders, GetPagedServices, GetPagedPackages, GetPagedEmployees, GetPagedUsers, GetPagedAdminUsers, GetPagedInvoices (only Include-order LOW), GetPagedPayPeriods, GetPagedPayConfigs, GetPagedEmailTemplates, GetPagedCompanyInfo, GetPagedDisputes canonical A1-A8; prior F1 offenders GetPagedPromoCodes and GetPagedReferrals confirmed canonicalized; bare-list reads (GetMembershipPlans, GetSavedAddresses, GetMyRecurringBookings, GetFiscalFailures, GetAvailableJobsPreview, GetMyServingCleaners) correctly non-paged; aggregation reads correctly non-paged; BusinessResult/Error reuse clean; the one control-flow CommitAsync is already-tracked F8. Frontend (83 facades) CLEAN: UnsubscribeControlDirective on all 83; the 3 firstValueFrom users already on F10; zero cross-app client imports (T-0239); Wave-3 ops facades use canonical takeUntil-catchError-finalize; no ticket numbers in source; track-order.facade.ts raw HttpClient refuted; order-wizard comment noise folded into F10. Mobile and contract CLEAN: RememberMe live on both apps and web; device register/unregister/revoke mobile-only on mobile controllers with session identity and ownership checks; web-vs-mobile cookie split is the correct enrichment pattern; the :core shared foundation correctly factored; Compose-typed vs pure OrderFormatters is a legitimate split; the two AuthRepositories genuinely diverge; device self-service repos LOW. Shared and dead-code CLEAN: Constants.cs, DbConstraintViolation, AddressDefaults/GeoBounds/UserRole, UserMappers vs AdminUserMappers, RepositoryExtensions/ServiceExtensions, IPushDispatcher/PushDispatchResult all live; BusinessErrorMessage InvalidNationalId/InvalidTaxId/InvalidZipCode/InvalidPhoneNumber/FileCountTooFew refuted as dead (i18n in all 5 admin locales). Re-verified: GetAllEmployees.cs zero outside references; resolver duplicated across exactly 8 facades plus snackbar.service.ts; RefreshToken.Command two fields unconditionally overwritten and init-only. Meta-finding for the PM: consistency-violations.md claims the backend sweep complete, but GetMyReferrals, GetLoyaltyActivity, GetUserLoyaltyActivity, GetPromoCodeRedemptions, GetEmployeeDocuments and the GetAllEmployees dead-dup are flagged by check-consistency.mjs yet absent; recommend updating it with these tickets.