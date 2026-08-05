#!/usr/bin/env node
/**
 * Self-test for the Nx project-registration guard (`check-nx-project-registration.mjs`).
 *
 * T-0537's AC1 states the acceptance test BEHAVIOURALLY: *"temporarily rename one project.json, show
 * the failure, restore"*. This file runs that test — and every other failure mode — on every CI run,
 * so the guard cannot decay into scaffolding the way `check-consistency.mjs` did. Stub the checker to
 * exit 0 and this file goes red.
 *
 * It never touches the working tree. Each scenario builds a throwaway workspace with the same SHAPE
 * as the real one (libs with barrels + project.json, the app roster, a tsconfig alias map) and
 * mutates exactly one thing.
 *
 * The recorded sets ship EMPTY (T-0554 and T-0555 closed the two gaps T-0537 found), so the
 * exact-match ratchet has no live subject of its own. The scenarios that exercise it therefore run a
 * throwaway COPY of the checker with entries injected — `checkerWithRecorded` below. That keeps the
 * ratchet covered in BOTH directions without the shipped tool growing a pass-me-a-suppression flag,
 * which is the one thing a recorded set must never become.
 *
 * The AC3 scenarios are the point of the file: a corpus that has gone EMPTY must be RED. A guard
 * that goes green because it looked at nothing is the failure this ticket exists to prevent. The
 * NX-6/NX-7 scenarios (T-0546) replay the same thing one layer in — a registered, tagged lib whose
 * test target compiles nothing — so the fixture carries real tsconfigs and jest configs.
 *
 * Stub the checker to exit 0 and 36 of the 40 scenarios go red. The 4 survivors are the must-NOT-fire
 * ones (a tag value the guard has no opinion on, a JSONC comment, a bare `extends` specifier, an
 * extensionless `extends`), which a no-op passes by construction.
 *
 *   node agents/tools/check-nx-project-registration.test.mjs
 */
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const TOOL = join(HERE, "check-nx-project-registration.mjs");

const LIBS = [
    ["libs/cleansia-partner-features/dashboard", "cleansia-partner-dashboard", "@cleansia-partner/dashboard", ["scope:partner", "type:feature"]],
    ["libs/cleansia-customer-features/orders", "cleansia-customer-orders", "@cleansia-customer/orders", ["scope:customer", "type:feature"]],
    ["libs/core/partner-services", "partner-services", "@cleansia/partner-services", ["scope:partner", "type:util"]],
    ["libs/shared/components", "components", "@cleansia/components", ["scope:shared", "type:ui"]],
];

const APPS = ["cleansia.app", "cleansia-partner.app", "cleansia-admin.app"];

/** The stand-ins the ratchet scenarios inject; no alias or tree in the real workspace is recorded. */
const GHOST_ALIAS = "@cleansia/ghost";
const GHOST_TARGET = "libs/ghost/src/index.ts";
const SCRATCH_ROOT = "libs/scratch";

const write = (root, p, body) => {
    const abs = join(root, p);
    mkdirSync(dirname(abs), { recursive: true });
    writeFileSync(abs, body);
};

const writeJson = (root, p, value) => write(root, p, `${JSON.stringify(value, null, 2)}\n`);

const readJson = (root, p) => JSON.parse(readFileSync(join(root, p), "utf8"));

const rm = (root, p) => rmSync(join(root, p), { recursive: true, force: true });

const WS = "src/Cleansia.App";

/** `libs/core/partner-services` sits three segments deep, so its base is three `../` up. */
const toBase = (dir) => `${"../".repeat(dir.split("/").length)}tsconfig.base.json`;

function freshRoot() {
    const root = mkdtempSync(join(tmpdir(), "nx-project-registration-"));
    const paths = {};
    for (const [dir, name, alias, tags] of LIBS) {
        write(root, `${WS}/${dir}/src/index.ts`, `export * from './lib/${name}';\n`);
        write(root, `${WS}/${dir}/src/lib/${name}.ts`, `export const ${name.replace(/-/g, "_")} = 1;\n`);
        writeJson(root, `${WS}/${dir}/project.json`, {
            name,
            sourceRoot: `${dir}/src`,
            projectType: "library",
            tags,
            targets: {
                test: {
                    executor: "@nx/jest:jest",
                    options: {
                        jestConfig: `${dir}/jest.config.ts`,
                        tsConfig: `${dir}/tsconfig.spec.json`,
                    },
                },
                lint: { executor: "@nx/eslint:lint" },
            },
        });
        // The fixture carries the real shape — a solution tsconfig referencing a lib and a spec
        // config, plus the jest config the test target names — so NX-6/NX-7 read a real corpus.
        writeJson(root, `${WS}/${dir}/tsconfig.json`, {
            extends: toBase(dir),
            files: [],
            include: [],
            references: [{ path: "./tsconfig.lib.json" }, { path: "./tsconfig.spec.json" }],
        });
        writeJson(root, `${WS}/${dir}/tsconfig.lib.json`, {
            extends: "./tsconfig.json",
            include: ["src/**/*.ts"],
        });
        writeJson(root, `${WS}/${dir}/tsconfig.spec.json`, {
            extends: "./tsconfig.json",
            include: ["src/**/*.spec.ts"],
        });
        write(root, `${WS}/${dir}/jest.config.ts`, `export default { displayName: '${name}' };\n`);
        paths[alias] = [`${dir}/src/index.ts`];
    }
    writeJson(root, `${WS}/tsconfig.base.json`, { compilerOptions: { paths } });

    for (const app of APPS) {
        writeJson(root, `${WS}/apps/${app}/project.json`, {
            name: app,
            projectType: "application",
            tags: ["scope:shared", "type:app"],
        });
    }
    return root;
}

const run = (root, tool) => spawnSync(process.execPath, [tool, `--root=${root}`], { encoding: "utf8" });

const CHECKER_SOURCE = readFileSync(TOOL, "utf8");

/**
 * A copy of the checker with recorded entries injected, so the both-directions ratchet keeps its
 * coverage now that both real sets are empty. The declaration must still be the empty literal — if it
 * is not, the injection point moved and this throws rather than silently testing the shipped tool.
 */
function checkerWithRecorded(root, recorded) {
    let source = CHECKER_SOURCE;
    for (const [name, value] of Object.entries(recorded)) {
        const declaration = `const ${name} = {};`;
        if (!source.includes(declaration)) {
            throw new Error(
                `cannot inject ${name}: '${declaration}' is not in check-nx-project-registration.mjs — ` +
                    "the recorded set was renamed or is no longer empty; fix this self-test.",
            );
        }
        source = source.replace(declaration, `const ${name} = ${JSON.stringify(value)};`);
    }
    const copy = join(root, "checker-with-recorded.mjs");
    writeFileSync(copy, source);
    return copy;
}

let failed = 0;
function scenario(name, { mutate, tool, expectExit, expectText = [], rejectText = [] }) {
    const root = freshRoot();
    try {
        if (mutate) mutate(root);
        const r = run(root, tool ? tool(root) : TOOL);
        const out = `${r.stdout}${r.stderr}`;
        const okExit = r.status === expectExit;
        const okText = expectText.every((t) => out.includes(t));
        const okReject = rejectText.every((t) => !out.includes(t));
        if (okExit && okText && okReject) {
            console.log(`  PASS  ${name}`);
        } else {
            failed++;
            console.log(`  FAIL  ${name}`);
            console.log(`        expected exit ${expectExit}, got ${r.status}`);
            if (!okText) console.log(`        expected output to contain: ${expectText.join(" | ")}`);
            if (!okReject) console.log(`        expected output NOT to contain: ${rejectText.join(" | ")}`);
            console.log(out.split("\n").map((l) => `        > ${l}`).join("\n"));
        }
    } catch (e) {
        failed++;
        console.log(`  FAIL  ${name} — ${e.message}`);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

console.log("check-nx-project-registration self-test (T-0537 AC1/AC3/AC4):");

scenario("an unmutated workspace is clean and states what it read", {
    expectExit: 0,
    // The count is what the tool READ, not what it approved — and with both recorded sets empty a
    // clean fixture must report ZERO known, not a quiet pass over something it tolerated.
    expectText: [
        "read 4 lib root(s), 4 registered project(s), 4 alias(es) into libs/, 3 rostered app(s), " +
            "13 tsconfig(s), 4 jest config(s)",
        "0 violation(s), 0 known",
    ],
});

// ── AC1 — the ticket's own acceptance test ──────────────────────────────────
scenario("AC1: a lib root loses its project.json -> RED, and the directory is NAMED", {
    mutate: (r) => rm(r, `${WS}/libs/cleansia-partner-features/dashboard/project.json`),
    expectExit: 1,
    expectText: ["NX-1", "libs/cleansia-partner-features/dashboard", "src/index.ts but NO project.json"],
});

scenario("AC1: every unregistered lib is named, not just the first", {
    mutate: (r) => {
        rm(r, `${WS}/libs/cleansia-partner-features/dashboard/project.json`);
        rm(r, `${WS}/libs/shared/components/project.json`);
    },
    expectExit: 1,
    // Two libs × two witnesses (the barrel and the alias) = four namings, not one summary line.
    expectText: [
        "libs/cleansia-partner-features/dashboard",
        "libs/shared/components",
        "4 violation(s)",
    ],
});

// ── AC4 — registration without tags is half of the original hole ────────────
scenario("AC4: a project.json with NO tags key -> RED", {
    mutate: (r) => {
        const p = `${WS}/libs/core/partner-services/project.json`;
        const j = readJson(r, p);
        delete j.tags;
        writeJson(r, p, j);
    },
    expectExit: 1,
    expectText: ["NX-2", "libs/core/partner-services/project.json", "no non-empty `tags` array"],
});

scenario("AC4: an EMPTY tags array -> RED (presence means non-empty)", {
    mutate: (r) => {
        const p = `${WS}/libs/core/partner-services/project.json`;
        writeJson(r, p, { ...readJson(r, p), tags: [] });
    },
    expectExit: 1,
    expectText: ["NX-2", "no non-empty `tags` array"],
});

scenario("AC4: a tags array of blank strings -> RED", {
    mutate: (r) => {
        const p = `${WS}/libs/core/partner-services/project.json`;
        writeJson(r, p, { ...readJson(r, p), tags: ["", "  "] });
    },
    expectExit: 1,
    expectText: ["NX-2", "no non-empty `tags` array"],
});

scenario("AC4: an unrecognised tag VALUE is accepted — vocabulary is T-0534's, not this guard's", {
    mutate: (r) => {
        const p = `${WS}/libs/core/partner-services/project.json`;
        writeJson(r, p, { ...readJson(r, p), tags: ["something:else"] });
    },
    expectExit: 0,
    rejectText: ["NX-2"],
});

scenario("a project.json that does not parse -> RED, never skipped", {
    mutate: (r) => write(r, `${WS}/libs/shared/components/project.json`, "{ not json"),
    expectExit: 1,
    expectText: ["NX-2", "does not parse as JSON"],
});

// ── AC3 — an EMPTY SCAN is illegal (ADR-0032 D3) ────────────────────────────
scenario("AC3: libs/ is gone -> RED with an explicit empty-corpus message, NOT a pass", {
    mutate: (r) => rm(r, `${WS}/libs`),
    expectExit: 1,
    expectText: ["P0", "libs/ is missing", "the corpus is EMPTY, which is a failure, not a pass"],
});

scenario("AC3: libs/ exists but holds no lib roots -> RED (ZERO lib roots)", {
    mutate: (r) => {
        rm(r, `${WS}/libs`);
        mkdirSync(join(r, `${WS}/libs`), { recursive: true });
    },
    expectExit: 1,
    expectText: ["P0", "ZERO lib roots", "HARD FAILURE, never a silent pass"],
});

scenario("AC3: every project.json disappears -> RED (ZERO registered projects)", {
    mutate: (r) => {
        for (const [dir] of LIBS) rm(r, `${WS}/${dir}/project.json`);
    },
    expectExit: 1,
    expectText: ["P0", "ZERO registered projects"],
});

scenario("AC3: the whole workspace directory is gone -> RED, not an empty pass", {
    mutate: (r) => rm(r, WS),
    expectExit: 1,
    expectText: ["P0", "the Nx workspace directory is missing"],
});

scenario("AC3: tsconfig.base.json is gone -> RED (the import-path witness)", {
    mutate: (r) => rm(r, `${WS}/tsconfig.base.json`),
    expectExit: 1,
    expectText: ["P0", "tsconfig.base.json not found"],
});

scenario("AC3: ZERO aliases resolve into libs/ -> RED (the alias parser is stale)", {
    mutate: (r) => writeJson(r, `${WS}/tsconfig.base.json`, { compilerOptions: { paths: {} } }),
    expectExit: 1,
    expectText: ["P0", "ZERO path aliases resolve into libs/"],
});

scenario("AC3: compilerOptions.paths removed entirely -> RED", {
    mutate: (r) => writeJson(r, `${WS}/tsconfig.base.json`, { compilerOptions: {} }),
    expectExit: 1,
    expectText: ["P0", "compilerOptions.paths is absent"],
});

// ── the alias witness is INDEPENDENT of the barrel witness ──────────────────
scenario("an aliased directory with no barrel and no project.json -> RED via NX-3 alone", {
    mutate: (r) => {
        const dir = `${WS}/libs/shared/components`;
        rm(r, `${dir}/project.json`);
        rm(r, `${dir}/src/index.ts`);
        write(r, `${dir}/src/public-api.ts`, "export const x = 1;\n");
        const p = `${WS}/tsconfig.base.json`;
        const j = readJson(r, p);
        j.compilerOptions.paths["@cleansia/components"] = ["libs/shared/components/src/public-api.ts"];
        writeJson(r, p, j);
    },
    expectExit: 1,
    expectText: ["NX-3", "importable and invisible to Nx at the same time"],
    rejectText: ["NX-1"],
});

// ── the exact-match ratchet, in BOTH directions ─────────────────────────────
// Nothing is recorded any more, so the FIRST dangling alias / orphan tree is already a violation.
// The recorded-set directions run against an injected copy (see checkerWithRecorded).
const addGhostAlias = (r) => {
    const p = `${WS}/tsconfig.base.json`;
    const j = readJson(r, p);
    j.compilerOptions.paths[GHOST_ALIAS] = [GHOST_TARGET];
    writeJson(r, p, j);
};
const recordGhost = (r) =>
    checkerWithRecorded(r, { KNOWN_DANGLING_ALIASES: { [GHOST_ALIAS]: GHOST_TARGET } });
const recordScratch = (r) =>
    checkerWithRecorded(r, { KNOWN_ORPHAN_SOURCE_ROOTS: { [SCRATCH_ROOT]: "recorded by a self-test" } });

scenario("a dangling alias -> RED with nothing recorded (the set is not a suppression list)", {
    mutate: addGhostAlias,
    expectExit: 1,
    expectText: ["NX-4", "NEW dangling alias", GHOST_ALIAS],
});

scenario("a recorded dangling alias that still dangles is KNOWN, not a violation", {
    mutate: addGhostAlias,
    tool: recordGhost,
    expectExit: 0,
    expectText: ["KNOWN, exactly recorded", "NX-4", GHOST_ALIAS, "0 violation(s), 1 known"],
});

scenario("a recorded dangling alias that is FIXED -> RED until its entry is deleted", {
    tool: recordGhost,
    expectExit: 1,
    expectText: ["NX-4", "STALE RECORD", GHOST_ALIAS, "delete its entry"],
});

const addScratchOrphan = (r) => write(r, `${WS}/${SCRATCH_ROOT}/src/lib/scratch.ts`, "export const s = 1;\n");

scenario("a NEW orphan source tree under libs/ -> RED", {
    mutate: addScratchOrphan,
    expectExit: 1,
    expectText: ["NX-5", "NEW orphan source under libs/", SCRATCH_ROOT],
});

scenario("a recorded orphan source tree that is still there is KNOWN, not a violation", {
    mutate: addScratchOrphan,
    tool: recordScratch,
    expectExit: 0,
    expectText: ["KNOWN, exactly recorded", "NX-5", SCRATCH_ROOT, "0 violation(s), 1 known"],
});

scenario("the recorded orphan source tree being cleaned up -> RED until its entry is deleted", {
    tool: recordScratch,
    expectExit: 1,
    expectText: ["NX-5", "STALE RECORD", SCRATCH_ROOT, "delete its entry"],
});

// ── NX-6 — a tsconfig that cannot resolve its own base (T-0546) ─────────────
// The defect this replays: four customer libs extended `../../../../tsconfig.base.json`, one `../`
// too many, resolving to a path outside the workspace. With no spec in the lib, Jest printed
// "No tests found, exiting with code 0" and Nx reported success for years.
const BROKEN_LIB = "libs/cleansia-customer-features/orders";

scenario("NX-6: one `../` too many in `extends` -> RED, naming the path it resolved to", {
    mutate: (r) => {
        const p = `${WS}/${BROKEN_LIB}/tsconfig.json`;
        writeJson(r, p, { ...readJson(r, p), extends: `../${toBase(BROKEN_LIB)}` });
    },
    expectExit: 1,
    expectText: ["NX-6", `${BROKEN_LIB}/tsconfig.json`, "is not on disk", "TS5083"],
});

scenario("NX-6: every broken tsconfig is named, not just the first", {
    mutate: (r) => {
        for (const dir of [BROKEN_LIB, "libs/shared/components"]) {
            const p = `${WS}/${dir}/tsconfig.json`;
            writeJson(r, p, { ...readJson(r, p), extends: `../${toBase(dir)}` });
        }
    },
    expectExit: 1,
    expectText: [BROKEN_LIB, "libs/shared/components", "2 violation(s)"],
});

scenario("NX-6: a `references` entry naming a tsconfig that is not there -> RED", {
    mutate: (r) => rm(r, `${WS}/${BROKEN_LIB}/tsconfig.spec.json`),
    expectExit: 1,
    expectText: ["NX-6", "`references`: './tsconfig.spec.json' is not on disk"],
});

scenario("NX-6: a tsconfig that does not parse -> RED, never skipped", {
    mutate: (r) => write(r, `${WS}/${BROKEN_LIB}/tsconfig.lib.json`, "{ not json"),
    expectExit: 1,
    expectText: ["NX-6", "does not parse as JSON(C)"],
});

scenario("NX-6: comments and a $schema URL do not read as a parse failure", {
    mutate: (r) =>
        write(
            r,
            `${WS}/${BROKEN_LIB}/tsconfig.lib.json`,
            '// the lib compilation unit\n{\n  "$schema": "https://json.schemastore.org/tsconfig",\n' +
                '  /* inherits the solution config */\n  "extends": "./tsconfig.json"\n}\n',
        ),
    expectExit: 0,
    rejectText: ["NX-6"],
});

scenario("NX-6: a bare package specifier in `extends` is left to node resolution", {
    mutate: (r) => {
        const p = `${WS}/${BROKEN_LIB}/tsconfig.lib.json`;
        writeJson(r, p, { ...readJson(r, p), extends: "@tsconfig/strictest/tsconfig.json" });
    },
    expectExit: 0,
    rejectText: ["NX-6"],
});

scenario("NX-6: `extends` written without the .json extension still resolves", {
    mutate: (r) => {
        const p = `${WS}/${BROKEN_LIB}/tsconfig.lib.json`;
        writeJson(r, p, { ...readJson(r, p), extends: "./tsconfig" });
    },
    expectExit: 0,
    rejectText: ["NX-6"],
});

// ── NX-7 — a suite no run can select (T-0546) ───────────────────────────────
// `legal-pages` had a jest-shaped lib and no `test` target, so `run-many -t test --all` never listed
// it. An absent project prints nothing at all — the one failure mode no log can show you.
scenario("NX-7: a jest config with NO `test` target -> RED", {
    mutate: (r) => {
        const p = `${WS}/${BROKEN_LIB}/project.json`;
        const j = readJson(r, p);
        delete j.targets.test;
        writeJson(r, p, j);
    },
    expectExit: 1,
    expectText: ["NX-7", BROKEN_LIB, "declares NO `test` target"],
});

scenario("NX-7: a `test` target whose jestConfig option is not on disk -> RED", {
    mutate: (r) => {
        const p = `${WS}/${BROKEN_LIB}/project.json`;
        const j = readJson(r, p);
        j.targets.test.options.jestConfig = `${BROKEN_LIB}/jest.config.js`;
        writeJson(r, p, j);
    },
    expectExit: 1,
    expectText: ["NX-7", "`jestConfig`", "is not on disk"],
});

scenario("NX-7: a project-relative jestConfig is caught — the option is workspace-relative", {
    mutate: (r) => {
        const p = `${WS}/${BROKEN_LIB}/project.json`;
        const j = readJson(r, p);
        j.targets.test.options.jestConfig = "jest.config.ts";
        writeJson(r, p, j);
    },
    expectExit: 1,
    expectText: ["NX-7", "workspace-relative, not project-relative"],
});

scenario("NX-7: a jest `test` target with no jest config on disk -> RED", {
    mutate: (r) => rm(r, `${WS}/${BROKEN_LIB}/jest.config.ts`),
    expectExit: 1,
    expectText: ["NX-7", "declares a jest `test` target but no"],
});

scenario("NX-7: an UNREGISTERED lib's jest config is NX-1's business, not NX-7's", {
    mutate: (r) => rm(r, `${WS}/${BROKEN_LIB}/project.json`),
    expectExit: 1,
    expectText: ["NX-1"],
    rejectText: ["NX-7"],
});

// ── the new corpora are anchored too: an empty SCAN is illegal ──────────────
scenario("AC3: ZERO tsconfig files -> RED, not a pass over an unread corpus", {
    mutate: (r) => {
        rm(r, `${WS}/tsconfig.base.json`);
        for (const [dir] of LIBS) {
            for (const name of ["tsconfig.json", "tsconfig.lib.json", "tsconfig.spec.json"]) {
                rm(r, `${WS}/${dir}/${name}`);
            }
        }
    },
    expectExit: 1,
    expectText: ["P0", "ZERO tsconfig files", "HARD FAILURE, never a silent pass"],
});

scenario("AC3: registered projects but ZERO jest configs -> RED (the probe went stale)", {
    mutate: (r) => {
        for (const [dir] of LIBS) rm(r, `${WS}/${dir}/jest.config.ts`);
    },
    expectExit: 1,
    expectText: ["P0", "ZERO jest configs", "HARD FAILURE, never a silent pass"],
});

// ── the app roster: the concrete floor under the corpus anchors ─────────────
scenario("an app loses its project.json -> RED", {
    mutate: (r) => rm(r, `${WS}/apps/cleansia-partner.app/project.json`),
    expectExit: 1,
    expectText: ["NX-1", "cleansia-partner.app", "invisible to Nx"],
});

scenario("the DOT trap: an app renamed to cleansia-partner-app -> RED", {
    mutate: (r) => {
        const j = readJson(r, `${WS}/apps/cleansia-partner.app/project.json`);
        rm(r, `${WS}/apps/cleansia-partner.app`);
        writeJson(r, `${WS}/apps/cleansia-partner-app/project.json`, j);
    },
    expectExit: 1,
    expectText: ["P0", "rostered app 'cleansia-partner.app' is missing", "note the DOT"],
});

scenario("an app whose project name drifts from its directory -> RED", {
    mutate: (r) => {
        const p = `${WS}/apps/cleansia-admin.app/project.json`;
        writeJson(r, p, { ...readJson(r, p), name: "cleansia-admin-app" });
    },
    expectExit: 1,
    expectText: ["NX-2", "does not match its directory"],
});

// ── --warn is a reporting mode, not a second gate ───────────────────────────
{
    const root = freshRoot();
    try {
        rm(root, `${WS}/libs/cleansia-partner-features/dashboard/project.json`);
        const r = spawnSync(process.execPath, [TOOL, `--root=${root}`, "--warn"], { encoding: "utf8" });
        const out = `${r.stdout}${r.stderr}`;
        if (r.status === 0 && out.includes("NX-1") && out.includes("[--warn: exit 0]")) {
            console.log("  PASS  --warn still reports the violation and exits 0");
        } else {
            failed++;
            console.log(`  FAIL  --warn still reports the violation and exits 0 (exit ${r.status})`);
            console.log(out.split("\n").map((l) => `        > ${l}`).join("\n"));
        }
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

console.log(
    failed
        ? `\ncheck-nx-project-registration self-test: ${failed} FAILED`
        : "\ncheck-nx-project-registration self-test: all scenarios passed",
);
process.exit(failed ? 1 : 0);
