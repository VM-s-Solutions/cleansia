#!/usr/bin/env node
/**
 * Typechecks every Angular app compilation unit with the Angular compiler (`ngc --noEmit`).
 *
 * Chained onto every script that invokes NSwag: the generator emits a nullable DTO member as a
 * REQUIRED interface key, which breaks every existing object-literal call site, so the regen is the
 * moment the drift is created and the earliest point a check still costs one person one minute.
 * See agents/backlog/adr/0031-nswag-regen-drift-is-guarded-at-regen-time.md.
 *
 * The unit set is read from each `apps/*` project's build-target `options.tsConfig` — the same
 * string handed to `@angular/build:application` — so the checked file set is the production build's
 * by construction rather than by convention. A declared build target whose tsconfig cannot be
 * resolved is a hard failure, never a silent skip: a guard that can quietly check two of three apps
 * reproduces the incident it exists to prevent.
 *
 * Usage:
 *   node tools/typecheck-apps.mjs           # or: npm run typecheck
 *   node tools/typecheck-apps.mjs --root=X  # test-only: discover app units under X instead
 *
 * Covers type + Angular template diagnostics for each app's compilation unit — NOT bundling,
 * budgets, SSR prerender or styles. `npm run build:cleansia-{customer,partner,admin}` stays the gate
 * before a push; this only moves the type half of it early enough to matter.
 */
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const WORKSPACE = join(dirname(fileURLToPath(import.meta.url)), "..");
const rootArg = (
    process.argv.slice(2).find((a) => a.startsWith("--root=")) || ""
).split("=")[1];
const root = rootArg ? resolve(process.cwd(), rootArg) : WORKSPACE;

const readJson = (file) => JSON.parse(readFileSync(file, "utf8"));
const shown = (file) => relative(root, file).split("\\").join("/");
function fail(headline, details = []) {
    console.log(`typecheck: ${headline}`);
    for (const d of details) console.log(`  ${d}`);
    process.exit(1);
}

let compilerManifest;
try {
    compilerManifest = createRequire(import.meta.url).resolve(
        "@angular/compiler-cli/package.json",
    );
} catch {
    fail(
        `@angular/compiler-cli is not installed under ${WORKSPACE}/node_modules — run \`npm ci\` first`,
    );
}
const compiler = readJson(compilerManifest);
const ngc =
    compiler.bin?.ngc && resolve(dirname(compilerManifest), compiler.bin.ngc);
if (!ngc || !existsSync(ngc)) {
    fail(
        `@angular/compiler-cli ${compiler.version} declares bin.ngc=${compiler.bin?.ngc ?? "nothing"}, which is not on disk — the Angular version changed the compiler entry point; update tools/typecheck-apps.mjs (node_modules is fine, do NOT reinstall)`,
    );
}

const appsDir = join(root, "apps");
const unresolved = [];
const units = [];
for (const dir of existsSync(appsDir) ? readdirSync(appsDir).sort() : []) {
    const manifest = join(appsDir, dir, "project.json");
    if (!existsSync(manifest)) continue;
    let project;
    try {
        project = readJson(manifest);
    } catch (e) {
        unresolved.push(`${shown(manifest)} is not readable JSON — ${e.message}`);
        continue;
    }
    const build = project.targets?.build;
    if (!build) continue;
    const declared = build.options?.tsConfig;
    if (!declared) {
        unresolved.push(
            `${shown(manifest)} declares a build target with no options.tsConfig`,
        );
        continue;
    }
    const tsconfig = resolve(root, declared);
    if (!existsSync(tsconfig)) {
        unresolved.push(
            `${shown(manifest)} build target points at ${declared}, which does not exist`,
        );
        continue;
    }
    units.push({ name: project.name ?? dir, tsconfig });
}

if (unresolved.length) {
    fail(
        `${unresolved.length} app build target(s) could not be resolved — refusing to check a partial set`,
        unresolved,
    );
}
if (units.length === 0) {
    fail(
        `no app compilation unit found under ${appsDir} — a check that inspects nothing is a non-run, not a pass`,
    );
}

const started = Date.now();
const failures = [];
for (const unit of units) {
    const at = Date.now();
    const r = spawnSync(process.execPath, [ngc, "-p", unit.tsconfig, "--noEmit"], {
        cwd: root,
        encoding: "utf8",
        maxBuffer: 64 * 1024 * 1024,
    });
    const output = [r.stdout, r.stderr, r.error?.message]
        .filter(Boolean)
        .join("")
        .trim();
    const secs = ((Date.now() - at) / 1000).toFixed(1);
    if (r.status === 0) {
        console.log(`  ok   ${unit.name} (${secs}s)`);
    } else {
        console.log(`  FAIL ${unit.name} (${secs}s)`);
        failures.push({ name: unit.name, output });
    }
}
const total = ((Date.now() - started) / 1000).toFixed(1);

if (failures.length === 0) {
    console.log(
        `typecheck: OK (${units.length} app compilation unit${units.length === 1 ? "" : "s"} checked, ${total}s)`,
    );
    process.exit(0);
}

for (const f of failures) console.log(`\n--- ${f.name} ---\n${f.output}`);
console.log(
    [
        `\ntypecheck: ${failures.length}/${units.length} app compilation unit(s) FAILED (${total}s)`,
        "",
        "If you just regenerated an NSwag client: the generator emits a nullable DTO member as a",
        "REQUIRED interface key, so every existing object-literal call site stops compiling. Wire the",
        "call sites listed above; never hand-edit libs/core/*/src/lib/client/*-client.ts.",
        "",
        "This is a typecheck, not a build — run npm run build:cleansia-{customer,partner,admin}",
        "before pushing.",
    ].join("\n"),
);
process.exit(1);
