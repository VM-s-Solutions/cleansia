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
 * REGISTRATION IS NECESSARY BUT NOT SUFFICIENT — NX-6 and NX-7 (T-0546). A project can be registered,
 * tagged and aliased and STILL have a test target that has never compiled a single test, in two ways
 * that both report success:
 *   - NX-6, a `tsconfig` whose `extends` names a file that is not there. Four customer feature libs
 *     shipped with one `../` too many, resolving outside the workspace. With no spec present Jest
 *     prints "No tests found, exiting with code 0" and Nx reports `Successfully ran target test`; the
 *     first person to add a spec gets `TS5083` and reads it as their own mistake. The wrong depth and
 *     the right depth are one character apart and neither is visible in any build output.
 *   - NX-7, a jest config with no `test` target to select it (or a target whose `jestConfig` path does
 *     not resolve). `legal-pages` had a jest-shaped lib and no target, so `run-many -t test --all`
 *     simply did not list it — an absence no run can print.
 *   - NX-8, NEITHER (T-0463). NX-7 uses the jest config as its witness, so a project that has no jest
 *     config AND no `test` target is invisible to it — the guard was blind to exactly the state it
 *     was built for. All three `libs/data-access/*-stores` sat there: registered, tagged, aliased,
 *     holding the NgRx effects for auth/user/catalog, with `lint` as their only target. `nx test
 *     partner-stores` answered "Cannot find configuration for task", `run-many -t test --all` never
 *     listed them, and NX-7 said nothing. The witness for this one has to be the SOURCE, not the
 *     jest config, because a jest config is precisely what these projects lacked.
 * All three are the same failure mode as an unregistered lib, one layer in: the corpus is smaller
 * than it looks and nothing says so.
 *
 * TAGS ARE ASSERTED BY PRESENCE, NOT BY VALUE. A `project.json` with no `tags` puts the lib straight
 * back outside `@nx/enforce-module-boundaries` — half of the original hole — so an empty or missing
 * array fails here. The VALUES need no list of their own and this guard deliberately keeps none:
 * `agents/tools/check-module-boundaries.mjs` catches a mistyped `scope:` through every CONSUMER of
 * the mistyped lib (measured — one typo took that gate from 19 violations to 117), so a hand-kept
 * vocabulary here would only be a second source of truth for the constraint table.
 *
 * Usage:
 *   node agents/tools/check-nx-project-registration.mjs           # strict: any violation -> exit 1
 *   node agents/tools/check-nx-project-registration.mjs --warn    # report, always exit 0
 *   node agents/tools/check-nx-project-registration.mjs --root=DIR  # run against another tree
 *                                                                   #   (used by the self-test)
 */
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
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

const JEST_CONFIG_NAMES = ["jest.config.ts", "jest.config.js", "jest.config.mjs"];

const isTsconfigName = (name) => /^tsconfig(\..+)?\.json$/.test(name);

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

/** NX-8's witness. A project with no TypeScript under it has nothing a `test` target could compile. */
const hasTypeScriptBeneath = (dir) =>
    subtree(dir, (e) => e.isFile() && (e.name.endsWith(".ts") || e.name.endsWith(".tsx")));

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

/**
 * tsconfigs are JSONC. Block comments and line comments are stripped before parsing; the line form is
 * ANCHORED to the start of a line so that the `//` inside `"$schema": "https://…"` survives.
 */
function readTsconfig(file) {
    try {
        const raw = readFileSync(file, "utf8")
            .replace(/\/\*[\s\S]*?\*\//g, "")
            .replace(/^\s*\/\/.*$/gm, "");
        return { value: JSON.parse(raw) };
    } catch (e) {
        return { error: e.message };
    }
}

/** TypeScript accepts `./x`, `./x.json`, and a DIRECTORY holding `tsconfig.json`. All three resolve. */
function tsconfigTargetExists(from, target) {
    const abs = resolve(dirname(from), target);
    if (existsSync(abs) && !isDir(abs)) return true;
    if (existsSync(`${abs}.json`)) return true;
    return existsSync(join(abs, "tsconfig.json"));
}

const jestConfigOf = (dir) => JEST_CONFIG_NAMES.map((n) => join(dir, n)).find(existsSync);

/** Every file under `dir` matching `match`, skipping the same directories the project walk does. */
function walkFiles(dir, match) {
    const out = [];
    const stack = [dir];
    while (stack.length) {
        const d = stack.pop();
        for (const e of children(d)) {
            if (SKIP_DIRS.has(e.name)) continue;
            const p = join(d, e.name);
            if (e.isDirectory()) stack.push(p);
            else if (match(e.name)) out.push(p);
        }
    }
    return out;
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
let tsconfigs = 0;
let jestConfigs = 0;
let testTargets = 0;
let libProjects = [];

if (!isDir(WORKSPACE)) {
    add(WORKSPACE, "P0", "the Nx workspace directory is missing — nothing could be checked");
} else if (!isDir(LIBS)) {
    add(LIBS, "P0", "libs/ is missing — the corpus is EMPTY, which is a failure, not a pass");
} else {
    const { projects, orphans } = walkLibs();
    libProjects = projects;
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
                    "(Presence only; the VALUES are checked by check-module-boundaries.mjs, through " +
                    "every consumer of the lib.)",
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

// ── NX-6 / NX-7 / NX-8: a registered, tagged project whose TEST TARGET still runs nothing ──
//
// None of the three gets a recorded set. All three baselines are zero as of T-0546 and T-0463, and
// each instance is a one-token fix, so there is nothing to ship enforcement behind
// (`agents/process/enforcement.md`). If a future gap genuinely has to land ahead of its cleanup,
// record it the way NX-4/NX-5 do — an exact-match set that goes red in both directions — never a
// suppression flag.
if (isDir(WORKSPACE)) {
    const tsconfigFiles = walkFiles(WORKSPACE, isTsconfigName);
    tsconfigs = tsconfigFiles.length;
    if (tsconfigs === 0) {
        add(
            WORKSPACE,
            "P0",
            "the walk found ZERO tsconfig files — the enumeration is broken or the tree moved; " +
                "this is a HARD FAILURE, never a silent pass",
        );
    }

    // NX-6 — a tsconfig that cannot resolve its own base or one of its references.
    for (const file of tsconfigFiles) {
        const { value, error } = readTsconfig(file);
        if (error) {
            add(file, "NX-6", `tsconfig does not parse as JSON(C) — ${error}`);
            continue;
        }
        const bases = Array.isArray(value?.extends)
            ? value.extends
            : value?.extends
              ? [value.extends]
              : [];
        for (const base of bases) {
            if (typeof base !== "string") {
                add(file, "NX-6", `\`extends\` holds ${typeof base}, not a path string`);
                continue;
            }
            // A bare specifier (`@tsconfig/strictest`) resolves through node_modules — out of scope.
            if (!base.startsWith(".")) continue;
            if (!tsconfigTargetExists(file, base)) {
                add(
                    file,
                    "NX-6",
                    `\`extends\`: '${base}' is not on disk (resolves to ` +
                        `${rel(resolve(dirname(file), base))}). A dangling base is SILENT until a ` +
                        "spec exists: Jest then dies with TS5083 and ZERO tests run, while a lib " +
                        "with no spec prints 'No tests found' and Nx reports success. Count the " +
                        "'../' segments against a sibling lib at the same depth.",
                );
            }
        }
        for (const ref of Array.isArray(value?.references) ? value.references : []) {
            const path = ref?.path;
            if (typeof path !== "string") {
                add(file, "NX-6", "a `references` entry has no `path` string");
                continue;
            }
            if (!tsconfigTargetExists(file, path)) {
                add(
                    file,
                    "NX-6",
                    `\`references\`: '${path}' is not on disk (resolves to ` +
                        `${rel(resolve(dirname(file), path))})`,
                );
            }
        }
    }

    // NX-7 — the jest config and the `test` target must both exist, and the target's paths resolve.
    const testable = [
        ...libProjects.filter((p) => p.registered).map((p) => p.dir),
        ...APP_ROSTER.map((a) => join(APPS, a)).filter((d) => existsSync(join(d, "project.json"))),
    ];
    for (const dir of testable) {
        const file = join(dir, "project.json");
        const { value, error } = readJson(file);
        if (error) continue; // already reported by NX-2
        const jestConfig = jestConfigOf(dir);
        if (jestConfig) jestConfigs++;
        const target = value?.targets?.test;
        if (target) testTargets++;

        // NX-8 — the state NX-7 cannot see, because NX-7's witness is the jest config. Source is the
        // only witness left when a project has neither half of the jest shape.
        if (!target && !jestConfig && hasTypeScriptBeneath(dir)) {
            add(
                file,
                "NX-8",
                "holds TypeScript source but declares NO `test` target and has no jest config — " +
                    "`nx test <project>` answers 'Cannot find configuration for task' and " +
                    "`run-many -t test --all` never lists it, so the project is absent from every " +
                    "test run with no output anywhere saying so. NX-7 cannot see this: its witness " +
                    "is the jest config, which is the half that is missing. Scaffold jest.config.ts " +
                    "+ tsconfig.spec.json + src/test-setup.ts from a sibling and add the target — " +
                    "and land a real spec in the same change, because a correct config with zero " +
                    "specs prints 'No tests found, exiting with code 0' and reads as success.",
            );
        }

        if (jestConfig && !target) {
            add(
                dir,
                "NX-7",
                `has ${relWs(jestConfig)} but its project.json declares NO \`test\` target — ` +
                    "`nx run-many -t test` cannot select the project, so the whole suite is absent " +
                    "from every run and nothing prints its absence",
            );
        }
        if (target && !jestConfig && String(target.executor ?? "").includes("jest")) {
            add(
                file,
                "NX-7",
                `declares a jest \`test\` target but no ${JEST_CONFIG_NAMES.join("/")} is on disk`,
            );
        }
        for (const key of ["jestConfig", "tsConfig"]) {
            const option = target?.options?.[key];
            if (typeof option === "string" && !existsSync(join(WORKSPACE, option))) {
                add(
                    file,
                    "NX-7",
                    `\`test\` target option \`${key}\` -> '${option}' is not on disk (paths here are ` +
                        "workspace-relative, not project-relative)",
                );
            }
        }
    }

    // Anchored on `registered` so the corpus anchors above own the empty cases and report them once.
    if (registered > 0 && jestConfigs === 0) {
        add(
            WORKSPACE,
            "P0",
            `read ${registered} registered lib project(s) and ZERO jest configs — the jest-config ` +
                "probe is stale; this is a HARD FAILURE, never a silent pass",
        );
    }
    if (registered > 0 && testTargets === 0) {
        add(
            WORKSPACE,
            "P0",
            `read ${registered} registered lib project(s) and ZERO \`test\` targets — the target ` +
                "probe is stale, or the whole workspace is outside `nx test`; this is a HARD " +
                "FAILURE, never a silent pass",
        );
    }
}

// ── report ──────────────────────────────────────────────────────────────────
console.log(
    `nx-project-registration: read ${libRoots} lib root(s), ${registered} registered project(s), ` +
        `${aliases} alias(es) into libs/, ${APP_ROSTER.length} rostered app(s), ` +
        `${tsconfigs} tsconfig(s), ${jestConfigs} jest config(s), ${testTargets} test target(s)`,
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
