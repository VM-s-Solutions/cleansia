---
id: T-0802
title: The next Slovak customer terms version calls a package "balík", not "balíček"
status: todo
size: S
owner: —
created: 2026-10-04
updated: 2026-10-04
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-10-04 (remark R-2): Slovak calls a package **balík** (*balíky*, *balíkov*) everywhere;
Czech keeps *balíček*. Every client moved the same day (web `85e13c927`, iOS `98b9d93d5`, Android
`ab85ad770`), and `/product/business-rules#charging-a-package-and-a-service-together` records it. One
Slovak text was left behind on purpose, because **a legal text in force is never edited**: the customer
terms of service.

Ground-truthed 2026-10-04 on `fix/remarks-2026-10-04`. The Slovak terms in force,
`src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/2026-10-03/sk.md`, say *balíček* in
three sentences:

- line 27: *Objednať si môžete služby, **balíčky** a doplnky, …*
- line 35: *Cena vychádza zo zvolených služieb, **balíčkov** a doplnkov, …*
- line 43: *Príplatok sa počíta z ceny všetkých zvolených služieb, **balíčkov** a doplnkov …*

No other Slovak customer or cleaner text in the seed tree says *balíček*. The Czech terms say *balíčky*
too, which is right for Czech and stays.

**Owner decision 2026-10-04 (*"write the recommended"*): change the word in the NEXT Slovak terms version
that is made for a real change of terms, never in a version made for this word alone.** A new terms
version brings the booking tick back for every customer before their next booking
(`/product/business-rules#legal-drafts`), and one word of house style does not justify asking every
customer to accept the terms again.

## Acceptance criteria

- [ ] **AC1** — Given a new customer terms-of-service version is being made for a real change of terms,
      When its Slovak text is written, Then it says *balíky* and *balíkov* at the three sites above (and
      anywhere else the new wording names a package), and *balíček* nowhere.
- [ ] **AC2** — The dated folders that exist, `2026-10-03` included, are not edited: the change exists
      only in the new version's `sk.md`.
- [ ] **AC3** — The Czech text of the same version keeps *balíčky* / *balíčků*.
- [ ] **AC4** — The sentence in `/product/business-rules#charging-a-package-and-a-service-together` that
      names the terms of service as the one Slovak text still saying *balíčky* is removed, and the
      CHANGELOG entry for that terms version says the Slovak text now says *balík*.

## Out of scope

- **Making a terms version for this word.** The owner ruled it out. This ticket waits for a version
  that has its own reason to exist (the lawyer's wording is the likely one,
  `/product/business-rules#legal-drafts`).
- Any other wording change in the Slovak terms. Whatever the real change is belongs to its own work.
- The Czech *balíček*, which is the right Czech word.
- The client strings. They already say *balík*.

## Implementation notes

- A terms version is a new dated folder under
  `src/Cleansia.Infra.Database/Seed/Legal/customer/terms-of-service/any/` with all five languages,
  plus a deploy. Copy the previous `sk.md` forward and change the three words in it, alongside the real
  change.
- `agents/tools/check-legal-drafts.mjs` reads the seed tree; the new folder must pass it as usual.

## Status log

- 2026-10-04 — filed as `todo` from owner decision 3 of wave T (2026-10-04), on `fix/remarks-2026-10-04`.
