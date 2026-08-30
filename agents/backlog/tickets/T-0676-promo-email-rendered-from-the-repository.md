---
id: T-0676
title: Promo e-mail rendered from the repository, not a hosted template
status: blocked
size: M
owner: backend
created: 2026-08-30
updated: 2026-08-31
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: false
manual_steps: [nswag-regen]
sprint: 16
---

## Context
- email-templates/*.html was documentation: nothing in the solution read it, and the copy that reached a customer was whatever had been pasted into SendGrid against a template id. The two could drift and nothing caught it.
- The owner asked for a first-order promo code by e-mail, and asked whether the templates could be sent from the codebase instead of depending on SendGrid's hosted templates.

## Acceptance criteria
- [ ] **AC1** - email-templates/promo-code.html exists and matches the set's structure and palette. DONE
- [ ] **AC2** - The folder is embedded in Cleansia.Core.AppServices and is the runtime source; there is one copy of each template in the repository. DONE
- [ ] **AC3** - SendPromoCodeEmailAsync renders locally and sends via MailHelper.CreateSingleEmail, sharing the transport, resilience handler and failure classification with the templated path. DONE
- [ ] **AC4** - Copy comes from EmailTemplateTranslation as it does for the other six; seeded in five locales. DONE
- [x] **AC5** - A command mints a PromoCode and sends it, exposed on the customer web host. DONE. Exposed on Cleansia.Web.Customer only, not the mobile customer host: the caller is the public site's footer box and a signed-in app user is not the audience for a first-order code. Adding the route to a host with no caller would be speculative.
- [ ] **AC6** - The web facade calls the endpoint instead of reporting unavailable. BLOCKED on the owner-run NSwag regeneration.

## Out of scope
- Migrating the six existing e-mails - T-0677.
- Replacing SendGrid as the transport. IEmailService already abstracts it; that is a separate decision.

## Status log
- 2026-08-30 - in_progress
- 2026-08-31 - AC5 done. RequestPromoCode derives the code from the address with SHA-256 over an
  unambiguous alphabet, so a re-request returns the same code instead of minting a row, and the
  send-email queue key is therefore stable — which is what lets the consumer's idempotency claim bound
  delivery at one promo e-mail per address. Exposed anonymously at POST /api/PromoCode/Request behind
  the "auth" rate limiter. Blocked only on AC6's NSwag regeneration.
- 2026-08-31 - blocked. Every AC but AC6 is shipped; AC6 needs the owner to regenerate the customer
  NSwag client before the facade can call the endpoint.
- 2026-08-31 - Policy.CanRequestPromoCode was added and then removed. An anonymous route on these
  hosts carries a bare [AllowAnonymous] and no [Permission]; the constant would have forced the
  ADR-0001 §D1.2 allow-list open from seven to eight and bought nothing, which three tests said
  plainly (AnonymousAllowListExhaustivenessTests x2, PolicyBuilderTests.AnonymousAllowList_Is_The_Frozen_Seven).

## Review
<!-- reviewer / security / optimizer write verdicts here -->
