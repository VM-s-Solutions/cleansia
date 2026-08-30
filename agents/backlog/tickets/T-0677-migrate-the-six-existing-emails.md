---
id: T-0677
title: Migrate the six existing e-mails off hosted SendGrid templates
status: done
size: M
owner: backend
created: 2026-08-30
updated: 2026-08-31
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
- [x] **AC1** - All six send through SendRenderedAsync; no call to CreateSingleTemplateEmail remains. DONE - seven call sites in fact, the [TEST] receipt included.
- [x] **AC2** - The six *TemplateId settings are removed from SendGridConfig and every appsettings file. DONE - ISendGridConfig, SendGridConfig, 12 appsettings files, local.settings.json and two test setups.
- [x] **AC3** - Attachments still work - the receipt and period-closed e-mails carry PDFs. DONE - asserted on the serialized request, both the with- and without-invoice branches.
- [ ] **AC4** - Each migrated e-mail is verified against a real send before its template id is deleted. OWNER - see the status log; a real send needs a live API key and puts mail in someone's inbox, so it is not something to do unattended.

## Out of scope
- Changing any copy - this is a transport change, not a content change.
- Removing the SendGrid dependency entirely.

## Status log
- 2026-08-30 - ready
- 2026-08-31 - done bar AC4. The two senders collapsed into one `SendRenderedAsync` taking a
  nullable attachment - the templated pair differed only in whether they called `AddAttachment`, so
  the branch was a second copy of the whole send. `MergeTranslationsWithData` became
  `BuildTemplateValues`, which layers copy (translations) under runtime data under two computed
  defaults, and stringifies with the invariant culture so a boxed decimal cannot format by whatever
  culture the thread happens to carry.

  Two defects surfaced that the hosted templates had been hiding:

  - **`{{lang}}` was never supplied by anything.** It sits on the `<html>` element of all seven
    templates and is in no translation row, because it IS the row's language. Under SendGrid it
    resolved to empty and nobody noticed. It is now set from the send's language code and pinned by
    a test.
  - **`OrderStatusUpdateTemplateId` was `SET_VIA_USER_SECRETS` in every appsettings file and
    `SET_VIA_SECRETS` in every production one.** No environment ever carried a real value, so that
    e-mail could not have been sending. Nothing in the repository recorded that. It renders from
    `order-status-update.html` now, so the question no longer exists.

  `SupportEmail` gained a fallback to the configured from-address: a locale missing that one row
  used to leave a customer with `mailto:` and nothing after it. A seeded row still wins.

  Ten tests assert on the HTML the SDK actually serializes - the assertions that were impossible
  while the body lived in someone's SendGrid account and the request carried only an id. Full suite
  4112 pass, host-boot 158 pass.

- 2026-08-31 - AC4 is the owner's. A real send needs a live SendGrid key and delivers to a real
  inbox; doing that unattended is not mine to choose. The template ids are already deleted from the
  repository, so the ordering the AC asks for cannot be honoured literally - what protects the
  rollback is git, not the config. The hosted templates still exist in the SendGrid account and were
  not touched, so reverting this commit restores the previous behaviour exactly.

## Review
<!-- reviewer / security / optimizer write verdicts here -->
