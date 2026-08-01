#!/usr/bin/env node
/**
 * Tests for tools/typecheck-apps.mjs — the NSwag regen-drift guard.
 *
 * Each case builds a throwaway workspace under the OS temp dir — never inside the repo, because a
 * fixture now contains an `apps/<app>/project.json` that Nx would infer as a real project if a
 * cancelled run skipped the cleanup — and points the guard at it with --root=. The fixture tsconfigs
 * reach the workspace's own node_modules through baseUrl+paths. Dependency-free, matching
 * agents/tools/*.test.mjs.
 *
 * The suite is the guard's own non-stub proof: replace the guard body with `process.exit(0)` and
 * every case asserting a non-zero exit goes red.
 *
 * Run: node tools/typecheck-apps.test.mjs   # or: npm run typecheck:test
 */
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const WORKSPACE = join(dirname(fileURLToPath(import.meta.url)), "..");
const TOOL = join(WORKSPACE, "tools", "typecheck-apps.mjs");

const appTsconfig = {
    compilerOptions: {
        strict: true,
        target: "es2022",
        module: "es2022",
        moduleResolution: "bundler",
        experimentalDecorators: true,
        skipLibCheck: true,
        types: [],
        baseUrl: WORKSPACE,
        paths: { "*": ["node_modules/*"] },
    },
    angularCompilerOptions: { strictTemplates: true },
    files: ["src/main.ts"],
};

// `units` maps an apps/ dir name to either its src/main.ts contents or a spec:
//   { main, noBuildTarget, noTsConfigOption, noTsConfigFile }
function run(units) {
    const root = mkdtempSync(join(tmpdir(), "typecheck-apps-fixture-"));
    try {
        mkdirSync(join(root, "apps"), { recursive: true });
        for (const [app, given] of Object.entries(units)) {
            const spec = typeof given === "string" ? { main: given } : given;
            const appDir = join(root, "apps", app);
            mkdirSync(join(appDir, "src"), { recursive: true });
            const project = { name: app, targets: {} };
            if (!spec.noBuildTarget) {
                project.targets.build = {
                    executor: "@angular/build:application",
                    options: spec.noTsConfigOption
                        ? {}
                        : { tsConfig: `apps/${app}/tsconfig.app.json` },
                };
            }
            writeFileSync(
                join(appDir, "project.json"),
                JSON.stringify(project, null, 2),
                "utf8",
            );
            if (!spec.noTsConfigFile) {
                writeFileSync(
                    join(appDir, "tsconfig.app.json"),
                    JSON.stringify(appTsconfig, null, 2),
                    "utf8",
                );
            }
            if (spec.main !== undefined) {
                writeFileSync(join(appDir, "src", "main.ts"), spec.main, "utf8");
            }
        }
        const r = spawnSync(process.execPath, [TOOL, `--root=${root}`], {
            encoding: "utf8",
            maxBuffer: 64 * 1024 * 1024,
        });
        // The Angular compiler colours its diagnostics even through a pipe.
        const out = `${r.stdout ?? ""}${r.stderr ?? ""}`.replace(
            /\[[0-9;]*m/g,
            "",
        );
        return { code: r.status, out };
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

// The exact shape NSwag emits: a nullable DTO member becomes a REQUIRED interface key, so an
// existing object-literal call site stops compiling.
const DRIFTED = `export interface ICreateOrderCommand {
  customerName: string;
  accessInstructions: string | undefined;
}
export class CreateOrderCommand {
  constructor(data: ICreateOrderCommand) {
    void data;
  }
}
export const command = new CreateOrderCommand({ customerName: 'x' });
`;

const WIRED = DRIFTED.replace(
    "{ customerName: 'x' }",
    "{ customerName: 'x', accessInstructions: undefined }",
);

const cases = [];
const test = (name, fn) => cases.push([name, fn]);

test("flags a missing required member, naming the call site", () => {
    const r = run({ "fixture.app": DRIFTED });
    assert.equal(r.code, 1, `expected exit 1, got ${r.code}\n${r.out}`);
    assert.match(r.out, /TS2345/, r.out);
    assert.match(r.out, /'accessInstructions' is missing/, r.out);
    assert.match(r.out, /src[/\\]main\.ts:10:47/, r.out);
});

test("passes a wired call site", () => {
    const r = run({ "fixture.app": WIRED });
    assert.equal(r.code, 0, `expected exit 0, got ${r.code}\n${r.out}`);
    assert.match(r.out, /1 app compilation unit checked/, r.out);
});

test("fails when it discovers no app compilation unit", () => {
    const r = run({});
    assert.equal(r.code, 1, `expected exit 1, got ${r.code}\n${r.out}`);
    assert.match(r.out, /no app compilation unit/i, r.out);
});

test("reports every unit — a failure does not stop the run", () => {
    const r = run({ "a-broken.app": DRIFTED, "z-clean.app": WIRED });
    assert.equal(r.code, 1, `expected exit 1, got ${r.code}\n${r.out}`);
    assert.match(r.out, /FAIL\s+a-broken\.app/, r.out);
    assert.match(r.out, /ok\s+z-clean\.app/, r.out);
});

test("reports Angular template diagnostics, which plain tsc does not see", () => {
    const r = run({
        "fixture.app": `import { Component } from '@angular/core';
@Component({ selector: 'fixture-root', template: '<div [hidden]="missingMember"></div>' })
export class FixtureComponent {}
`,
    });
    assert.equal(r.code, 1, `expected exit 1, got ${r.code}\n${r.out}`);
    assert.match(r.out, /TS2339/, r.out);
    assert.match(r.out, /missingMember/, r.out);
});

// Reporting "1 app compilation unit checked" and exiting 0 here is the T-0438 topology: one app
// silently unguarded while the guard stays green. Degraded coverage must fail, not shrink.
test("an app with a build target but no discovered unit fails the run", () => {
    const r = run({
        "a-present.app": WIRED,
        "z-missing.app": { main: WIRED, noTsConfigFile: true },
    });
    assert.equal(r.code, 1, `expected exit 1, got ${r.code}\n${r.out}`);
    assert.match(r.out, /refusing to check a partial set/, r.out);
    assert.match(r.out, /z-missing\.app[\s\S]*does not exist/, r.out);
    assert.doesNotMatch(r.out, /app compilation units? checked/, r.out);
});

test("a build target that declares no tsConfig fails the run", () => {
    const r = run({ "fixture.app": { main: WIRED, noTsConfigOption: true } });
    assert.equal(r.code, 1, `expected exit 1, got ${r.code}\n${r.out}`);
    assert.match(r.out, /no options\.tsConfig/, r.out);
});

test("an apps/ project with no build target is not a unit (the e2e projects)", () => {
    const r = run({
        "fixture.app": WIRED,
        "fixture.app-e2e": { noBuildTarget: true, noTsConfigFile: true },
    });
    assert.equal(r.code, 0, `expected exit 0, got ${r.code}\n${r.out}`);
    assert.match(r.out, /1 app compilation unit checked/, r.out);
});

let failed = 0;
for (const [name, fn] of cases) {
    try {
        fn();
        console.log(`  ok   ${name}`);
    } catch (e) {
        failed++;
        console.log(`  FAIL ${name}\n       ${e.message}`);
    }
}
console.log(
    failed === 0
        ? `\ntypecheck-apps guard: ${cases.length} passed`
        : `\ntypecheck-apps guard: ${failed}/${cases.length} FAILED`,
);
process.exit(failed === 0 ? 0 : 1);
