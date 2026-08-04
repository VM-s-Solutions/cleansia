---
id: T-0538
title: Four Web SDK hosts still carry the recursive content glob that caused the build-output nesting
status: ready
size: S
owner: backend
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 15
source: `01b21746` — the build-nesting cause was found and fixed **structurally** after three symptom
  cleanups, but only on the affected host. Filed by the PM in the sprint-15 reconciliation.
---

## Context

The build-output nesting had a real cause and it was found by measurement, not reasoning:

`dotnet ef` on macOS creates a directory whose **name literally contains a backslash** — `bin\Debug` —
in the two EF projects. MSBuild's glob walker enumerates that entry, normalizes the backslash to a
slash, and reads the **real** `bin/Debug`. The SDK's `bin/**` prune never fires, because the entry is
not named `bin`. So the Web SDK's default `**/*.json` content glob ingested the project's own output and
copied it one level deeper **every build** — reaching the test projects through `ProjectReference`,
which is why their nested chains contained `Cleansia.Web.Partner`'s files rather than their own.

The measurements, from `01b21746`: 57 content items on the affected host versus 3 on its four siblings;
creating an empty `bin\Debug` took a clean project from 3 → 9; deleting it took 57 → 3. Two candidate
fixes were **ruled out by experiment** — a `Directory.Build.props` exclude (the prune is walk-time) and
an escaped literal pattern (MSBuild normalizes backslashes, so no pattern can name the aliased entry).

The fix was to disable default content items on that host and list the two files explicitly.

**It was applied to one project.** Verified at HEAD, 2026-08-04 — of the five `Microsoft.NET.Sdk.Web`
hosts, exactly one sets `EnableDefaultContentItems`:

| host | state |
|---|---|
| `Cleansia.Web.Partner` | `EnableDefaultContentItems` set — **fixed** |
| `Cleansia.Web.Mobile.Customer` | default glob |
| `Cleansia.Web.Admin` | default glob |
| `Cleansia.Web.Customer` | default glob |
| `Cleansia.Web.Mobile.Partner` | default glob |

**Why this is not "it only affected the EF project, leave it".** The trigger is *any* directory whose
name contains a backslash appearing under a host — and `dotnet ef` is not the only tool that can create
one on macOS. The four remaining hosts are armed; they are simply untriggered. The cost of disarming
them is four csproj edits with a known-good template already in the tree.

## Acceptance criteria

- [ ] **AC1 — all five Web SDK hosts disable default content items and list their content explicitly.**
      Given each of the four remaining `.csproj` files, When they are read, Then each sets
      `EnableDefaultContentItems` to false and enumerates the files it actually needs
      (`appsettings*.json` and whatever else that host ships), mirroring `Cleansia.Web.Partner`.
- [ ] **AC2 — the published output is byte-identical.** Given each host, When it is built before and
      after, Then the output directory has the **same entry count and the same contents**. This is the
      AC that matters: the risk of this change is dropping a file the host needs at runtime, not leaving
      the glob in. **Evidence:** a per-host before/after listing with the diff shown as empty. `01b21746`
      did exactly this for the first host — 155 entries, zero diff.
- [ ] **AC3 — the trigger is re-armed and the depth stays flat.** Given a host, When an empty directory
      named `bin\Debug` (a literal backslash in the name) is created under it and the project is built
      three times, Then the output depth does not grow and the content item count does not move.
      **Evidence: the mutation, run, then removed.** This is the proof the fix is structural rather than
      a cleanup of a symptom — the same proof `01b21746` produced.
- [ ] **AC4 — no stale cleanup scripts or comments are left behind.** Given the three earlier symptom
      cleanups, When this lands, Then anything that exists only to delete nested output is removed, and
      any comment describing the workaround goes with it.
- [ ] **AC5 — the solution builds and the three test suites run.** `dotnet build`, then
      `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`.

## Out of scope

- Changing anything about how `dotnet ef` is invoked. The owner runs migrations
  (`CLAUDE.md`); this ticket does not touch that.
- The two EF projects' own `bin\Debug` directories. They are artifacts on a developer machine, not
  repository state.
- Non-Web-SDK projects. The default `**/*.json` content glob is a Web SDK behaviour.

## Implementation notes

**Archetype:** copy `src/Cleansia.Web.Partner/Cleansia.Web.Partner.csproj` — it is the shipped reference
and it carries the reasoning.

Do the hosts **one at a time** with AC2's before/after per host. Four hosts changed in one sweep with a
single "it builds" at the end would not distinguish "correct" from "three correct and one silently
missing a config file", and the failure mode is a host that starts and then cannot read a setting.

**No-decision note:** the mechanism was decided and proved by experiment in `01b21746`. This is applying
a known fix to four more instances. No panel.

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. Passes DoR: five hosts
  enumerated and their state verified at HEAD, AC observable with mutation evidence, `S`, no
  dependencies, no owner-only steps, archetype named.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
