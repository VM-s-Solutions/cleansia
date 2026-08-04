#!/usr/bin/env node
/**
 * ADR-0037 D7 layer 2 — the cross-stack offerability parity check.
 *
 * ADR-0037 rules that "which orders may a cleaner be offered, and take" is ONE rule, owned by
 * `Cleansia.Core.Domain.Orders.OrderAvailability`, and that the three partner clients hold only a
 * DISPLAY REFINEMENT of its coarse fulfilment-axis floor (`OfferableStatuses`). The clients cannot
 * evaluate the money conjunct, so they are not expected to — but their status literals must not
 * DISAGREE with the floor, and the drift they are prone to lives across C#, TypeScript, Kotlin and
 * Swift, where no compiler and no single-stack linter can see it.
 *
 * This script parses the canonical C# rule and asserts every client literal agrees with it.
 *
 * WHY A PLAIN NODE SCRIPT OUTSIDE THE NX WORKSPACE (ADR-0037 D7 ruling 1-3, amended by CH-M7):
 *   - `frontend-ci.yml` runs `nx affected -t test`; a Kotlin/Swift/C#-only diff selects ZERO Nx
 *     projects, so a Jest spec would not run at all — and if it were selected, `nx.json` declares
 *     `@nx/jest:jest` inputs as `{projectRoot}/**\/*` with an EMPTY `sharedGlobals`, and Nx inputs
 *     cannot reference paths above the workspace root (`src/Cleansia.App`). `OrderAvailability.cs`,
 *     `OrdersListViewModel.kt` and `OrdersListLogic.swift` are therefore NOT declared inputs: the
 *     spec would replay a CACHED PASS over a drifted literal.
 *   - `backend-ci.yml` explicitly EXCLUDES `src/cleansia_android/**` and `src/cleansia_ios/**`.
 *   - `android-ci` / `ios-ci` are Gradle/Xcode and cannot read C#.
 * Living outside the Nx workspace removes the cache hazard BY CONSTRUCTION rather than by
 * configuration. It has its OWN repo-root workflow (`.github/workflows/offerability-parity.yml`)
 * triggering on all four trees, so it is never behind `nx affected` and never coupled to
 * `frontend-ci`'s deliberately narrow paths scope.
 *
 * IT COVERS BUTTON GATES, NOT ONLY QUERY LITERALS (ADR-0037 D7 ruling 4 / CH-X5). The query decides
 * what is LISTED, the button decides what is CLICKABLE, and the ADR's whole thesis is that those must
 * not diverge. A query-only check would have gone green over `order-details.helpers.ts` while the web
 * detail page hid Take for the entire `New` + Cash pipeline.
 *
 * ANTI-FALSE-GREEN: every surface is anchored. If an anchor is gone (file moved, function renamed,
 * literal restructured) or a surface yields ZERO status tokens, that is a HARD FAILURE (`P0`), never
 * a silent pass. A green run means the tool READ all ten files and compared real sets.
 *
 * Usage:
 *   node agents/tools/check-available-status-parity.mjs              # strict: any divergence -> exit 1
 *   node agents/tools/check-available-status-parity.mjs --baseline   # ...except the enumerated,
 *                                                                    #    ticketed, exact-match baseline
 *   node agents/tools/check-available-status-parity.mjs --warn       # report, always exit 0
 *   node agents/tools/check-available-status-parity.mjs --root=DIR   # run against another tree
 *                                                                    #    (used by the self-test)
 */
import { readFileSync, existsSync } from "node:fs";
import { join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const args = process.argv.slice(2);
const warnOnly = args.includes("--warn");
const useBaseline = args.includes("--baseline");
const rootArg = (args.find((a) => a.startsWith("--root=")) || "").split("=")[1];
const REPO = rootArg
    ? resolve(rootArg)
    : resolve(join(fileURLToPath(import.meta.url), "..", "..", "..")); // agents/tools -> repo root

// ─────────────────────────────────────────────────────────────────────────────
// The KNOWN, TICKETED baseline.
//
// Read this before adding to it. It is NOT a suppression list: each entry pins the EXACT divergent
// set, so it fails the moment reality moves in EITHER direction —
//   - the surface drifts further         -> the recorded set no longer matches -> RED
//   - the surface is FIXED               -> the recorded set no longer matches -> RED ("stale entry")
// so a baselined gap can only be closed deliberately, and the entry must be deleted in the same
// change that closes it. Nothing here is ever printed as "OK": the summary always states the count.
//
// It is EMPTY, and that is the finished state. Its four original entries were ADR-0037 D4 rows 5, 9,
// 10 and 11 — the partner-web half of T-0530 — and they were deleted in the change that fixed those
// four surfaces, which is the only way an exact-match entry can ever be retired. All eight surfaces
// are now gated strictly.
// ─────────────────────────────────────────────────────────────────────────────
const BASELINE = {};

// ── plumbing ────────────────────────────────────────────────────────────────
const findings = []; // hard: sets the exit code
const known = []; // baselined: printed, does not set the exit code

const rel = (f) => relative(REPO, f).split(sep).join("/");
const add = (file, line, rule, msg) =>
    findings.push(`${rel(file)}:${line}  ${rule}  ${msg}`);

function read(file) {
    if (!existsSync(file)) return null;
    return readFileSync(file, "utf8").split(/\r?\n/);
}

/** Strip `//` line comments so brace/token scanning is not fooled by prose. */
const decomment = (l) => l.replace(/\/\/.*$/, "").replace(/\/\*.*?\*\//g, "");

const fmt = (ords, names) =>
    `{${[...ords].sort((a, b) => a - b).map((o) => `${names.get(o) ?? "?"}(${o})`).join(", ")}}`;

const eq = (a, b) => a.size === b.size && [...a].every((x) => b.has(x));

/**
 * Slice the block opened by the `{` on `startIdx`, using brace depth.
 * Returns [startIdx, endIdxInclusive]. Hard-fails upward by returning null if it never closes.
 */
function blockRange(lines, startIdx) {
    let depth = 0;
    let seen = false;
    for (let i = startIdx; i < lines.length; i++) {
        const l = decomment(lines[i]);
        for (const ch of l) {
            if (ch === "{") {
                depth++;
                seen = true;
            } else if (ch === "}") depth--;
        }
        if (seen && depth <= 0) return [startIdx, i];
    }
    return null;
}

/**
 * Split a `when`/`switch` body into { labels[], body } branches.
 * `labelRe` must capture the whole label list in group 1.
 */
function branches(lines, from, to, labelRe) {
    const out = [];
    let cur = null;
    for (let i = from; i <= to; i++) {
        const m = labelRe.exec(decomment(lines[i]));
        if (m) {
            if (cur) out.push(cur);
            cur = { labels: m[1], body: "", line: i + 1 };
        } else if (cur) cur.body += `\n${decomment(lines[i])}`;
    }
    if (cur) out.push(cur);
    return out;
}

// ── the canonical rule (C#) ─────────────────────────────────────────────────
const CANON_ENUM = join(REPO, "src/Cleansia.Core.Domain/Enums/OrderStatus.cs");
const CANON_RULE = join(REPO, "src/Cleansia.Core.Domain/Orders/OrderAvailability.cs");

function parseCanonical() {
    const enumLines = read(CANON_ENUM);
    if (!enumLines) {
        add(CANON_ENUM, 0, "P0", "canonical OrderStatus enum not found — cannot check anything");
        return null;
    }
    const byName = new Map();
    const byOrdinal = new Map();
    const dead = new Set();
    let doc = "";
    for (let i = 0; i < enumLines.length; i++) {
        const raw = enumLines[i];
        if (/^\s*\/\/\//.test(raw)) {
            doc += raw;
            continue;
        }
        const m = /^\s*([A-Z]\w*)\s*=\s*(\d+)\s*,?\s*$/.exec(raw);
        if (m) {
            const [, name, n] = m;
            byName.set(name, Number(n));
            byOrdinal.set(Number(n), name);
            if (/\bDEAD\b/.test(doc)) dead.add(Number(n));
            doc = "";
        } else if (raw.trim() !== "") doc = "";
    }
    if (byName.size < 5) {
        add(CANON_ENUM, 0, "P0", `parsed only ${byName.size} enum members — the parser is stale`);
        return null;
    }

    const ruleLines = read(CANON_RULE);
    if (!ruleLines) {
        add(CANON_RULE, 0, "P0", "OrderAvailability.cs not found — the canonical rule has moved");
        return null;
    }
    const idx = ruleLines.findIndex((l) => /\bOfferableStatuses\b\s*=/.test(l));
    if (idx < 0) {
        add(CANON_RULE, 0, "P0", "OfferableStatuses literal not found — the canonical floor has moved");
        return null;
    }
    let text = "";
    for (let i = idx; i < ruleLines.length; i++) {
        text += ruleLines[i];
        if (ruleLines[i].includes(";")) break;
    }
    const floor = new Set();
    for (const m of text.matchAll(/OrderStatus\.([A-Z]\w*)/g)) {
        if (!byName.has(m[1])) {
            add(CANON_RULE, idx + 1, "P0", `OfferableStatuses names unknown member '${m[1]}'`);
            return null;
        }
        floor.add(byName.get(m[1]));
    }
    if (floor.size === 0) {
        add(CANON_RULE, idx + 1, "P0", "OfferableStatuses parsed as EMPTY — the parser is stale");
        return null;
    }
    return { byName, byOrdinal, dead, floor, floorLine: idx + 1 };
}

// ── the ten surfaces ────────────────────────────────────────────────────────
// Each extractor returns { line, statuses:Set<number> } or a string describing why it could not.

const P = {
    webOrders: "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/orders",
    webDetail: "src/Cleansia.App/libs/cleansia-partner-features/orders/src/lib/order-details",
    android: "src/cleansia_android/partner-app/src/main/java/cz/cleansia/partner/features/orders",
    ios: "src/cleansia_ios/CleansiaPartner/Sources/Features/Orders",
};

/** web dialect: `OrderStatus.Confirmed` -> ordinal via the canonical map. */
const webTokens = (text, canon, onUnknown) => {
    const out = new Set();
    for (const m of text.matchAll(/OrderStatus\.([A-Z]\w*)/g)) {
        if (!canon.byName.has(m[1])) onUnknown(m[1]);
        else out.add(canon.byName.get(m[1]));
    }
    return out;
};
/** android dialect: generated `OrderStatus._2` -> 2. */
const kotlinTokens = (text) =>
    new Set([...text.matchAll(/OrderStatus\._(\d+)/g)].map((m) => Number(m[1])));
/** ios dialect: generated `._2` -> 2. */
const swiftTokens = (text) =>
    new Set([...text.matchAll(/(?:^|[^\w.])\._(\d+)\b/g)].map((m) => Number(m[1])));

const SURFACES = [
    {
        id: "web.query.available",
        adr: "D4 row 5",
        kind: "equals-floor",
        what: "partner-web Available list QUERY literal",
        file: `${P.webOrders}/orders.facade.ts`,
        extract(lines, canon, bad) {
            const fn = lines.findIndex((l) => /loadAvailableOrders\s*\(/.test(l));
            if (fn < 0) return "anchor `loadAvailableOrders(` not found";
            const k = lines.findIndex((l, i) => i > fn && /orderStatuses\s*:/.test(l));
            if (k < 0 || k > fn + 40) return "`orderStatuses:` not found inside loadAvailableOrders";
            let text = "";
            for (let i = k; i < lines.length && i < k + 15; i++) {
                text += `\n${decomment(lines[i])}`;
                if (/\]/.test(lines[i])) break;
            }
            return { line: k + 1, statuses: webTokens(text, canon, bad) };
        },
    },
    {
        id: "web.button.row-action",
        adr: "D4 row 9",
        kind: "equals-floor",
        what: "partner-web Available row-action TAKE BUTTON gate",
        file: `${P.webOrders}/orders.models.ts`,
        extract(lines, canon, bad) {
            const a = lines.findIndex((l) => /tooltip:\s*'pages\.orders\.take_order'/.test(l));
            if (a < 0) return "anchor `tooltip: 'pages.orders.take_order'` not found";
            const v = lines.findIndex((l, i) => i > a && /\bvisible\s*:/.test(l));
            if (v < 0 || v > a + 15) return "no `visible:` gate beside the take_order action";
            const r = blockRange(lines, v);
            if (!r) return "`visible:` gate block never closes";
            const text = lines.slice(r[0], r[1] + 1).map(decomment).join("\n");
            return { line: v + 1, statuses: webTokens(text, canon, bad) };
        },
    },
    {
        id: "web.button.detail",
        adr: "D4 row 10",
        kind: "equals-floor",
        what: "partner-web order-detail TAKE BUTTON gate (canTakeOrder)",
        file: `${P.webDetail}/order-details.helpers.ts`,
        extract(lines, canon, bad) {
            const a = lines.findIndex((l) => /export\s+function\s+canTakeOrder\s*\(/.test(l));
            if (a < 0) return "anchor `export function canTakeOrder(` not found";
            const open = lines.findIndex((l, i) => i >= a && /\{\s*$/.test(decomment(l)));
            const r = open < 0 ? null : blockRange(lines, open);
            if (!r) return "canTakeOrder body never closes";
            const text = lines.slice(r[0], r[1] + 1).map(decomment).join("\n");
            return { line: a + 1, statuses: webTokens(text, canon, bad) };
        },
    },
    {
        id: "web.filter.vocabulary",
        adr: "D4 row 11",
        kind: "vocabulary",
        what: "partner-web status FILTER dropdown vocabulary",
        file: `${P.webOrders}/orders.helpers.ts`,
        extract(lines, canon, bad) {
            const a = lines.findIndex((l) =>
                /export\s+function\s+buildOrderStatusOptions\s*\(/.test(l),
            );
            if (a < 0) return "anchor `export function buildOrderStatusOptions(` not found";
            const open = lines.findIndex((l, i) => i >= a && /\{\s*$/.test(decomment(l)));
            const r = open < 0 ? null : blockRange(lines, open);
            if (!r) return "buildOrderStatusOptions body never closes";
            const text = lines
                .slice(r[0], r[1] + 1)
                .map(decomment)
                .filter((l) => /value\s*:/.test(l))
                .join("\n");
            return { line: a + 1, statuses: webTokens(text, canon, bad) };
        },
    },
    {
        id: "android.query.available",
        adr: "D4 row 6",
        kind: "equals-floor",
        what: "Android partner Available tab QUERY literal",
        file: `${P.android}/OrdersListViewModel.kt`,
        extract(lines) {
            const a = lines.findIndex((l) => /OrdersTab\.Available\s*->/.test(l));
            if (a < 0) return "anchor `OrdersTab.Available ->` not found";
            const k = lines.findIndex((l, i) => i >= a && /listOf\s*\(/.test(l));
            if (k < 0 || k > a + 8) return "no `listOf(` beside OrdersTab.Available";
            let text = "";
            for (let i = k; i < lines.length && i < k + 10; i++) {
                text += `\n${decomment(lines[i])}`;
                if (/\)/.test(decomment(lines[i]))) break;
            }
            return { line: k + 1, statuses: kotlinTokens(text) };
        },
    },
    {
        id: "android.button.take",
        adr: "D7 ruling 4",
        kind: "equals-floor",
        what: "Android partner detail TAKE BUTTON gate (OrderPrimaryAction)",
        file: `${P.android}/OrderPrimaryAction.kt`,
        extract(lines) {
            const a = lines.findIndex((l) => /\bwhen\s*\(\s*status\s*\)\s*\{/.test(l));
            if (a < 0) return "anchor `when (status) {` not found";
            const r = blockRange(lines, a);
            if (!r) return "`when (status)` block never closes";
            const br = branches(
                lines,
                r[0] + 1,
                r[1],
                /^\s*(OrderStatus\._\d+(?:\s*,\s*OrderStatus\._\d+)*)\s*->/,
            );
            if (!br.length) return "no `OrderStatus._N ->` branches inside `when (status)`";
            const take = br.filter((b) => /\bonTake\b/.test(b.body));
            if (!take.length) return "no branch of `when (status)` dispatches onTake";
            const statuses = new Set();
            let line = take[0].line;
            for (const b of take) for (const o of kotlinTokens(b.labels)) statuses.add(o);
            return { line, statuses };
        },
    },
    {
        id: "ios.query.available",
        adr: "D4 row 7",
        kind: "equals-floor",
        what: "iOS partner Available tab QUERY literal",
        file: `${P.ios}/OrdersListLogic.swift`,
        extract(lines) {
            // Anchor on the query builder's own `switch tab` — the file also has a `case
            // .available: .available` mapping near the top, which is NOT the query literal.
            const sw = lines.findIndex((l) => /\bswitch\s+tab\s*\{/.test(l));
            if (sw < 0) return "anchor `switch tab {` (OrdersQueryBuilder) not found";
            const a = lines.findIndex((l, i) => i > sw && /^\s*case\s+\.available\s*:\s*$/.test(l));
            if (a < 0) return "no `case .available:` branch inside `switch tab`";
            const k = lines.findIndex((l, i) => i >= a && /statuses\s*:\s*\[/.test(l));
            if (k < 0 || k > a + 10) return "no `statuses: [` inside `case .available`";
            return { line: k + 1, statuses: swiftTokens(decomment(lines[k])) };
        },
    },
    {
        id: "ios.button.take",
        adr: "D7 ruling 4",
        kind: "equals-floor",
        what: "iOS partner detail TAKE BUTTON gate (OrderPrimaryAction)",
        file: `${P.ios}/OrderPrimaryAction.swift`,
        extract(lines) {
            const a = lines.findIndex((l) => /\bswitch\s+status\s*\{/.test(l));
            if (a < 0) return "anchor `switch status {` not found";
            const r = blockRange(lines, a);
            if (!r) return "`switch status` block never closes";
            const br = branches(
                lines,
                r[0] + 1,
                r[1],
                /^\s*case\s+((?:\.\w+)(?:\s*,\s*\.\w+)*)\s*:/,
            );
            if (!br.length) return "no `case .X:` branches inside `switch status`";
            const take = br.filter((b) => /\.take\b/.test(b.body));
            if (!take.length) return "no branch of `switch status` returns .take";
            const statuses = new Set();
            for (const b of take) for (const o of swiftTokens(b.labels)) statuses.add(o);
            return { line: take[0].line, statuses };
        },
    },
];

// ── run ─────────────────────────────────────────────────────────────────────
const canon = parseCanonical();
let checked = 0;

if (canon) {
    const names = canon.byOrdinal;
    console.log(
        `canonical: OrderAvailability.OfferableStatuses = ${fmt(canon.floor, names)}  ` +
            `(${rel(CANON_RULE)}:${canon.floorLine}; dead: ${fmt(canon.dead, names)})`,
    );

    for (const s of SURFACES) {
        const abs = join(REPO, s.file);
        const lines = read(abs);
        if (!lines) {
            add(abs, 0, "P0", `[${s.id}] surface file missing — ${s.what} (ADR-0037 ${s.adr})`);
            continue;
        }
        let unknown = null;
        const got = s.extract(lines, canon, (n) => (unknown = n));
        if (typeof got === "string") {
            add(abs, 0, "P0", `[${s.id}] ${got} — the extractor is stale, NOT a pass`);
            continue;
        }
        if (unknown) {
            add(abs, got.line, "P0", `[${s.id}] references unknown OrderStatus.${unknown}`);
            continue;
        }
        if (got.statuses.size === 0) {
            add(abs, got.line, "P0", `[${s.id}] anchor found but ZERO statuses parsed — stale extractor`);
            continue;
        }
        checked++;

        // What "agreeing with the floor" means for this surface.
        let bad = null;
        if (s.kind === "equals-floor") {
            if (!eq(got.statuses, canon.floor)) {
                const extra = [...got.statuses].filter((o) => !canon.floor.has(o));
                const missing = [...canon.floor].filter((o) => !got.statuses.has(o));
                bad =
                    `${s.what} is ${fmt(got.statuses, names)}, canonical floor is ` +
                    `${fmt(canon.floor, names)}` +
                    (extra.length ? ` | EXTRA ${fmt(extra, names)}` : "") +
                    (missing.length ? ` | MISSING ${fmt(missing, names)}` : "");
            }
        } else {
            // vocabulary: must be able to NAME every offerable status, must never offer a dead one.
            const missing = [...canon.floor].filter((o) => !got.statuses.has(o));
            const deadOffered = [...got.statuses].filter((o) => canon.dead.has(o));
            if (missing.length || deadOffered.length) {
                bad =
                    `${s.what} is ${fmt(got.statuses, names)}` +
                    (missing.length ? ` | cannot name offerable ${fmt(missing, names)}` : "") +
                    (deadOffered.length ? ` | offers DEAD ${fmt(deadOffered, names)}` : "");
            }
        }

        const base = useBaseline ? BASELINE[s.id] : undefined;
        if (base) {
            const recorded = new Set(base.statuses);
            if (!eq(got.statuses, recorded)) {
                add(
                    abs,
                    got.line,
                    "P2",
                    `[${s.id}] BASELINE STALE — recorded ${fmt(recorded, names)}, found ` +
                        `${fmt(got.statuses, names)}. If you fixed it, delete the '${s.id}' entry ` +
                        `from BASELINE in ${rel(join(REPO, "agents/tools/check-available-status-parity.mjs"))}.`,
                );
            } else if (bad) {
                known.push(
                    `${rel(abs)}:${got.line}  ${base.ticket}  [${s.id}] ${bad}  (ADR-0037 ${s.adr})`,
                );
            } else {
                add(
                    abs,
                    got.line,
                    "P2",
                    `[${s.id}] BASELINE STALE — this surface now AGREES with the floor; delete its ` +
                        `BASELINE entry.`,
                );
            }
        } else if (bad) {
            add(abs, got.line, "P1", `[${s.id}] ${bad}  (ADR-0037 ${s.adr})`);
        }
    }
}

if (known.length) {
    console.log(`\nKNOWN divergences (baselined, ticketed — NOT a pass):`);
    for (const k of known) console.log(`  ${k}`);
    for (const [id, b] of Object.entries(BASELINE)) {
        if (known.some((k) => k.includes(`[${id}]`))) console.log(`    ^ ${b.ticket}: ${b.why}`);
    }
}
if (findings.length) {
    console.log(`\nofferability-parity violations:`);
    for (const f of findings) console.log(`  ${f}`);
}

const total = SURFACES.length;
console.log(
    `\nofferability-parity: ${checked}/${total} surfaces read, ` +
        `${findings.length} violation(s), ${known.length} known divergence(s)` +
        (useBaseline ? " [--baseline]" : "") +
        (warnOnly ? " [--warn: exit 0]" : ""),
);
if (checked !== total && !findings.length) {
    console.log("offerability-parity: NOT RUN in full — see P0 findings above.");
}
process.exit(!warnOnly && findings.length ? 1 : 0);
