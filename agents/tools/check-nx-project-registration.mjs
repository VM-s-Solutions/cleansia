#!/usr/bin/env node
/**
 * T-0537 — the Nx project-registration guard.
 *
 * A library that has no `project.json` is not a project, and a non-project is outside **test**,
 * outside **lint** and outside the **module-boundary constraint** simultaneously — with no signal
 * from any of the three. That is how `libs/cleansia-partner-features/dashboard` shipped for months:
 * three guards, one silent hole, zero output. The state is silent BY CONSTRUCTION, so the only fix
 * that holds is a check that enumerates the tree itself and refuses to be quiet.
 *
 * WHY A PLAIN NODE SCRIPT OUTSIDE THE NX WORKSPACE:
 *   - The subject is *projects Nx does not know exist*. Asking Nx to enumerate them is circular, and
 *     a Jest spec cannot cover it: `frontend-ci.yml` runs `nx affected -t test`, and an unregistered
 *     lib belongs to NO project, so `affected` can never select it. Hosting the spec inside some
 *     other project does not help either — `nx.json` declares `@nx/jest:jest` inputs as
 *     `{projectRoot}/**\/*` with an EMPTY `sharedGlobals`, so adding an unregistered directory
 *     anywhere else in the tree does not invalidate that project's cache and Nx replays a CACHED
 *     PASS over the hole.
 *   - `frontend-ci.yml`'s lint step is `continue-on-error: true` (`:73`), so nothing attached to it
 *     can set an exit code at all.
 *   - `agents/tools/check-consistency.mjs` is the counter-example, not the model: it appears in ZERO
 *     workflow files (ADR-0038 CH-P6), so it can never go red.
 * Living outside the workspace removes the cache hazard by construction rather than by
 * configuration. It has its OWN repo-root workflow (`.github/workflows/nx-project-registration.yml`)
 * so it is never behind `nx affected` and never behind a `continue-on-error` step.
 *
 * ANTI-FALSE-GREEN (ADR-0032 D3). Every enumeration is anchored. If `libs/` is gone, if the walk
 * yields ZERO lib roots, if ZERO projects are registered, if `tsconfig.base.json` yields ZERO lib
 * aliases, or if a rostered app has no project — that is a HARD FAILURE, never a silent pass. An
 * empty SCAN is illegal even where an empty RESULT is legal. A green run means the tool read a real
 * corpus and compared real sets; the summary always prints the counts it read.
 *
 * THREE INDEPENDENT WITNESSES, deliberately. `src/index.ts` on disk (the barrel), `project.json` on
 * disk (the registration), and `tsconfig.base.json` (the import path). Breaking one glob still
 * leaves the other two firing, which is what a single-witness grep cannot offer.
 *
 * TAGS ARE ASSERTED BY PRESENCE, NOT BY VALUE. A `project.json` with no `tags` puts the lib straight
 * back outside `@nx/enforce-module-boundaries` — half of the original hole — so an empty or missing
 * array fails here. The tag VOCABULARY (`scope:*` / `type:*`) is T-0534's, which is `in_progress`;
 * this guard deliberately asserts nothing about which tags are legal.
 *
 * Usage:
 *   node agents/tools/check-nx-project-registration.mjs           # strict: any violation -> exit 1
 *   node agents/tools/check-nx-project-registration.mjs --warn    # report, always exit 0
 *   node agents/tools/check-nx-project-registration.mjs --root=DIR  # run against another tree
 *                                                                   #   (used by the self-test)
 */
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const warnOnly = args.includes("--warn");
const rootArg = (args.find((a) => a.startsWith("--root=")) || "").split("=")[1];
const REPO = rootArg
    ? resolve(rootArg)
    : resolve(join(fileURLToPath(import.meta.url), "..", "..", "..")); // agents/tools -> repo root

const WORKSPACE = join(REPO, "src/Cleansia.App");
const LIBS = join(WORKSPACE, "libs");
const APPS = join(WORKSPACE, "apps");
const TSCONFIG = join(WORKSPACE, "tsconfig.base.json");

/**
 * The DOT before `app` is load-bearing: `cleansia-partner-app` fails with "Cannot find project".
 * This roster is the guard's concrete floor — a renamed or moved app directory is a hard failure
 * rather than a smaller corpus that still passes.
 */
const APP_ROSTER = ["cleansia.app", "cleansia-partner.app", "cleansia-admin.app"];

const SKIP_DIRS = new Set([
    "node_modules",
    "dist",
    "coverage",
    "tmp",
    ".nx",
    ".angular",
    ".git",
    ".cache",
]);

const SOURCE_EXT = [".ts", ".tsx", ".html", ".scss", ".css"];

// ─────────────────────────────────────────────────────────────────────────────
// The KNOWN, EXACT-MATCH sets.
//
// Neither is a suppression list. Each records the EXACT state found when this guard was written, so
// it fails the moment reality moves in EITHER direction —
//   - a NEW instance appears        -> not in the set          -> RED
//   - a recorded instance is FIXED  -> the set no longer holds -> RED ("stale entry, delete it")
// so a recorded gap can only be closed deliberately, and its entry must be deleted in the same
// change that closes it. Nothing here is ever printed as "OK": the summary always states the count.
//
// They exist because `agents/process/enforcement.md` forbids making a check blocking while its
// baseline is non-zero — "add enforcement behind the cleanup, never in front of it". BOTH are now
// EMPTY: T-0554 deleted the three dangling aliases and T-0555 deleted the orphan tree, so all five
// rules gate strictly. The machinery stays for the next gap that has to ship behind its own cleanup —
// record it here, then delete the entry in the same change that closes it.
// ─────────────────────────────────────────────────────────────────────────────

/** `tsconfig.base.json` aliases whose target file does not exist. Emptied by T-0554. */
const KNOWN_DANGLING_ALIASES = {};

/** Source trees under `libs/` with no project root anywhere beneath them. Emptied by T-0555. */
const KNOWN_ORPHAN_SOURCE_ROOTS = {};

// ── plumbing ────────────────────────────────────────────────────────────────
const findings = []; // hard: sets the exit code
const known = []; // recorded above: printed, does not set the exit code

const rel = (f) => relative(REPO, f).split(sep).join("/");
const relWs = (f) => relative(WORKSPACE, f).split(sep).join("/");
const add = (file, rule, msg) => findings.push(`${rel(file)}  ${rule}  ${msg}`);

const isDir = (p) => {
    try {
        return statSync(p).isDirectory();
    } catch {
        return false;
    }
};

const children = (dir) => {
    try {
        return readdirSync(dir, { withFileTypes: true });
    } catch {
        return [];
    }
};

const isLibRoot = (dir) => existsSync(join(dir, "src", "index.ts"));
const isRegistered = (dir) => existsSync(join(dir, "project.json"));

function subtree(dir, predicate) {
    const stack = [dir];
    while (stack.length) {
        const d = stack.pop();
        for (const e of children(d)) {
            if (SKIP_DIRS.has(e.name)) continue;
            const p = join(d, e.name);
            if (predicate(e, p)) return true;
            if (e.isDirectory()) stack.push(p);
        }
    }
    return false;
}

const hasProjectRootBeneath = (dir) =>
    subtree(dir, (e, p) => e.isDirectory() && (isLibRoot(p) || isRegistered(p)));

const hasSourceBeneath = (dir) =>
    subtree(dir, (e) => e.isFile() && SOURCE_EXT.some((x) => e.name.endsWith(x)));

/**
 * Walk `libs/` and classify. A project root's own `src/` tree is source, not a place another project
 * root can live, so it is not descended into; everything else is, so a project root nested beside
 * one is still seen and still reported.
 */
function walkLibs() {
    const projects = []; // { dir, libRoot, registered }
    const orphans = []; // directories holding source with no project root anywhere beneath
    const stack = [LIBS];
    while (stack.length) {
        const dir = stack.pop();
        const dirIsLibRoot = isLibRoot(dir);
        for (const e of children(dir)) {
            if (!e.isDirectory() || SKIP_DIRS.has(e.name)) continue;
            if (dirIsLibRoot && e.name === "src") continue;
            const d = join(dir, e.name);
            const libRoot = isLibRoot(d);
            const registered = isRegistered(d);
            if (libRoot || registered) {
                projects.push({ dir: d, libRoot, registered });
                stack.push(d);
                continue;
            }
            if (!hasProjectRootBeneath(d)) {
                if (hasSourceBeneath(d)) orphans.push(d);
                continue;
            }
            stack.push(d);
        }
    }
    return { projects, orphans };
}

function readJson(file) {
    try {
        return { value: JSON.parse(readFileSync(file, "utf8")) };
    } catch (e) {
        return { error: e.message };
    }
}

/** Exact-match reconciliation against a recorded set. Deviation in EITHER direction is a finding. */
function reconcile(observed, recorded, { rule, label, file, staleFile, describe }) {
    for (const [key, value] of observed) {
        if (Object.prototype.hasOwnProperty.call(recorded, key)) {
            known.push(`${rule}  ${describe(key, value)}  — recorded: ${recorded[key]}`);
        } else {
            add(file(key, value), rule, `NEW ${label}: ${describe(key, value)}`);
        }
    }
    const seen = new Set(observed.map(([k]) => k));
    for (const key of Object.keys(recorded)) {
        if (seen.has(key)) continue;
        add(
            staleFile,
            rule,
            `STALE RECORD — '${key}' is no longer ${label}. It was fixed; delete its entry from ` +
                `the recorded set in agents/tools/check-nx-project-registration.mjs in the same change.`,
        );
    }
}

// ── the anchors (ADR-0032 D3): an empty SCAN is illegal ──────────────────────
let libRoots = 0;
let registered = 0;
let aliases = 0;

if (!isDir(WORKSPACE)) {
    add(WORKSPACE, "P0", "the Nx workspace directory is missing — nothing could be checked");
} else if (!isDir(LIBS)) {
    add(LIBS, "P0", "libs/ is missing — the corpus is EMPTY, which is a failure, not a pass");
} else {
    const { projects, orphans } = walkLibs();
    libRoots = projects.filter((p) => p.libRoot).length;
    registered = projects.filter((p) => p.registered).length;

    if (libRoots === 0) {
        add(
            LIBS,
            "P0",
            "the walk found ZERO lib roots (a directory with src/index.ts) — the enumeration is " +
                "broken or the tree moved; this is a HARD FAILURE, never a silent pass",
        );
    }
    if (registered === 0) {
        add(
            LIBS,
            "P0",
            "the walk found ZERO registered projects (a directory with project.json) — the " +
                "enumeration is broken or the tree moved; this is a HARD FAILURE, never a silent pass",
        );
    }

    // NX-1 — the ticket's rule: a lib that exists and is importable but is not a project.
    for (const p of projects) {
        if (p.libRoot && !p.registered) {
            add(
                p.dir,
                "NX-1",
                "lib root has src/index.ts but NO project.json — invisible to Nx: outside test, " +
                    "outside lint AND outside the module-boundary constraint, all silently. Add a " +
                    "project.json with name + tags.",
            );
        }
    }

    // NX-2 — registration without tags puts the lib back outside the module-boundary constraint.
    for (const p of projects) {
        if (!p.registered) continue;
        const file = join(p.dir, "project.json");
        const { value, error } = readJson(file);
        if (error) {
            add(file, "NX-2", `project.json does not parse as JSON — ${error}`);
            continue;
        }
        const tags = value.tags;
        if (!Array.isArray(tags) || tags.filter((t) => typeof t === "string" && t.trim()).length === 0) {
            add(
                file,
                "NX-2",
                "project.json has no non-empty `tags` array — @nx/enforce-module-boundaries cannot " +
                    "constrain an untagged project, so the lib is registered but still unguarded. " +
                    "(Presence only; the tag vocabulary is T-0534's.)",
            );
        }
    }

    // NX-5 — source under libs/ with no project root anywhere beneath it: the same invisibility as
    // NX-1, one step earlier, before a barrel exists to witness it.
    reconcile(
        orphans.map((d) => [relWs(d), d]),
        KNOWN_ORPHAN_SOURCE_ROOTS,
        {
            rule: "NX-5",
            label: "orphan source under libs/",
            file: (_k, d) => d,
            staleFile: LIBS,
            describe: (k) => `${k} holds source but no project root exists anywhere beneath it`,
        },
    );
}

// ── the third witness: tsconfig.base.json ───────────────────────────────────
if (!existsSync(TSCONFIG)) {
    add(TSCONFIG, "P0", "tsconfig.base.json not found — the import-path witness is gone");
} else {
    const { value, error } = readJson(TSCONFIG);
    if (error) {
        add(TSCONFIG, "P0", `tsconfig.base.json does not parse as JSON — ${error}`);
    } else {
        const paths = value?.compilerOptions?.paths;
        if (!paths || typeof paths !== "object") {
            add(TSCONFIG, "P0", "compilerOptions.paths is absent — the alias parser is stale");
        } else {
            const entries = Object.entries(paths).flatMap(([alias, targets]) =>
                (Array.isArray(targets) ? targets : []).map((t) => [alias, t]),
            );
            const intoLibs = entries.filter(([, t]) => t.startsWith("libs/"));
            aliases = intoLibs.length;
            if (aliases === 0) {
                add(
                    TSCONFIG,
                    "P0",
                    "ZERO path aliases resolve into libs/ — the alias parser is stale; this is a " +
                        "HARD FAILURE, never a silent pass",
                );
            }

            // NX-3 — importable but unregistered. This is the original defect seen from the import
            // side: the dashboard lib had `@cleansia-partner/dashboard` and no project.
            for (const [alias, target] of intoLibs) {
                const abs = join(WORKSPACE, target);
                if (!existsSync(abs)) continue; // dangling — NX-4's business, not this rule's
                const cut = target.lastIndexOf("/src/");
                if (cut < 0) {
                    add(
                        TSCONFIG,
                        "NX-3",
                        `alias '${alias}' -> '${target}' has no /src/ segment — the lib root cannot ` +
                            "be derived; the parser is stale, NOT a pass",
                    );
                    continue;
                }
                const libRootDir = join(WORKSPACE, target.slice(0, cut));
                if (!isRegistered(libRootDir)) {
                    add(
                        libRootDir,
                        "NX-3",
                        `alias '${alias}' imports this directory but it has NO project.json — ` +
                            "importable and invisible to Nx at the same time",
                    );
                }
            }

            // NX-4 — an alias naming a library that is not there.
            reconcile(
                entries.filter(([, t]) => !existsSync(join(WORKSPACE, t))),
                KNOWN_DANGLING_ALIASES,
                {
                    rule: "NX-4",
                    label: "dangling alias",
                    file: () => TSCONFIG,
                    staleFile: TSCONFIG,
                    describe: (alias, target) => `alias '${alias}' -> '${target}' does not exist`,
                },
            );
        }
    }
}

// ── the app roster: the concrete floor under the "zero projects" anchor ──────
for (const app of APP_ROSTER) {
    const dir = join(APPS, app);
    if (!isDir(dir)) {
        add(dir, "P0", `rostered app '${app}' is missing — the app corpus moved (note the DOT before 'app')`);
        continue;
    }
    const file = join(dir, "project.json");
    if (!existsSync(file)) {
        add(file, "NX-1", `app '${app}' has NO project.json — the whole app is invisible to Nx`);
        continue;
    }
    const { value, error } = readJson(file);
    if (error) {
        add(file, "NX-2", `project.json does not parse as JSON — ${error}`);
        continue;
    }
    if (value.name !== app) {
        add(
            file,
            "NX-2",
            `project name '${value.name}' does not match its directory '${app}' — every script and ` +
                "workflow addresses the app by directory name",
        );
    }
}

// ── report ──────────────────────────────────────────────────────────────────
console.log(
    `nx-project-registration: read ${libRoots} lib root(s), ${registered} registered project(s), ` +
        `${aliases} alias(es) into libs/, ${APP_ROSTER.length} rostered app(s)`,
);

if (known.length) {
    console.log(`\nKNOWN, exactly recorded (NOT a pass — see the recorded sets in this file):`);
    for (const k of known) console.log(`  ${k}`);
}
if (findings.length) {
    console.log(`\nnx-project-registration violations:`);
    for (const f of findings) console.log(`  ${f}`);
}

console.log(
    `\nnx-project-registration: ${findings.length} violation(s), ${known.length} known` +
        (warnOnly ? " [--warn: exit 0]" : ""),
);
process.exit(!warnOnly && findings.length ? 1 : 0);
