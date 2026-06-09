export const meta = {
  name: 'wave2-2b2d',
  description: 'Wave-2 Batch 2B (T-0161→T-0164) + 2D (T-0220→T-0221, T-0219, T-0222) — 4 concurrent lanes, TDD + reviewer/security',
  phases: [{ title: 'Build' }, { title: 'Review' }],
}

const T = {
  'T-0161': { sec: true, agent: 'backend',
    dev: 'Implement T-0161 (agents/backlog/tickets/T-0161-aud-01b-refund-service.md) TEST-FIRST per ADR-0006 D1/D2/D3/D7 + ADR-0005 D1.2. ' +
      'Build IRefundService.IssueRefundAsync(RefundRequest, ct) → BusinessResult<RefundResult> as the ONE place money leaves via Stripe: ' +
      'call Stripe (classified per ADR-0005 + keyed), then record the Refund projection (T-0160 — already built+migrated) + the PaymentStatus ' +
      'transition AFTER Stripe confirms (confirm-then-record, D7); do NOT enqueue notifications (caller does). AC2: deterministic ' +
      'RefundKey = refund:{OrderId}:{purpose} (purpose in {cancel, dispute:{DisputeId}, admin:{RefundRequestId}}) passed as Stripe IdempotencyKey ' +
      '(never Guid/timestamp). AC3: clamp Amount to refundable(order)=amountCharged-Σ(succeeded refunds) read from the Refund projection; a ' +
      'concurrent double-issue → one winner, the loser catches PG 23505 on the unique RefundKey index and resolves-to-existing (ack, not 500). ' +
      'AC4: add an idempotency-key param to IStripeClient.RefundCheckoutSessionAsync (it has none today) and pass it to Stripe — the ADR-0005 ' +
      'retry auto-retries this write ONLY because it is now keyed. AC5: the seam is the ONLY Stripe refund call site (CancelOrder\'s inline one ' +
      'is removed by T-0164, not here). The refund WINDOW is NOT enforced in the seam (ADR-0009 D1) — seam enforces ceiling + idempotency only. ' +
      'security_touching (money-out). MANUAL_STEP nswag-regen ONLY if a refund response DTO crosses the wire — flag it, do not run it. ' +
      'Serialize-aware: the IStripeClient refund-method change touches StripeClient.cs (T-0144 already pooled it — build on that). ' +
      'Tests first: TC-7 money-math, TC-KEY-REFUND (same inputs→same key), TC-IDEMP-REFUND (retried same-key→one Stripe refund+one row), ' +
      'TC-RETRY-IDEMP (transient retry same key→no second refund). dotnet build + tests; report.' },
  'T-0164': { sec: true, agent: 'backend', after: 'T-0161',
    dev: 'Implement T-0164 (agents/backlog/tickets/T-0164-aud-01e-migrate-cancel-dispute-onto-seam.md) TEST-FIRST per ADR-0006 D1.1/D2/D7 + ' +
      'ADR-0009 D1. Migrate the two legacy money paths onto the IRefundService seam T-0161 just built. AC1: ResolveDispute.Handler (today records ' +
      'RefundAmount + Resolved but NEVER calls Stripe) now calls IRefundService.IssueRefundAsync (Reason=DisputeResolution, DisputeId set) — do ' +
      'NOT give it a raw IStripeClientFactory. AC2: CancelOrder.Handler\'s inline un-keyed RefundCheckoutSessionAsync (CancelOrder.cs:142) is ' +
      'REMOVED and replaced by IRefundService.IssueRefundAsync (Reason=CustomerCancellation) with the refundAmount order.Cancel(...) computes; the ' +
      'ADR-0002 OrderRefunded notification enqueue (CancelOrder.cs:158-176) STAYS in CancelOrder. AC3: confirm-then-record — the ' +
      'PaymentStatus.Refunded flip moves INTO the seam after Stripe confirms; a Stripe failure leaves PaymentStatus un-flipped and reports ' +
      'RefundInitiated=false (no phantom Refunded). AC4: deterministic key (cancel→refund:{OrderId}:cancel, dispute→refund:{OrderId}:dispute:{DisputeId}) ' +
      '→ retried cancel/dispute = exactly one Stripe refund + one Refund row. AC5: a Source=Chargeback reconciliation row is window-EXEMPT and ' +
      'counts in refundable(order) so an admin cannot double-refund a charged-back order. May CALL T-0163\'s RevokeForPartialRefundAsync for the ' +
      'cancel/dispute loyalty clawback (T-0163 is done). security_touching. No migration. Tests first: TC-IDEMP-REFUND on cancel+dispute, the ' +
      'confirm-then-record failure test (Stripe fails → PaymentStatus un-flipped), the chargeback-window-exempt test. Grep-verify CancelOrder no ' +
      'longer calls Stripe refund directly. dotnet build + tests; report.' },
  'T-0220': { sec: true, agent: 'db',
    dev: 'Implement T-0220 (agents/backlog/tickets/T-0220-fiscal-seq.md) TEST-FIRST per ADR-0004 D-F4.2. Replace the unsafe fiscal sequence ' +
      'allocator GetNextSequenceForYearAsync (OrderReceiptRepository.cs:35-47 — literally COUNT(*)+1, non-atomic + gappy). AC1: a new FiscalCounter ' +
      'table keyed (TenantId, Year[, IssuerScope]) with ATOMIC allocation (UPDATE ... SET Value = Value + 1 RETURNING Value, or SELECT ... FOR ' +
      'UPDATE on the counter row) — N concurrent allocations → N distinct contiguous numbers (concurrency test). AC2: the number is allocated in ' +
      'the SAME transaction that commits the T-0119 phase-1 claim. AC3: IssuerScope is per TSE/cash-register/issuer per regime (document the ' +
      'mapping); do NOT assume year-reset for DE TSE — confirm year semantics per regime. AC4: a reserved-but-never-signed number is a documented ' +
      'gap (void), NEVER re-allocated; a rolled-back/voided claim does not shift the next allocation. AC5: ReceiptService allocates via the new ' +
      'mechanism; the old COUNT(*)+1 is removed; no ReceiptNumber duplicate possible under concurrency. security_touching (regulatory), tenant- ' +
      'scoped per S8. MANUAL_STEP ef-migration (FiscalCounter table) — flag it, do not run dotnet ef. Tests first: FISCAL-SEQ (N-concurrent→N- ' +
      'distinct-contiguous over a real DbContext; voided claim doesn\'t shift; issuer-scoped reset semantics). dotnet build + tests; report.' },
  'T-0221': { sec: true, agent: 'backend', after: 'T-0220',
    dev: 'Implement T-0221 (agents/backlog/tickets/T-0221-fiscal-auth-idemp.md) TEST-FIRST per ADR-0004 D-F4.3 C-C/C-D. Close the registered-but- ' +
      'stamp-not-persisted residual: recovery re-calls RegisterReceiptAsync (ReceiptService.cs:239) with the same ReceiptNumber, and IFiscalService ' +
      '.RegisterReceiptAsync / FiscalReceiptRequest carry NO idempotency key today. AC1: for each BlockingOnline provider (DE TSE, AT RKSV, ES ' +
      'VeriFactu) document whether RegisterReceiptAsync is idempotent on ReceiptNumber — OR add a provider-side key to make it so. AC2: add an ' +
      'idempotency token to FiscalReceiptRequest / RegisterReceiptAsync (natural token = ReceiptNumber) so the contract is explicit, not implicit. ' +
      'AC3: the ADR-0004 go-live gate item (2) is closed per provider. AC4: a redelivery that re-calls RegisterReceiptAsync with the same ' +
      'ReceiptNumber does not double-register (per-provider mock/contract test). This is a contract change to IFiscalService (clients layer). ' +
      'security_touching (regulatory). Builds on T-0220 (same ReceiptService/fiscal surface — serialize after it). No migration. Tests first. ' +
      'dotnet build + tests; report.' },
  'T-0219': { sec: true, agent: 'db',
    dev: 'Implement T-0219 (agents/backlog/tickets/T-0219-anon-catalog-platform-config.md) TEST-FIRST per ADR-0001 Addendum A1 + platform- ' +
      'expandability doctrine. Drop ITenantEntity from the 5 anonymous catalog entities (Service, ServiceCategory, Package, Extra, ServiceCity) ' +
      'making them platform config like Currency/Language/Country — so anonymous [AllowAnonymous] reads no longer collapse to the TenantId==null ' +
      'slice. AC1: each drops : ITenantEntity; fix its EF config indexes — ServiceCategory & Extra: (TenantId,Slug) UNIQUE → (Slug) UNIQUE; ' +
      'Service/Package: NO unique index today, record the explicit decision (likely none); ServiceCity: (CountryId,Name) non-unique, record the ' +
      'decision. AC2: the [AllowAnonymous] overview reads (ServiceController/PackageController/ExtraController.GetOverview, ' +
      'ServiceCityController.GetServiceCities on both customer hosts) return the full active catalog without a JWT. AC3: structural test asserts ' +
      'none of the 5 is ITenantEntity. AC4: the anonymous Order/Quote pricing read (OrderPricingCalculator) resolves the same catalog rows ' +
      '(no half-fix). AC5: Referral/Validate is OUT OF SCOPE — do not touch. NO CleansiaDbContext change (the filter loop auto-excludes once not ' +
      'ITenantEntity); no handler/repo change. security_touching (S3/S8). MANUAL_STEP ef-migration (drops TenantId columns + reindexes the 5) — ' +
      'flag it, do not run dotnet ef. ServiceCategory MUST move with Service (reached transitively via Service.GetOverview). Record the forward- ' +
      'safe/reverse-constrained note in the migration notes. Tests first: structural anti-regression (none of 5 is ITenantEntity) + a real- ' +
      'DbContext anonymous-catalog read with TWO tenants seeded returns the full catalog (no null-slice). dotnet build + tests; report.' },
  'T-0222': { sec: false, agent: 'backend',
    dev: 'Implement T-0222 (agents/backlog/tickets/T-0222-pay-split-rounding.md) TEST-FIRST. SplitPayForMultipleEmployees currently does a pure ' +
      'full-precision decimal divide (totalPay / employeeCount) with NO currency-minor-unit rounding and NO last-share remainder reconciliation, ' +
      'so an uneven split\'s shares can drift a sub-cent epsilon from the total. AC1: round each share to the currency minor unit (2dp for CZK/EUR) ' +
      'and assign the remainder so sum(shares) == the input total EXACTLY (last-share-takes-remainder — decide + document). AC2: property/exhaustive ' +
      'test over a range of totals and employee counts (incl. odd cents and N that don\'t divide evenly): sum(shares)==total to the minor unit, no ' +
      'share negative or off by more than one minor unit from the even share. AC3: update the existing T-0125 characterization test ' +
      '(SplitPayForMultipleEmployees_Uneven_Split_Is_A_Pure_Decimal_Divide_No_Remainder_Reconciliation in PayCalculatorTests.cs) to assert the new ' +
      'exact-sum contract. AC4: no regression to even splits or single-employee; review the CalculateOrderPay consumer that calls the split to ' +
      'confirm it consumes the reconciled shares correctly. CZK-centric (minor-unit precision from the currency/order CurrencyId). No migration, ' +
      'no nswag. Tests first (AC2 property test red→green). dotnet build + tests; report.' },
}

const DEV_SCHEMA = { type: 'object', properties: {
  ticket: { type: 'string' }, summary: { type: 'string' }, filesChanged: { type: 'array', items: { type: 'string' } },
  manualSteps: { type: 'array', items: { type: 'string' } }, buildStatus: { type: 'string' }, testStatus: { type: 'string' }, decisions: { type: 'string' },
}, required: ['ticket', 'buildStatus', 'testStatus'] }
const REVIEW_SCHEMA = { type: 'object', properties: {
  ticket: { type: 'string' }, role: { type: 'string' }, verdict: { type: 'string', enum: ['APPROVED', 'CHANGES_REQUESTED'] }, acsCovered: { type: 'boolean' },
  blockers: { type: 'array', items: { type: 'object', properties: { severity: { type: 'string', enum: ['blocker', 'major', 'minor', 'nit'] }, file: { type: 'string' }, issue: { type: 'string' }, fix: { type: 'string' } }, required: ['severity', 'issue'] } },
}, required: ['ticket', 'verdict'] }

function bnr(id) {
  const t = T[id]
  return agent(
    t.dev + ' Follow conventions.md INCLUDING comment-discipline: NO tracker refs (// T-0xxx, // AC#, BLIND-#, DA-#, ADR sub-section ids like ' +
    'D-F4.2/CH-#) and NO WHAT-comments in source/tests — only genuine non-obvious WHY (stable ADR-NNNN and S1-S10 refs may stay). Reuse real ' +
    'types (patterns-backend.md). Read the ticket + governing ADRs + catalogs first.',
    { label: `dev:${id}`, phase: 'Build', agentType: t.agent, schema: DEV_SCHEMA }
  ).then((dev) => {
    const revs = [() => agent(
      `REVIEWER for ${id}. Read agents/backlog/tickets/${id}-*.md (ACs), the git diff, the governing ADRs, the catalogs. Verify every AC has ` +
      `evidence + a test written test-first; conventions + strong-type reuse; comment-discipline; ef-migration/nswag FLAGGED not run; no scope ` +
      `creep. Dev: ${JSON.stringify(dev)}. APPROVED or CHANGES_REQUESTED with file:line blockers.`,
      { label: `review:${id}`, phase: 'Review', agentType: 'reviewer', schema: REVIEW_SCHEMA })]
    if (t.sec) revs.push(() => agent(
      `SECURITY reviewer for ${id} (security_touching). Audit S1-S10 + S7a/S7b. ` +
      `${id === 'T-0161' || id === 'T-0164' ? 'Focus: the no-double-refund property (deterministic key + 23505 resolve-to-existing + ceiling clamp), ' +
        'confirm-then-record (no phantom Refunded on Stripe failure), and that NO non-seam Stripe refund call survives.' :
        id === 'T-0220' || id === 'T-0221' ? 'Focus: the gapless/monotonic/atomic regulatory guarantee (no duplicate ReceiptNumber under concurrency; ' +
        'no double-registration on redelivery; tenant-scoped per S8).' :
        'Focus: S3/S8 — no TenantId==null leak; anonymous catalog reads return the right rows; the structural anti-regression holds.'} ` +
      `Read the diff. APPROVED or CHANGES_REQUESTED with findings.`,
      { label: `security:${id}`, phase: 'Review', agentType: 'security', schema: REVIEW_SCHEMA }))
    return parallel(revs).then((r) => ({ id, dev, reviews: r.filter(Boolean) }))
  })
}

log('Wave-2 2B+2D: lanes [T-0161→T-0164], [T-0220→T-0221], T-0219, T-0222 — concurrent.')

const lanes = await Promise.all([
  (async () => { const a = await bnr('T-0161'); const b = await bnr('T-0164'); return [a, b] })(),
  (async () => { const a = await bnr('T-0220'); const b = await bnr('T-0221'); return [a, b] })(),
  bnr('T-0219').then((r) => [r]),
  bnr('T-0222').then((r) => [r]),
])

const all = lanes.flat().filter(Boolean)
return { tickets: all.map((r) => ({
  id: r.id, build: r.dev?.buildStatus, tests: r.dev?.testStatus, manualSteps: r.dev?.manualSteps, decisions: r.dev?.decisions,
  reviews: r.reviews.map((v) => ({ role: v.role, verdict: v.verdict, acsCovered: v.acsCovered,
    blockers: (v.blockers || []).filter((b) => b.severity === 'blocker' || b.severity === 'major') })),
})) }
