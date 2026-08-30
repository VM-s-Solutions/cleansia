---
id: T-0677
title: Migrate the six existing e-mails off hosted SendGrid templates
status: ready
size: M
owner: backend
created: 2026-08-30
updated: 2026-08-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context
- T-0676 proved the mechanism: all seven templates are embedded and render locally, but only the promo e-mail uses it. The six live e-mails still call MailHelper.CreateSingleTemplateEmail against a template id.
- Until they migrate, the repo HTML and the sent HTML can still drift for receipts, password resets, confirmations, period notices and status updates.

## Acceptance criteria
- [ ] **AC1** - All six send through SendRenderedAsync; no call to CreateSingleTemplateEmail remains.
- [ ] **AC2** - The six *TemplateId settings are removed from SendGridConfig and every appsettings file.
- [ ] **AC3** - Attachments still work - the receipt and period-closed e-mails carry PDFs.
- [ ] **AC4** - Each migrated e-mail is verified against a real send before its template id is deleted.

## Out of scope
- Changing any copy - this is a transport change, not a content change.
- Removing the SendGrid dependency entirely.

## Status log
- 2026-08-30 - ready

## Review
<!-- reviewer / security / optimizer write verdicts here -->
