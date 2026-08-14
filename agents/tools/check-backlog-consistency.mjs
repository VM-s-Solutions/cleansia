#!/usr/bin/env node
/**
 * BACKLOG CONSISTENCY — does INDEX.md still agree with itself and with the ticket files?
 *
 * WHY THIS EXISTS. Three lanes in a row were dispatched to implement work that was already in the
 * tree — two Android tickets, six frontend tickets, sixteen backend tickets. Twenty-four tickets,
 * three wasted batches, and in the backend case the brief called one of them "the most serious row
 * here" when its subject had not existed for weeks. Nobody wrote a false row: every one was true when
 * filed and was falsified by the work landing somewhere the row could not see.
 *
 * THE STRUCTURAL CAUSE, found by the backend lane. `INDEX.md` carries **two rows per ticket** — the
 * original *filing* row in a follow-ups table, and the *close-out* row in the wave table — and their
 * statuses are independent strings. Shipping a wave updates the close-out row and leaves the filing
 * row saying `draft`. A reader who greps for "not done" finds the filing row and believes it.
 *
 * So this checks the two things a human cannot hold in their head across a 2500-line file:
 *
 *   C1  SELF-AGREEMENT — a ticket with more than one row in INDEX.md must not have one row saying
 *       done and another saying draft/ready/blocked. This is the defect that burned the three lanes.
 *   C2  FILE AGREEMENT — a ticket whose `agents/archive/2026-08/backlog/tickets/T-*.md` says `status: done` must not
 *       have an INDEX row claiming otherwise.
 *
 * WHAT IT DELIBERATELY DOES NOT CHECK. Whether a `done` row is *true* — that needs the tree, and the
 * tree is what the lanes read. This tool catches the cheap half: the file disagreeing with itself.
 * A row can still be wrong in a way no parser can see, which is why a lane's first move stays
 * "verify the defect exists" and not "read the row".
 *
 * ANTI-VACUITY. A run that parses no rows, or finds no ticket files, is RED — a checker reporting
 * zero divergences while blind is the failure it exists to close (`enforcement.md` §"reach failure").
 */

import { readFileSync, readdirSync, existsSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const REPO = join(dirname(fileURLToPath(import.meta.url)), "..", "..");

// The live backlog. The 428-file predecessor was archived on 2026-08-13 and deleted on 2026-08-14;
// this reads whatever is being filed NOW. The path is built from segments, which is why the earlier
// archive move did not rewrite it the way it rewrote prose — worth remembering before the next move.
const BACKLOG = join(REPO, "agents", "backlog");
const INDEX = join(BACKLOG, "INDEX.md");
const TICKETS = join(BACKLOG, "tickets");

const warn = process.argv.includes("--warn");
const verbose = process.argv.includes("--verbose");

// ANTI-VACUITY, and the distinction that makes it honest: an EMPTY backlog is a legitimate state (it
// is the state right now), while a backlog with rows that the reader cannot parse is a broken reader.
// A fixed floor cannot tell those apart, so the check is a CONSISTENCY one instead — rows and ticket
// files must both be present or both absent. Nothing to report is only ever reported as a pass when
// there is genuinely nothing.

const DONE = /\b(done|shipped|closed)\b|✅/i;
const OPEN = /\b(draft|ready|blocked|in_progress|in_review|proposed|qa)\b/i;

const findings = [];
const add = (level, msg) => findings.push({ level, msg });

// ── read INDEX rows ─────────────────────────────────────────────────────────
if (!existsSync(INDEX)) {
    console.error("backlog-consistency REACH: INDEX.md not found");
    process.exit(1);
}
const lines = readFileSync(INDEX, "utf8").split(/\r?\n/);

/** id -> [{ line, status }] */
const rows = new Map();
lines.forEach((raw, i) => {
    // Rows appear both at column 0 and inside blockquotes (`> | **T-0240** | …`).
    const m = /^>?\s*\|\s*\*\*(T-\d+)\*\*\s*\|/.exec(raw);
    if (!m) return;
    const cells = raw.split("|");
    // The status column is not at a fixed index — the file carries several table shapes. Take the
    // whole row as the haystack and let the verdict come from which vocabulary appears, which is
    // what a reader does anyway.
    const list = rows.get(m[1]) ?? [];
    list.push({ line: i + 1, text: raw, cells: cells.length });
    rows.set(m[1], list);
});

const rowCount = [...rows.values()].reduce((n, l) => n + l.length, 0);

/** A row's verdict: done wins only when the DONE marker is in the status half, not the prose. */
const verdictOf = (text) => {
    // Status lives before the long description in every shape used here; cap the window so a
    // description mentioning "done" elsewhere cannot flip a row.
    const head = text.slice(0, 400);
    const done = DONE.test(head);
    const open = OPEN.test(head);
    if (done && !open) return "done";
    if (done && open) return "mixed";
    if (open) return "open";
    return "unknown";
};

// ── C1 — self-agreement ─────────────────────────────────────────────────────
let c1 = 0;
for (const [id, list] of rows) {
    if (list.length < 2) continue;
    const verdicts = list.map((r) => ({ ...r, v: verdictOf(r.text) }));
    const done = verdicts.filter((r) => r.v === "done");
    const open = verdicts.filter((r) => r.v === "open");
    if (done.length && open.length) {
        c1++;
        add(
            "P1",
            `C1 ${id}: row ${done[0].line} reads DONE while row ${open[0].line} still reads open — ` +
                `a lane greping for open work finds the stale one and re-implements shipped code`,
        );
    }
}

// ── C2 — file agreement ─────────────────────────────────────────────────────
let ticketFiles = 0;
const fileStatus = new Map();
if (existsSync(TICKETS)) {
    for (const name of readdirSync(TICKETS)) {
        const m = /^(T-\d+)/.exec(name);
        if (!m || !name.endsWith(".md")) continue;
        ticketFiles++;
        const txt = readFileSync(join(TICKETS, name), "utf8");
        const sm = /^status:\s*(\S+)/im.exec(txt);
        if (sm) fileStatus.set(m[1], sm[1].toLowerCase());
    }
}
// Rows and files must agree about whether the backlog is empty. Either side alone reading zero while
// the other has content means the reader lost half the corpus — the vacuous-green failure this exists
// to close. Both at zero is simply an empty backlog and passes.
if (rowCount === 0 && ticketFiles > 0) {
    add("P0", `REACH: parsed 0 ticket row(s) from INDEX.md while ${ticketFiles} ticket file(s) exist under ${TICKETS} — the INDEX reader is broken, or every ticket is unfiled`);
}
if (ticketFiles === 0 && rowCount > 0) {
    add("P0", `REACH: found 0 ticket file(s) under ${TICKETS} while INDEX.md carries ${rowCount} ticket row(s) — the files are missing, or the ticket reader is broken`);
}

let c2 = 0;
for (const [id, status] of fileStatus) {
    if (status !== "done") continue;
    const list = rows.get(id);
    if (!list) continue;
    const stillOpen = list.filter((r) => verdictOf(r.text) === "open");
    if (stillOpen.length && !list.some((r) => verdictOf(r.text) === "done")) {
        c2++;
        add("P1", `C2 ${id}: ticket file says \`status: done\`, but INDEX row ${stillOpen[0].line} reads open and no row records the closure`);
    }
}

// ── report ──────────────────────────────────────────────────────────────────
const p0 = findings.filter((f) => f.level === "P0");
for (const f of findings) {
    if (verbose || f.level !== "P2") console.log(`  ${f.level}  ${f.msg}`);
}
console.log(
    `\nbacklog-consistency REACH: ${rows.size} ticket(s) over ${rowCount} row(s), ${ticketFiles} ticket file(s)`,
);
console.log(`backlog-consistency FAILED: C1 ${c1} · C2 ${c2} · reach ${p0.length}`);

// A reach failure is blocking even under --warn: advisory about the backlog's debt, never about
// whether the instrument ran. Same split as check-catalog-claims.mjs.
if (p0.length) process.exit(1);
process.exit(warn ? 0 : c1 + c2 ? 1 : 0);
