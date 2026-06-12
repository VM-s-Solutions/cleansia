export const meta = {
  name: 'wave3-3f-fix',
  description: 'Close the T-0194 security FAIL: add the enumerated missing rate-limit attributes (F1-F5 + tail), extend the reflection guard, record the AC6 deviation; security re-gate',
  phases: [
    { title: 'Fix', detail: 'one backend dev applies the enumerated attributes + guard' },
    { title: 'Re-gate', detail: 'security re-verifies S5 closure' },
  ],
}

const SPEC = [
  'T-0194 security FAIL findings — every fix is ATTRIBUTE-ONLY ([EnableRateLimiting("auth")] unless a sibling',
  'precedent uses a different bucket; match the sibling). Do NOT touch CleansiaStartupBase.cs or the policy',
  'definitions. The exact endpoints:',
  '',
  'F1 (HIGH — anonymous credential, Partner host): Cleansia.Web.Partner/Controllers/UserController.cs',
  '  RequestPasswordChange (:52-56) + ChangePassword (:63-67) — both [AllowAnonymous] with ZERO rate limit.',
  '  The Customer + Mobile.Customer siblings carry [EnableRateLimiting("auth")] — mirror them exactly.',
  '  Also CHECK Mobile.Partner UserController for the same gap and fix if present.',
  'F2 (money-out): Cleansia.Web.Admin/Controllers/AdminInvoiceController.cs — ApproveInvoice:40,',
  '  MarkInvoicePaid:52, CancelInvoice:64, RegenerateInvoicePdf:76.',
  'F3 (money params): Cleansia.Web.Admin/Controllers/AdminPayConfigController.cs — create:45, update:59,',
  '  delete:79, bulk-create-for-employee:109.',
  'F4 (mass side-effect): Cleansia.Web.Admin/Controllers/AdminMarketingController.cs SendSitewidePromo:19-25;',
  '  AdminEmailTemplateController.cs SendTestEmailByType:42 + SendTestEmail:110.',
  'F5 (customer money): Cleansia.Web.Customer/Controllers/OrderController.cs ConfirmRecurring:68 (creates a',
  '  Stripe PaymentIntent) + Cancel:166 (refund money-path entry) + SubmitReview if flagged — AND the',
  '  Mobile.Customer OrderController twins of each.',
  'TAIL (also enumerated by security): AdminGdprController.DeleteUserAccount:24; Partner + Mobile.Partner',
  '  OrderController lifecycle writes (TakeOrder/StartOrder/CompleteOrder — CompleteOrder fans out',
  '  receipt/fiscal/loyalty side-effects); Mobile.Partner EmployeeController.UpdateBankDetails:80 (payout',
  '  redirection); Customer-host GdprController GrantConsent:45 + WithdrawConsent:54.',
  '',
  'THEN: extend the T-0194 reflection guard test (MoneyAndSideEffectControllers list) to include EVERY',
  'controller above so the guard would catch each of these if the attribute were removed. Re-run it green.',
  '',
  'AC6 deviation: the runtime 429 flood/harness test was not delivered — record the deviation explicitly in',
  'the T-0194 ticket status log ("structural reflection guard accepted as interim evidence; runtime 429',
  'harness test deferred to the Wave-4 test slice") so the PM/owner accept it on the record.',
  '',
  'RULES: attributes only; match sibling bucket precedents; comment discipline; build src/Cleansia.Api.sln +',
  'run src/Cleansia.Tests (single-threaded) green incl. the extended guard.',
].join('\n')

phase('Fix')
const dev = await agent(
  'You are the BACKEND developer. Close the T-0194 security FAIL by applying the enumerated fixes exactly.\n\n' +
  SPEC + '\n\nReturn: every endpoint annotated (per host), the guard-list additions, the AC6 deviation log ' +
  'entry, build/test results incl. the guard test.',
  { label: 'dev:3f-fix', phase: 'Fix', agentType: 'backend' },
)

phase('Re-gate')
const security = await agent(
  'You are the SECURITY reviewer re-gating T-0194 (S5 closure) after the fix. You previously FAILED it for ' +
  'the enumerated uncovered endpoints (F1 Partner anonymous credential, F2 AdminInvoice money-out, F3 ' +
  'AdminPayConfig, F4 marketing/email blast, F5 customer order money endpoints + Mobile twins, plus ' +
  'AdminGdpr delete, Partner/Mobile.Partner order lifecycle, UpdateBankDetails, customer consent). Verify ' +
  'EVERY one now carries the correct [EnableRateLimiting] (sibling-consistent buckets); grep HttpPost/Put/' +
  'Delete across ALL hosts one more time for any remaining unthrottled money/credential/mass-side-effect ' +
  'endpoint; confirm the reflection guard now enumerates these controllers (remove an attribute mentally — ' +
  'would the guard catch it?); confirm the AC6 deviation is recorded on the ticket. Verdict ' +
  'PASS/PASS-WITH-NOTES/FAIL with file:line.\nDEV REPORT:\n' + dev,
  { label: 'security:3f-regate', phase: 'Re-gate', agentType: 'security' },
)

return { dev, security }
