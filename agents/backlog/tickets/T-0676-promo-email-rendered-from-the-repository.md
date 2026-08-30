---
id: T-0676
title: Promo e-mail rendered from the repository, not a hosted template
status: in_progress
size: M
owner: backend
created: 2026-08-30
updated: 2026-08-30
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
- [ ] **AC5** - A command mints a PromoCode and sends it, exposed on the customer hosts. NOT DONE
- [ ] **AC6** - The web facade calls the endpoint instead of reporting unavailable. BLOCKED on the owner-run NSwag regeneration.

## Out of scope
- Migrating the six existing e-mails - T-0677.
- Replacing SendGrid as the transport. IEmailService already abstracts it; that is a separate decision.

## Status log
- 2026-08-30 - in_progress

## Review
<!-- reviewer / security / optimizer write verdicts here -->
