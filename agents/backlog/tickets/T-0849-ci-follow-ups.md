---
id: T-0849
title: CI follow-ups — move to Ubuntu 26.04 deliberately, let Dependabot update the action pins, settle static-web-apps-deploy's ref and stray input, pin dotnet-ef, delete the dead partner Dockerfile, and bring CLAUDE.md's Nx version current
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0838]
blocks: []
stories: []
adrs: []
layers: [ci, backend, docs]
security_touching: true
---

## Context

These are surprises in the CI ground truth of 2026-10-12 that no ruling covered. The ground truth is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/gt_ci.json`. Its inventory is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/ci-inventory.json`. Lines are as of `76e1638a3`.

1. **Ubuntu 26.04.** T-0838 pins `ubuntu-24.04` ahead of the 2026-10-19 migration. Moving later is a choice to make with evidence. These image differences touch our jobs:
   - Docker goes from 28.0.4 to 29.4.2. That affects Testcontainers.PostgreSql 4.10.0 (`src/Directory.Packages.props:122`);
   - psql goes from 16.15 to 18.6. `deploy-azure.yml:889` uses the image's psql; execute-sql installs its own from apt (`:129-132`, `:187-190`);
   - Android cmdline-tools go from 12.0 to 20.0. `android-ci.yml:53-69` relies on the preinstalled SDK and its accepted licences;
   - jq goes from 1.7 to 1.8.1 (`deploy-azure.yml:342`, `:1354`).
2. **Nothing will update the pins.** `.github/dependabot.yml:26` and `:81` cover npm only.
3. **static-web-apps-deploy's `v1` is both a tag and a branch.**
   - Actions runs the tag (`1a947af`). Deploy-docs check run 97938729832 carries the annotation "Unexpected input(s) 'skip_api_build'", and the valid inputs it lists are exactly the tag's.
   - So `deploy-docs.yml:44` passes an input the action ignores.
   - The branch head (`4d27395`) declares that input, and others besides.
   - The deploy logic itself lives in the image `mcr.microsoft.com/appsvc/staticappsclient:stable`, which no pin freezes. The security review accepts that.
4. **`dotnet-ef` is installed unpinned.**
   - `deploy-azure.yml:179-181` installs it with no `--version`, while EF Core is 10.0.3 (`src/Directory.Packages.props:11`, `:13`, `:20`). There is no `dotnet-tools.json`.
   - It builds the migrations bundle that the deploy runs with the database admin connection (SEC-CI-3, SEC-CI-5).
5. **A dead Dockerfile.**
   - `src/Cleansia.Web.Partner/Dockerfile:15-24` builds `Cleansia.Web/Cleansia.Web.csproj`, and no `src/Cleansia.Web` exists.
   - No workflow, Bicep file or script references this Dockerfile. Only Visual Studio's hooks do: `<DockerDefaultTargetOS>` (`Cleansia.Web.Partner.csproj:8`) and the "Docker" launch profile (`Properties/launchSettings.json:12-13`).
6. **CLAUDE.md's quick reference is mostly current already.**
   - The ground truth quoted "Angular 19, Nx 21", but that line is not in the tree.
   - `CLAUDE.md:100` has said "Angular 20.3, Nx 23.1, NgRx 20.1, PrimeNG 20.4" since `00583a452` (2026-10-09), at `b405e9cc5` and on `origin/master`. The old line survives only in a local checkout that is behind `master`.
   - The one drift left: Nx is 23.2.1 (`src/Cleansia.App/package.json:92`, `:122`).

## Doing (proposed, for Mike)

1. **Ubuntu 26.04.** A branch moves the PR workflows to `ubuntu-26.04`, and they must all pass on its head. The deploy workflows follow, on an approved DEV deploy. Until then, 24.04 stays.
2. **Dependabot.** Add `package-ecosystem: github-actions`, `directory: "/"` to `dependabot.yml`, so it proposes updates to the pins and their version comments. Add an `ignore` rule for gradle/actions major versions (Mike: stay on v5).
3. **The stray input.** Delete `skip_api_build: true` at `deploy-docs.yml:44`. The action ignores the input today, so deleting it changes nothing but the warning. Moving to the branch head would change which inputs the action declares, so that is not proposed.
4. **`dotnet-ef`.** Add `--version 10.0.3` to the install, or use one tool manifest shared with T-0842's CI step.
5. **The Dockerfile.** Delete it and its two Visual Studio hooks.
6. **CLAUDE.md.** Change `Nx 23.1` to `Nx 23.2` at `CLAUDE.md:100`. CLAUDE.md is Mike's file, so this waits for his word.

## NOT doing

- ios-ci's `macos-latest`, gradle/actions v6, and newer action majors.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Before any switch, the PR workflows pass on `ubuntu-26.04` on one exact head, and the deploy workflows pass on an approved DEV deploy. Otherwise 24.04 stays, with the reason recorded here.
- [ ] **AC2** — Dependabot's config validates and covers the pinned actions. It never proposes gradle/actions v6.
- [ ] **AC3** — deploy-docs carries no "Unexpected input" annotation. This needs a deploy-docs dispatch, which is a deployment, so it waits for Mike's approval.
- [ ] **AC4** — `dotnet-ef` installs at the EF Core version.
- [ ] **AC5** — No file refers to a `Cleansia.Web` project or to the deleted Dockerfile, and the backend builds.
- [ ] **AC6** — On Mike's word, CLAUDE.md's quick reference matches `package.json`.

## Work record

- 2026-10-12 — Filed from the surprises in the CI ground truth of 2026-10-12. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
