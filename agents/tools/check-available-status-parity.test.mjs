#!/usr/bin/env node
/**
 * Self-test for ADR-0037 D7 layer 2 (`check-available-status-parity.mjs`).
 *
 * ADR-0037 D7 ruling 5 states layer 2's acceptance test BEHAVIOURALLY: *"delete one status from one
 * client literal ... and the PR must go red. If it does not, layer 2 does not exist."* This file runs
 * that acceptance test on every CI run, so the guard cannot decay into scaffolding — the exact
 * failure mode `frontend-ci.yml`'s "Regen-drift guard self-test" step exists to prevent.
 *
 * It never touches the working tree. It copies the ten files the checker reads into a throwaway root
 * (preserving their repo-relative paths), mutates ONE line, and asserts the exit code and the named
 * surface. Case 7 mutates the CANONICAL C# rule instead of a client and asserts the clients go red —
 * that is what proves the checker parses `OrderAvailability.cs` rather than carrying its own copy of
 * the answer.
 *
 *   node agents/tools/check-available-status-parity.test.mjs
 */
import { cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, join, resolve } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = resolve(join(HERE, "..", ".."));
const TOOL = join(HERE, "check-available-status-parity.mjs");

const FILES = [
    "src/Cleansia.Core.Domain/Enums/OrderStatus.cs",
    "src/Cleansia.Core.Domain/Orders/OrderAvailability.cs",
    "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts",
    "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.models.ts",
    "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.helpers.ts",
    "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/order-details/order-details.helpers.ts",
    "src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/orders/OrdersListViewModel.kt",
    "src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/orders/OrderPrimaryAction.kt",
    "src/cleansia_ios/CleansiaPartner/Sources/Features/Orders/OrdersListLogic.swift",
    "src/cleansia_ios/CleansiaPartner/Sources/Features/Orders/OrderPrimaryAction.swift",
];

function freshRoot() {
    const root = mkdtempSync(join(tmpdir(), "offerability-parity-"));
    for (const f of FILES) {
        const dst = join(root, f);
        mkdirSync(dirname(dst), { recursive: true });
        cpSync(join(REPO, f), dst);
    }
    return root;
}

function patch(root, file, from, to) {
    const p = join(root, file);
    const src = readFileSync(p, "utf8");
    if (!src.includes(from)) {
        throw new Error(`fixture patch target vanished in ${file}: ${JSON.stringify(from)}`);
    }
    writeFileSync(p, src.replace(from, to));
}

const run = (root, tool = TOOL) =>
    spawnSync(process.execPath, [tool, `--root=${root}`, "--baseline"], { encoding: "utf8" });

const BASELINE_DECL = /const BASELINE = \{[\s\S]*?\n?\};/;

/**
 * The shipped BASELINE is empty (T-0530 landed), so the baseline MACHINERY has no live subject to
 * exercise. Rather than delete its scenarios — the next agent to add an entry needs the exact-match
 * ratchet to still work — they run against a copy of the checker with an injected BASELINE.
 */
function toolWithBaseline(root, baseline) {
    const src = readFileSync(TOOL, "utf8");
    if (!BASELINE_DECL.test(src)) {
        throw new Error("BASELINE declaration not found in the checker — this self-test is stale");
    }
    const dst = join(root, "checker-with-baseline.mjs");
    writeFileSync(dst, src.replace(BASELINE_DECL, `const BASELINE = ${JSON.stringify(baseline)};`));
    return dst;
}

let failed = 0;
function scenario(name, { mutate, tool, expectExit, expectText }) {
    const root = freshRoot();
    try {
        if (mutate) mutate(root);
        const r = run(root, tool ? tool(root) : TOOL);
        const out = `${r.stdout}${r.stderr}`;
        const okExit = r.status === expectExit;
        const okText = !expectText || expectText.every((t) => out.includes(t));
        if (okExit && okText) {
            console.log(`  PASS  ${name}`);
        } else {
            failed++;
            console.log(`  FAIL  ${name}`);
            console.log(`        expected exit ${expectExit}, got ${r.status}`);
            if (!okText) {
                console.log(`        expected output to contain: ${expectText.join(" | ")}`);
            }
            console.log(out.split("\n").map((l) => `        > ${l}`).join("\n"));
        }
    } catch (e) {
        failed++;
        console.log(`  FAIL  ${name} — ${e.message}`);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

console.log("check-available-status-parity self-test (ADR-0037 D7 ruling 5):");

// 0 — every surface agrees with the canonical floor and nothing is baselined away.
scenario("unmutated tree is clean with an EMPTY baseline, all eight surfaces read", {
    expectExit: 0,
    expectText: ["8/8 surfaces read", "0 violation(s)", "0 known divergence(s)"],
});

// 1..3 — the ADR's literal acceptance test on each client stack that is NOT baselined.
scenario("Android take-BUTTON gate loses a status -> RED", {
    mutate: (r) =>
        patch(
            r,
            "src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/orders/OrderPrimaryAction.kt",
            "OrderStatus._0, OrderStatus._2 ->",
            "OrderStatus._2 ->",
        ),
    expectExit: 1,
    expectText: ["[android.button.take]", "MISSING"],
});

scenario("Android Available QUERY literal gains dead Pending -> RED", {
    mutate: (r) =>
        patch(
            r,
            "src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/orders/OrdersListViewModel.kt",
            "listOf(OrderStatus._0, OrderStatus._2)",
            "listOf(OrderStatus._0, OrderStatus._1, OrderStatus._2)",
        ),
    expectExit: 1,
    expectText: ["[android.query.available]", "EXTRA"],
});

scenario("iOS Available QUERY literal loses a status -> RED", {
    mutate: (r) =>
        patch(
            r,
            "src/cleansia_ios/CleansiaPartner/Sources/Features/Orders/OrdersListLogic.swift",
            "statuses: [._0, ._2],",
            "statuses: [._2],",
        ),
    expectExit: 1,
    expectText: ["[ios.query.available]", "MISSING"],
});

scenario("iOS take-BUTTON gate stops offering .take on New -> RED", {
    mutate: (r) =>
        patch(
            r,
            "src/cleansia_ios/CleansiaPartner/Sources/Features/Orders/OrderPrimaryAction.swift",
            "return isMine ? .none : .take",
            "return .none",
        ),
    expectExit: 1,
    expectText: ["[ios.button.take]", "MISSING"],
});

// 4 — a moved/renamed surface must be a HARD failure, never a silent pass. This is the property that
//     separates this check from a grep that quietly matches nothing.
scenario("a renamed surface is P0 (stale extractor), NOT a pass", {
    mutate: (r) =>
        patch(
            r,
            "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/order-details/order-details.helpers.ts",
            "export function canTakeOrder(",
            "export function canAcceptOrder(",
        ),
    expectExit: 1,
    expectText: ["P0", "[web.button.detail]", "NOT a pass"],
});

// 5 — the web surfaces are gated strictly now that T-0530 has landed. This is the defect the ADR
//     calls the worst of the four: the detail page hiding Take for the whole New + Cash pipeline.
scenario("web detail take-BUTTON gate stops offering New -> RED", {
    mutate: (r) =>
        patch(
            r,
            "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/order-details/order-details.helpers.ts",
            "orderStatusValue === OrderStatus.New || orderStatusValue === OrderStatus.Confirmed",
            "orderStatusValue === OrderStatus.Confirmed",
        ),
    expectExit: 1,
    expectText: ["[web.button.detail]", "MISSING", "New(0)"],
});

// 6..8 — the baseline machinery, exercised against an INJECTED baseline because the shipped one is
//     empty. A baselined gap must be reported and survivable; a baseline that no longer describes
//     reality — in either direction — must be RED, so an entry cannot outlive its ticket.
scenario("an exactly-baselined divergence is reported, not hidden, and does not fail the build", {
    mutate: (r) =>
        patch(
            r,
            "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts",
            "        OrderStatus.New,\n        OrderStatus.Confirmed,",
            "        OrderStatus.New,\n        OrderStatus.Pending,\n        OrderStatus.Confirmed,",
        ),
    tool: (r) =>
        toolWithBaseline(r, {
            "web.query.available": { statuses: [0, 1, 2], ticket: "T-TEST", why: "fixture" },
        }),
    expectExit: 0,
    expectText: ["KNOWN divergences", "[web.query.available]", "1 known divergence(s)"],
});

scenario("a baselined surface that drifts to a DIFFERENT set goes RED", {
    tool: (r) =>
        toolWithBaseline(r, {
            "web.query.available": { statuses: [0, 1, 2], ticket: "T-TEST", why: "fixture" },
        }),
    expectExit: 1,
    expectText: ["BASELINE STALE", "[web.query.available]"],
});

scenario("a baselined surface that AGREES with the floor goes RED until its entry is deleted", {
    tool: (r) =>
        toolWithBaseline(r, {
            "web.query.available": { statuses: [0, 2], ticket: "T-TEST", why: "fixture" },
        }),
    expectExit: 1,
    expectText: ["BASELINE STALE", "now AGREES with the floor"],
});

// 6 — THE test that proves the canonical C# is the source of truth. Widen the domain floor and the
//     two already-correct mobile clients must go red for disagreeing with the NEW floor. A checker
//     carrying its own hardcoded {New, Confirmed} would stay green here.
scenario("widening the CANONICAL C# floor reddens the mobile clients (canonical is really parsed)", {
    mutate: (r) =>
        patch(
            r,
            "src/Cleansia.Core.Domain/Orders/OrderAvailability.cs",
            "[OrderStatus.New, OrderStatus.Confirmed]",
            "[OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay]",
        ),
    expectExit: 1,
    expectText: ["[android.query.available]", "[ios.query.available]", "OnTheWay(3)"],
});

// 7 — losing the canonical file must fail loudly, not check nothing.
scenario("a missing canonical rule file is P0", {
    mutate: (r) => rmSync(join(r, "src/Cleansia.Core.Domain/Orders/OrderAvailability.cs")),
    expectExit: 1,
    expectText: ["P0", "OrderAvailability.cs not found"],
});

console.log(
    failed
        ? `\ncheck-available-status-parity self-test: ${failed} FAILED`
        : "\ncheck-available-status-parity self-test: all scenarios passed",
);
process.exit(failed ? 1 : 0);
