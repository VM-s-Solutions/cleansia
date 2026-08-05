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
 * mutates exactly one thing. The fixture deliberately reproduces the checker's recorded sets — the
 * three dangling aliases and the one orphan source root — so the exact-match ratchet is exercised
 * against its real recorded values rather than against injected stand-ins.
 *
 * The AC3 scenarios are the point of the file: a corpus that has gone EMPTY must be RED. A guard
 * that goes green because it looked at nothing is the failure this ticket exists to prevent.
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

/** The checker's recorded sets, reproduced so the exact-match ratchet has its real subject. */
const RECORDED_DANGLING = {
    "@cleansia.app/order-details": "cleansia-partner-features/order-details/src/index.ts",
    "@cleansia/cleansia-services": "libs/cleansia-services/src/index.ts",
    "@cleansia/stores": "libs/data-access/stores/src/index.ts",
};
const RECORDED_ORPHAN = "libs/cleansia/src/lib/cleansia/cleansia.ts";

const write = (root, p, body) => {
    const abs = join(root, p);
    mkdirSync(dirname(abs), { recursive: true });
    writeFileSync(abs, body);
};

const writeJson = (root, p, value) => write(root, p, `${JSON.stringify(value, null, 2)}\n`);

const readJson = (root, p) => JSON.parse(readFileSync(join(root, p), "utf8"));

const rm = (root, p) => rmSync(join(root, p), { recursive: true, force: true });

const WS = "src/Cleansia.App";

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
        });
        paths[alias] = [`${dir}/src/index.ts`];
    }
    for (const [alias, target] of Object.entries(RECORDED_DANGLING)) paths[alias] = [target];
    writeJson(root, `${WS}/tsconfig.base.json`, { compilerOptions: { paths } });

    write(root, `${WS}/${RECORDED_ORPHAN}`, "export class Cleansia {}\n");

    for (const app of APPS) {
        writeJson(root, `${WS}/apps/${app}/project.json`, {
            name: app,
            projectType: "application",
            tags: ["scope:shared", "type:app"],
        });
    }
    return root;
}

const run = (root) => spawnSync(process.execPath, [TOOL, `--root=${root}`], { encoding: "utf8" });

let failed = 0;
function scenario(name, { mutate, expectExit, expectText = [], rejectText = [] }) {
    const root = freshRoot();
    try {
        if (mutate) mutate(root);
        const r = run(root);
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
    // 6 aliases into libs/: the 4 real ones plus the 2 recorded dangling targets that are also
    // libs/-prefixed. The count is what the tool READ, not what it approved.
    expectText: [
        "read 4 lib root(s), 4 registered project(s), 6 alias(es) into libs/, 3 rostered app(s)",
        "0 violation(s), 4 known",
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
scenario("a FOURTH dangling alias -> RED (the recorded set is not a suppression list)", {
    mutate: (r) => {
        const p = `${WS}/tsconfig.base.json`;
        const j = readJson(r, p);
        j.compilerOptions.paths["@cleansia/ghost"] = ["libs/ghost/src/index.ts"];
        writeJson(r, p, j);
    },
    expectExit: 1,
    expectText: ["NX-4", "NEW dangling alias", "@cleansia/ghost"],
});

scenario("a recorded dangling alias that is FIXED -> RED until its entry is deleted", {
    mutate: (r) => {
        const p = `${WS}/tsconfig.base.json`;
        const j = readJson(r, p);
        delete j.compilerOptions.paths["@cleansia/stores"];
        writeJson(r, p, j);
    },
    expectExit: 1,
    expectText: ["NX-4", "STALE RECORD", "@cleansia/stores", "delete its entry"],
});

scenario("a NEW orphan source tree under libs/ -> RED", {
    mutate: (r) => write(r, `${WS}/libs/scratch/src/lib/scratch.ts`, "export const s = 1;\n"),
    expectExit: 1,
    expectText: ["NX-5", "NEW orphan source under libs/", "libs/scratch"],
});

scenario("the recorded orphan source tree being cleaned up -> RED until its entry is deleted", {
    mutate: (r) => rm(r, `${WS}/libs/cleansia`),
    expectExit: 1,
    expectText: ["NX-5", "STALE RECORD", "libs/cleansia", "delete its entry"],
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
