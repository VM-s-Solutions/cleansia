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

const run = (root) =>
    spawnSync(process.execPath, [TOOL, `--root=${root}`, "--baseline"], { encoding: "utf8" });

let failed = 0;
function scenario(name, { mutate, expectExit, expectText }) {
    const root = freshRoot();
    try {
        if (mutate) mutate(root);
        const r = run(root);
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

// 0 — the baseline is honest about TODAY's tree: no NEW divergence, and the four ticketed ones are
//     reported, not hidden.
scenario("unmutated tree is clean under --baseline, and prints the known divergences", {
    expectExit: 0,
    expectText: ["4 known divergence(s)", "8/8 surfaces read"],
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

// 5 — the baseline self-invalidates when the gap it records is CLOSED, so it cannot outlive T-0530.
scenario("fixing a baselined surface goes RED until its BASELINE entry is deleted", {
    mutate: (r) =>
        patch(
            r,
            "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders/orders.facade.ts",
            "        OrderStatus.New,\n        OrderStatus.Pending,\n        OrderStatus.Confirmed,",
            "        OrderStatus.New,\n        OrderStatus.Confirmed,",
        ),
    expectExit: 1,
    expectText: ["BASELINE STALE", "[web.query.available]"],
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
