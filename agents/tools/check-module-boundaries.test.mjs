#!/usr/bin/env node
/**
 * Self-test for the module-boundary gate (`check-module-boundaries.mjs`).
 *
 * It never runs eslint and never touches the working tree: every scenario writes a synthetic
 * `eslint --format json` report to a throwaway file and feeds it through `--report=`, so the tool's
 * classification, its exact-match ratchet and its anti-vacuity anchors are all covered in about a
 * second. The one thing this cannot cover — that eslint really reports what the fixtures claim — is
 * covered by the gate's own run in the same workflow, over the real workspace.
 *
 * The shipped KNOWN set is NON-empty (18 entries, 19 violations — three rule classes that predate
 * this tool and each need their own decision), so the ratchet has a live subject and the scenarios
 * below drive it directly rather than through an injected copy.
 *
 * Stub the tool's body to `process.exit(0)` and every scenario that expects exit 1 goes red.
 *
 *   node agents/tools/check-module-boundaries.test.mjs
 */
import { spawnSync } from "node:child_process";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const TOOL = join(HERE, "check-module-boundaries.mjs");
const RULE = "@nx/enforce-module-boundaries";

const WS = "/repo/src/Cleansia.App";

/** The shipped KNOWN set, as eslint would report it. Kept here so a drift in either goes red. */
const BASELINE = [
    ["apps/cleansia-partner.app/src/app/app.component.ts", "Static imports of lazy-loaded libraries are forbidden.\n\nLibrary \"components\" is lazy-loaded", 1],
    ["apps/cleansia.app/src/app/app.ts", "Static imports of lazy-loaded libraries are forbidden.", 1],
    ["apps/cleansia.app/src/app/components/footer/customer-footer.component.ts", "Static imports of lazy-loaded libraries are forbidden.", 1],
    ["apps/cleansia.app/src/app/components/navbar/customer-navbar.component.ts", "Static imports of lazy-loaded libraries are forbidden.", 1],
    ["libs/cleansia-admin-features/invoice-management/src/lib/invoice-detail/invoice-detail.facade.ts", "Projects cannot be imported by a relative or absolute path, and must begin with a npm scope", 1],
    ["libs/shared/components/src/lib/cleansia-address-autocomplete/cleansia-address-autocomplete.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-calendar/cleansia-calendar.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-checkbox/cleansia-checkbox.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-cookie-consent/cleansia-cookie-consent.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-file/cleansia-file.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-help-card/cleansia-help-card.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-multiselect/cleansia-multiselect.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-radio/cleansia-radio.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-select/cleansia-select.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-sidebar-menu/cleansia-sidebar-menu.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-telephone/cleansia-telephone.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 2],
    ["libs/shared/components/src/lib/cleansia-text-input/cleansia-text-input.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
    ["libs/shared/components/src/lib/cleansia-textarea/cleansia-textarea.component.ts", "Buildable libraries cannot import or export from non-buildable libraries", 1],
];

const CROSS_SCOPE =
    'A project tagged with "scope:customer" can only depend on libs tagged with "scope:customer", "scope:shared"';
const CIRCULAR =
    'Circular dependency between "partner-services" and "partner-stores" detected: partner-services -> partner-stores -> partner-services';
const UNTAGGED = "A project without tags matching at least one constraint cannot depend on any libraries";

/** A report the tool will accept as a full walk: the baseline plus enough clean files to clear the floor. */
function baselineReport(extra = [], { padTo = 900 } = {}) {
    const files = [];
    for (const [rel, message, count] of BASELINE) {
        files.push({
            filePath: `${WS}/${rel}`,
            messages: Array.from({ length: count }, () => ({ ruleId: RULE, severity: 2, message })),
        });
    }
    for (const [rel, message] of extra) {
        files.push({
            filePath: `${WS}/${rel}`,
            messages: [{ ruleId: RULE, severity: 2, message }],
        });
    }
    while (files.length < padTo) {
        files.push({
            filePath: `${WS}/libs/filler/src/lib/file-${files.length}.ts`,
            // A file with unrelated findings must not register as a boundary violation.
            messages: [{ ruleId: "@typescript-eslint/no-explicit-any", severity: 1, message: "Unexpected any." }],
        });
    }
    return files;
}

/** The baseline minus one recorded entry — i.e. somebody fixed it and left the entry behind. */
function baselineMissing(rel) {
    return baselineReport().filter((f) => f.filePath !== `${WS}/${rel}`);
}

let failed = 0;
const root = mkdtempSync(join(tmpdir(), "module-boundaries-selftest-"));

function scenario(name, { report, args = [], expectExit, expectText = [], rejectText = [] }) {
    const path = join(root, `report-${Math.random().toString(36).slice(2)}.json`);
    try {
        writeFileSync(path, typeof report === "string" ? report : JSON.stringify(report));
        const r = spawnSync(process.execPath, [TOOL, `--report=${path}`, ...args], { encoding: "utf8" });
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
        rmSync(path, { force: true });
    }
}

console.log("check-module-boundaries self-test:");

scenario("the recorded state is clean, and the run states the corpus it read", {
    report: baselineReport(),
    expectExit: 0,
    expectText: ["19 @nx/enforce-module-boundaries violation(s) in 18 file/class pair(s), 18 known", "0 drift"],
});

// ── the violation classes the gate holds at ZERO ────────────────────────────
scenario("T-0533's own violation: a customer lib importing the partner client -> RED, classed cross-scope", {
    report: baselineReport([["libs/core/customer-services/src/lib/services/customer-auth.service.ts", CROSS_SCOPE]]),
    expectExit: 1,
    expectText: ["NEW", "customer-auth.service.ts::cross-scope", "1 drift"],
});

scenario("a cross-scope break inside a FEATURE lib -> RED (the class that was invisible for 50 projects)", {
    report: baselineReport([["libs/cleansia-customer-features/orders/src/lib/orders/orders.facade.ts", CROSS_SCOPE]]),
    expectExit: 1,
    expectText: ["NEW", "orders.facade.ts::cross-scope"],
});

scenario("T-0455's cycle coming back -> RED, classed circular-dependency", {
    report: baselineReport([["libs/core/partner-services/src/lib/interceptors/loading.interceptor.ts", CIRCULAR]]),
    expectExit: 1,
    expectText: ["NEW", "loading.interceptor.ts::circular-dependency"],
});

scenario("an untagged project -> RED, classed untagged-project", {
    report: baselineReport([["libs/shared/brand-new/src/index.ts", UNTAGGED]]),
    expectExit: 1,
    expectText: ["NEW", "index.ts::untagged-project"],
});

scenario("a rule message the classifier does not know is still counted, as `other`", {
    report: baselineReport([["libs/shared/utils/src/lib/thing.ts", "Some future @nx boundary message"]]),
    expectExit: 1,
    expectText: ["NEW", "thing.ts::other"],
});

// ── the ratchet, in BOTH directions ─────────────────────────────────────────
scenario("a recorded violation that is FIXED -> RED, telling you to delete the entry", {
    report: baselineMissing("libs/shared/components/src/lib/cleansia-calendar/cleansia-calendar.component.ts"),
    expectExit: 1,
    expectText: ["STALE", "cleansia-calendar.component.ts::buildable-from-non-buildable", "delete its KNOWN entry"],
});

scenario("a recorded file that grows an EXTRA violation -> RED (count is part of the match)", {
    report: baselineReport([[
        "libs/shared/components/src/lib/cleansia-calendar/cleansia-calendar.component.ts",
        "Buildable libraries cannot import or export from non-buildable libraries",
    ]]),
    expectExit: 1,
    expectText: ["CHANGED", "recorded x1, found x2"],
});

scenario("a recorded file whose count DROPS -> RED too, not a quiet improvement", {
    report: baselineReport().map((f) =>
        f.filePath.endsWith("cleansia-telephone.component.ts") ? { ...f, messages: f.messages.slice(0, 1) } : f
    ),
    expectExit: 1,
    expectText: ["CHANGED", "cleansia-telephone.component.ts::buildable-from-non-buildable", "recorded x2, found x1"],
});

scenario("a recorded violation that MOVES to another file is two drifts, not zero", {
    report: baselineMissing("libs/shared/components/src/lib/cleansia-radio/cleansia-radio.component.ts").concat([{
        filePath: `${WS}/libs/shared/components/src/lib/cleansia-radio-v2/cleansia-radio-v2.component.ts`,
        messages: [{ ruleId: RULE, severity: 2, message: "Buildable libraries cannot import or export from non-buildable libraries" }],
    }]),
    expectExit: 1,
    expectText: ["NEW", "STALE", "2 drift(s)"],
});

// ── anti-vacuity: an empty or partial scan must never read as a pass ────────
scenario("ZERO files linted -> RED, not a pass over an unread corpus", {
    report: [],
    expectExit: 1,
    expectText: ["ZERO files", "empty scan is a failure"],
});

scenario("a walk far below the file floor -> RED (mis-invoked, its silence means nothing)", {
    report: baselineReport([], { padTo: 40 }),
    expectExit: 1,
    expectText: ["below the floor", "cannot certify"],
});

scenario("a report that is not an array -> RED", {
    report: { results: [] },
    expectExit: 1,
    expectText: ["not an array"],
});

scenario("a report that is not JSON at all -> RED", {
    report: "eslint crashed",
    expectExit: 1,
    expectText: [],
});

scenario("EVERY violation gone while entries remain recorded -> RED with one stale line per entry", {
    report: baselineReport().map((f) => ({ ...f, messages: [] })),
    expectExit: 1,
    expectText: ["STALE", "18 drift(s)"],
});

// ── the floor is a floor, not a hard-coded corpus size ──────────────────────
scenario("a workspace that grew past the floor is still measured, not tripped", {
    report: baselineReport([], { padTo: 2500 }),
    expectExit: 0,
    expectText: ["linted 2500 file(s)", "0 drift"],
});

// ── modes ───────────────────────────────────────────────────────────────────
scenario("--warn reports the drift and still exits 0", {
    report: baselineReport([["libs/core/customer-services/src/lib/services/customer-auth.service.ts", CROSS_SCOPE]]),
    args: ["--warn"],
    expectExit: 0,
    expectText: ["NEW", "cross-scope"],
});

scenario("--warn does NOT hide an empty scan's message either", {
    report: [],
    args: ["--warn"],
    expectExit: 0,
    expectText: ["empty scan is a failure"],
});

scenario("--floor overrides the shipped floor (the self-test's own escape hatch, and it is honest)", {
    report: baselineReport([], { padTo: 30 }),
    args: ["--floor=10"],
    expectExit: 0,
    expectText: ["linted 30 file(s)", "0 drift"],
});

// ── a missing report path is a broken run, not an empty one ─────────────────
{
    const r = spawnSync(process.execPath, [TOOL, "--report=/nonexistent/eslint-report.json"], { encoding: "utf8" });
    const out = `${r.stdout}${r.stderr}`;
    if (r.status === 1 && out.includes("does not exist")) {
        console.log("  PASS  a --report path that is not on disk -> RED");
    } else {
        failed++;
        console.log(`  FAIL  a --report path that is not on disk -> RED (exit ${r.status})\n        > ${out}`);
    }
}

// ── the workspace probe is real: a wrong --root cannot silently spawn eslint anywhere ──
{
    const r = spawnSync(process.execPath, [TOOL, `--root=${root}`], { encoding: "utf8" });
    const out = `${r.stdout}${r.stderr}`;
    if (r.status === 1 && out.includes("no eslint.config.mjs")) {
        console.log("  PASS  a --root with no Nx workspace under it -> RED before eslint is spawned");
    } else {
        failed++;
        console.log(`  FAIL  a --root with no Nx workspace under it -> RED (exit ${r.status})\n        > ${out}`);
    }
}

rmSync(root, { recursive: true, force: true });

if (failed > 0) {
    console.log(`\ncheck-module-boundaries self-test: ${failed} scenario(s) FAILED`);
    process.exit(1);
}
console.log("\ncheck-module-boundaries self-test: all scenarios passed");
