#!/usr/bin/env node
/**
 * T-0534 / T-0455 — the module-boundary gate.
 *
 * `@nx/enforce-module-boundaries` is configured (one table, `src/Cleansia.App/
 * eslint.module-boundaries.config.mjs`) and every lib is tagged. Neither fact makes it a GATE:
 * frontend-ci.yml's only lint step is `continue-on-error: true` (:73), so a boundary violation
 * reported there sets no exit code, and `nx affected -t lint` cannot select a project a change did
 * not touch. A cross-app client import therefore shipped and stayed shipped (T-0533). This tool is
 * the exit code.
 *
 * WHY IT LINTS FROM THE WORKSPACE ROOT RATHER THAN RUNNING `nx lint` PER PROJECT.
 *   The defect T-0534 records is that a PER-PROJECT `eslint.config.mjs` can quietly opt out of the
 *   rule — that is literally how the guard was off for 50 projects while reading as enforcement. A
 *   gate assembled from those same per-project configs inherits the hole it exists to close. ESLint
 *   9 flat config resolves ONE config from the cwd and applies it to every file it walks, so a
 *   single `npx eslint .` at `src/Cleansia.App` measures the whole workspace through the ROOT
 *   config — which spreads the shared table and cannot be overridden downward. Verified equal to
 *   the 70-project `nx run-many -t lint --all --skip-nx-cache` run at the commit that introduced
 *   this tool: both report the same 19 violations in the same 19 files.
 *
 * EXACT-MATCH RATCHET, in BOTH directions (`agents/process/enforcement.md` — "add enforcement
 * behind the cleanup, never in front of it"). KNOWN records the exact state at the commit that
 * introduced this tool, keyed by file + violation class + count:
 *   - a NEW violation      -> not in KNOWN                -> RED
 *   - a KNOWN one is FIXED -> its entry no longer matches -> RED ("stale entry, delete it")
 * so a recorded gap can only be closed deliberately and its entry must be deleted in the same
 * change. Nothing here is a suppression list.
 *
 * ANTI-FALSE-GREEN (ADR-0032 D3). An empty scan is illegal even where an empty result is legal:
 * fewer files linted than LINTED_FILE_FLOOR, an eslint process that failed to produce parseable
 * JSON, or an eslint exit code outside {0,1} is a HARD FAILURE. The summary always prints the
 * counts it read, so a green run states the corpus it was green over.
 *
 * Usage:
 *   node agents/tools/check-module-boundaries.mjs             # strict: any drift -> exit 1
 *   node agents/tools/check-module-boundaries.mjs --warn      # report, always exit 0
 *   node agents/tools/check-module-boundaries.mjs --report=F  # read a saved `eslint -f json`
 *                                                             #   instead of spawning one
 *   node agents/tools/check-module-boundaries.mjs --root=DIR  # run against another tree
 */
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const RULE = "@nx/enforce-module-boundaries";

/**
 * The message the rule emits is a paragraph; the CLASS is what a reader and a ratchet need. Order
 * matters only in that the first match wins, and the patterns are disjoint.
 */
const VIOLATION_CLASSES = [
    ["cross-scope", /can only depend on libs tagged with/],
    ["untagged-project", /without tags matching at least one constraint/],
    ["circular-dependency", /^Circular dependency between/],
    ["buildable-from-non-buildable", /^Buildable libraries cannot import or export/],
    ["static-import-of-lazy", /^Static imports of lazy-loaded libraries/],
    ["deep-relative-import", /must begin with a npm scope/],
];

/**
 * The EXACT state at the commit that introduced this tool, AFTER T-0455 retired the three
 * `*-services <-> *-stores` cycles (47 violations, all `circular-dependency`, gone). Every entry
 * below predates that work and belongs to a different rule class with a different fix:
 *
 *   - `buildable-from-non-buildable` x14 — `libs/shared/components` carries a package.json, so Nx
 *     treats it as buildable and refuses its imports of non-buildable shared libs. The fix is a
 *     build-setup decision about that one lib (publishable or not), not an import rewrite.
 *   - `static-import-of-lazy` x4 — each app shell statically imports `@cleansia/components` while
 *     its own `app.routes.ts` lazy-loads the same lib, so the "lazy" chunk is pulled in eagerly.
 *     The fix is an app-shell restructure, per app.
 *   - `deep-relative-import` x1 — `invoice-management` reaches into `employee-management`'s source
 *     through `../../../../` for the reject dialog, which that lib's barrel does not export. The
 *     fix is a decision between widening that barrel and moving the dialog to `shared/components`.
 *
 * None is a cross-scope or untagged violation: after the cycles were retired the workspace has
 * ZERO of both, which is the number this gate exists to hold at zero.
 */
const KNOWN = [
    { file: "apps/cleansia-partner.app/src/app/app.component.ts", class: "static-import-of-lazy", count: 1 },
    { file: "apps/cleansia.app/src/app/app.ts", class: "static-import-of-lazy", count: 1 },
    { file: "apps/cleansia.app/src/app/components/footer/customer-footer.component.ts", class: "static-import-of-lazy", count: 1 },
    { file: "apps/cleansia.app/src/app/components/navbar/customer-navbar.component.ts", class: "static-import-of-lazy", count: 1 },
    { file: "libs/cleansia-admin-features/invoice-management/src/lib/invoice-detail/invoice-detail.facade.ts", class: "deep-relative-import", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-address-autocomplete/cleansia-address-autocomplete.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-calendar/cleansia-calendar.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-checkbox/cleansia-checkbox.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-cookie-consent/cleansia-cookie-consent.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-file/cleansia-file.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-help-card/cleansia-help-card.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-multiselect/cleansia-multiselect.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-radio/cleansia-radio.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-select/cleansia-select.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-sidebar-menu/cleansia-sidebar-menu.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-telephone/cleansia-telephone.component.ts", class: "buildable-from-non-buildable", count: 2 },
    { file: "libs/shared/components/src/lib/cleansia-text-input/cleansia-text-input.component.ts", class: "buildable-from-non-buildable", count: 1 },
    { file: "libs/shared/components/src/lib/cleansia-textarea/cleansia-textarea.component.ts", class: "buildable-from-non-buildable", count: 1 },
];

/**
 * The workspace held 1340 lintable files when this floor was set. A run that walks a small
 * fraction of that has been mis-invoked (wrong cwd, a stray `ignores`, a half-checkout) and its
 * silence means nothing. Deliberately loose so ordinary growth or pruning does not trip it.
 */
const LINTED_FILE_FLOOR = 800;

const args = process.argv.slice(2);
const warnOnly = args.includes("--warn");
const arg = (name) => (args.find((a) => a.startsWith(`--${name}=`)) || "").split("=").slice(1).join("=");
const rootArg = arg("root");
const reportArg = arg("report");
const floorArg = arg("floor");

const REPO = rootArg
    ? resolve(rootArg)
    : resolve(join(fileURLToPath(import.meta.url), "..", "..", "..")); // agents/tools -> repo root
const WORKSPACE = join(REPO, "src/Cleansia.App");
const FLOOR = floorArg ? Number(floorArg) : LINTED_FILE_FLOOR;

const fail = (message) => {
    console.error(`module-boundaries: ${message}`);
    process.exit(warnOnly ? 0 : 1);
};

function readReport() {
    if (reportArg) {
        const path = resolve(reportArg);
        if (!existsSync(path)) fail(`--report=${reportArg} does not exist`);
        return JSON.parse(readFileSync(path, "utf8"));
    }

    if (!existsSync(join(WORKSPACE, "eslint.config.mjs"))) {
        fail(`no eslint.config.mjs at ${WORKSPACE} — wrong --root, or the workspace moved`);
    }

    const run = spawnSync("npx", ["eslint", ".", "--format", "json"], {
        cwd: WORKSPACE,
        encoding: "utf8",
        maxBuffer: 256 * 1024 * 1024,
        shell: process.platform === "win32",
    });

    // eslint exits 0 with no findings and 1 with findings. Anything else (2 = config/crash, null =
    // killed) is a broken run, and a broken run must never read as "no violations".
    if (run.error) fail(`could not run eslint: ${run.error.message}`);
    if (run.status !== 0 && run.status !== 1) {
        fail(`eslint exited ${run.status}\n${(run.stderr || "").slice(0, 4000)}`);
    }
    try {
        return JSON.parse(run.stdout);
    } catch {
        fail(`eslint did not produce parseable JSON\n${(run.stderr || run.stdout || "").slice(0, 4000)}`);
    }
}

function classify(message) {
    for (const [name, pattern] of VIOLATION_CLASSES) {
        if (pattern.test(message)) return name;
    }
    return "other";
}

function toWorkspaceRelative(filePath) {
    const marker = `${sep}src${sep}Cleansia.App${sep}`;
    const at = filePath.lastIndexOf(marker);
    const relative = at === -1 ? filePath : filePath.slice(at + marker.length);
    return relative.split(sep).join("/");
}

function collect(report) {
    if (!Array.isArray(report)) fail("the eslint report is not an array");
    if (report.length === 0) fail("eslint reported ZERO files — an empty scan is a failure, not a pass");
    if (report.length < FLOOR) {
        fail(
            `eslint linted only ${report.length} file(s), below the floor of ${FLOOR}. ` +
                "A partial walk cannot certify anything."
        );
    }

    const found = new Map();
    for (const file of report) {
        for (const message of file.messages || []) {
            if (message.ruleId !== RULE) continue;
            const key = `${toWorkspaceRelative(file.filePath)}::${classify(message.message)}`;
            found.set(key, (found.get(key) ?? 0) + 1);
        }
    }
    return { found, linted: report.length };
}

const report = readReport();
const { found, linted } = collect(report);

const expected = new Map(KNOWN.map((k) => [`${k.file}::${k.class}`, k.count]));
const violations = [];

for (const [key, count] of found) {
    const known = expected.get(key);
    if (known === undefined) {
        violations.push(`NEW      ${key} (x${count})`);
    } else if (known !== count) {
        violations.push(`CHANGED  ${key} — recorded x${known}, found x${count}`);
    }
}
for (const [key, count] of expected) {
    if (!found.has(key)) {
        violations.push(`STALE    ${key} (x${count}) is recorded but no longer reported — delete its KNOWN entry`);
    }
}

const total = [...found.values()].reduce((a, b) => a + b, 0);
const byClass = new Map();
for (const [key, count] of found) {
    const cls = key.split("::")[1];
    byClass.set(cls, (byClass.get(cls) ?? 0) + count);
}

console.log(
    `module-boundaries: linted ${linted} file(s), ${total} ${RULE} violation(s) in ${found.size} file/class pair(s), ${KNOWN.length} known`
);
for (const [cls, count] of [...byClass].sort((a, b) => b[1] - a[1])) {
    console.log(`  ${String(count).padStart(4)}  ${cls}`);
}

if (violations.length === 0) {
    console.log("\nmodule-boundaries: 0 drift from the recorded set");
    process.exit(0);
}

console.error(`\nmodule-boundaries: ${violations.length} drift(s) from the recorded set:`);
for (const v of violations.sort()) console.error(`  ${v}`);
console.error(
    "\nA NEW entry means an import crossed a boundary — fix the import, do not record it.\n" +
        "A STALE entry means a recorded gap was closed — delete it from KNOWN in the same change."
);
process.exit(warnOnly ? 0 : 1);
