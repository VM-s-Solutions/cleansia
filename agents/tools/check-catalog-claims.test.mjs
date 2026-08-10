#!/usr/bin/env node
/**
 * Self-test for T-0574 (`check-catalog-claims.mjs`).
 *
 * `enforcement.md` §"Anti-vacuity" states this guard's acceptance test behaviourally: *"mutate one
 * banner / one citation, assert red"*, and *"a run that finds zero citations because a glob broke
 * must be red"*. This file runs both on every CI run, so the checker cannot decay into scaffolding —
 * the failure mode the parity guard's own self-test exists to prevent.
 *
 * It never touches the working tree. Each scenario builds a throwaway mini-repo — a corpus page, an
 * ADR with a real `- **Status:**` line, and a source file of a known length — mutates ONE thing, and
 * asserts the exit code and the named rule.
 *
 * THE ANTI-VACUITY SCENARIOS ARE THE POINT, not decoration. A checker that matched nothing would pass
 * every "assert red" case by accident only if the fixtures were also empty, so three scenarios assert
 * the tool COUNTS what it found (a green run states its corpus), one asserts a citation-shaped token
 * the parser cannot consume is a hard P0 rather than a clean file, and one asserts the reach floors
 * fire on an under-populated root when `--floors=off` is not passed.
 *
 *   node agents/tools/check-catalog-claims.test.mjs
 */
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, join } from "node:path";
import { tmpdir } from "node:os";
import { fileURLToPath } from "node:url";

const HERE = dirname(fileURLToPath(import.meta.url));
const TOOL = join(HERE, "check-catalog-claims.mjs");

/** Exactly 20 lines. Cited at :5-9 and :12 by the good fixture; :30 is past its end. */
const THING_CS = Array.from({ length: 20 }, (_, i) =>
    i === 4 ? "public sealed class FixtureThing" : i === 11 ? "    public int Compute() => 1;" : `// line ${i + 1}`,
).join("\n");

const ADR = `# ADR-0099 — the fixture decision

- **Status:** accepted   <!-- proposed | accepted | superseded | rejected -->
- **Date:** 2026-08-10

## Decision

It is decided.
`;

const GOOD_CARD = `# Role — \`FixtureThing\` (CRC card)

> **SHIPPED.** **ADR-0099 is \`accepted\`** (\`agents/backlog/adr/0099-fixture-decision.md:3\`).
> **Retires when:** ADR-0099's own status token changes.

## Invariants

1. The type is declared at \`src/Fixture/Thing.cs:5-9\`.
2. \`Compute\` is at \`Thing.cs:12\`.
`;

function makeRoot(files) {
    const root = mkdtempSync(join(tmpdir(), "catalog-claims-"));
    const base = {
        "agents/backlog/adr/0099-fixture-decision.md": ADR,
        "src/Fixture/Thing.cs": THING_CS,
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD,
        ...files,
    };
    for (const [p, content] of Object.entries(base)) {
        if (content === null) continue;
        const dst = join(root, p);
        mkdirSync(dirname(dst), { recursive: true });
        writeFileSync(dst, content);
    }
    return root;
}

let failed = 0;
function scenario(name, { files = {}, args = ["--floors=off"], expectExit, expectText = [], denyText = [] }) {
    const root = makeRoot(files);
    try {
        const r = spawnSync(process.execPath, [TOOL, `--root=${root}`, ...args], { encoding: "utf8" });
        const out = `${r.stdout}${r.stderr}`;
        const okExit = r.status === expectExit;
        const okText = expectText.every((t) => out.includes(t));
        const okDeny = denyText.every((t) => !out.includes(t));
        if (okExit && okText && okDeny) {
            console.log(`  PASS  ${name}`);
        } else {
            failed++;
            console.log(`  FAIL  ${name}`);
            console.log(`        expected exit ${expectExit}, got ${r.status}`);
            if (!okText) console.log(`        expected output to contain: ${expectText.join(" | ")}`);
            if (!okDeny) console.log(`        expected output NOT to contain: ${denyText.join(" | ")}`);
            console.log(out.split("\n").map((l) => `        > ${l}`).join("\n"));
        }
    } catch (e) {
        failed++;
        console.log(`  FAIL  ${name} — ${e.message}`);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

console.log("check-catalog-claims self-test:");

// ── 0. the known-good fixture, and the proof the checker READ it ────────────
scenario("a known-good card passes AND the summary states what it found", {
    expectExit: 0,
    expectText: [
        "C1 1 ADR status claim(s)",
        "3 citation(s)",
        "catalog-claims FAILED: C1 0 · C2 0 · C3 0",
    ],
});

// ── C1 — ADR status agreement ───────────────────────────────────────────────
scenario("C1: a card claiming `proposed` over an `accepted` ADR is RED", {
    files: {
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace(
            "**ADR-0099 is `accepted`**",
            "**ADR-0099 is `proposed`**",
        ),
    },
    expectExit: 1,
    expectText: ["C1", "claims ADR-0099 is `proposed`", "reads `accepted`"],
});

scenario("C1: the reverse form (`an accepted ADR-0099`) is checked too", {
    files: {
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace(
            "**ADR-0099 is `accepted`**",
            "built against a `superseded` ADR-0099",
        ),
    },
    expectExit: 1,
    expectText: ["C1", "claims ADR-0099 is `superseded`"],
});

scenario("C1: a status verb that is NOT a status claim does not fire", {
    files: {
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace(
            "**ADR-0099 is `accepted`**",
            "that coupling was explicitly rejected (ADR-0099 PA-5) and **ADR-0099 is `accepted`**",
        ),
    },
    expectExit: 0,
    expectText: ["C1 1 ADR status claim(s)"],
});

scenario("C1: a QUOTED bad example is skipped, and the skip is counted", {
    files: {
        "agents/knowledge/roles/fixture-card.md": `${GOOD_CARD}
Retires the *"PROPOSED — do not build until ADR-0099 is \`proposed\`"* banner.
`,
    },
    expectExit: 0,
    expectText: ["+1 skipped as quoted"],
});

scenario("C1: a claim about an ADR that does not exist is RED", {
    files: {
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace("ADR-0099 is `accepted`", "ADR-0098 is `accepted`"),
    },
    expectExit: 1,
    expectText: ["C1", "no ADR 0098 exists"],
});

scenario("C1: an ADR whose own Status line moves reddens the card that quoted it", {
    files: { "agents/backlog/adr/0099-fixture-decision.md": ADR.replace("accepted ", "superseded ") },
    expectExit: 1,
    expectText: ["C1", "reads `superseded`"],
});

// ── C2 — "not yet built" banners ────────────────────────────────────────────
const NOT_BUILT_NO_MARKER = `# Role — \`Allocator\` (CRC card)

> **NOT YET BUILT — no ticket is cut yet.** The decision is settled and this card is what the
> implementer builds against.
`;

scenario("C2-FORM: a not-yet-built banner with no `Retires when:` is RED", {
    files: { "agents/knowledge/roles/allocator.md": NOT_BUILT_NO_MARKER },
    expectExit: 1,
    expectText: ["C2-FORM", "carries no `Retires when:` condition"],
});

scenario("C2: the same banner WITH a retirement condition naming a missing path passes", {
    files: {
        "agents/knowledge/roles/allocator.md": `${NOT_BUILT_NO_MARKER}> **Retires when:** \`src/Fixture/Allocator.cs\` exists.
`,
    },
    expectExit: 0,
    expectText: ["C2 2 not-yet-built banner(s)", "1 name a path"],
});

scenario("C2-RETIRED: a retirement condition whose path now EXISTS is RED", {
    files: {
        "agents/knowledge/roles/allocator.md": `${NOT_BUILT_NO_MARKER}> **Retires when:** \`src/Fixture/Thing.cs\` exists.
`,
    },
    expectExit: 1,
    expectText: ["C2-RETIRED", "the banner it guards is now false"],
});

scenario("C2: a prose mention of the phrase is not a banner", {
    files: {
        "agents/knowledge/roles/allocator.md": `# Notes

The rule covers a claim that something is not yet built, in prose, unemphasised.
`,
    },
    expectExit: 0,
    expectText: ["C2 0 not-yet-built banner(s)"],
});

// ── C3 — citation resolution ────────────────────────────────────────────────
scenario("C3: a citation past the end of the file is RED", {
    files: { "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace("Thing.cs:5-9", "Thing.cs:5-30") },
    expectExit: 1,
    expectText: ["C3", "cited line 30 is past the end of the file", "(20 lines)"],
});

scenario("C3: the same citation stays green after the file GROWS", {
    files: {
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace("Thing.cs:5-9", "Thing.cs:5-30"),
        "src/Fixture/Thing.cs": `${THING_CS}\n${Array.from({ length: 20 }, (_, i) => `// extra ${i}`).join("\n")}`,
    },
    expectExit: 0,
    expectText: ["catalog-claims FAILED: C1 0 · C2 0 · C3 0"],
});

scenario("C3: a citation to a file that no longer exists is RED", {
    files: { "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace("Thing.cs:12", "Renamed.cs:12") },
    expectExit: 1,
    expectText: ["C3", "no such file in the tree"],
});

scenario("C3: deleting the CITED file reddens the citing page (the decay direction)", {
    files: { "src/Fixture/Thing.cs": null },
    expectExit: 1,
    expectText: ["C3", "no such file in the tree"],
});

scenario("C3: the `Type.Member:N-M` dialect resolves and is checked", {
    files: {
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace("`Compute` is at `Thing.cs:12`", "`Thing.Compute:99`"),
    },
    expectExit: 1,
    expectText: ["[type]", "cited line 99 is past the end"],
});

scenario("C3: a continuation's verdict is SOFT — printed, never blocking", {
    files: {
        "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace("`Compute` is at `Thing.cs:12`", "at `Thing.cs:12`, also `:99`"),
    },
    expectExit: 0,
    expectText: ["C3-SOFT", "catalog-claims FAILED: C1 0 · C2 0 · C3 0"],
});

// ── anti-vacuity ────────────────────────────────────────────────────────────
scenario("ANTI-VACUITY: citation-shaped text the parser cannot consume is a hard P0", {
    files: {
        "agents/knowledge/roles/fixture-card.md": `${GOOD_CARD}\nSee \`agents/backlog/adr/0099-fixture-…md:3-5\` for the ruling.\n`,
    },
    expectExit: 1,
    expectText: ["P0", "REACH", "NOT consumed by the citation parser", "NOT RUN in full"],
});

scenario("ANTI-VACUITY: the reach floors fire on an under-populated root", {
    args: [],
    expectExit: 1,
    expectText: ["P0", "REACH", "corpus files: 1 < floor", "this is NOT a pass"],
});

scenario("ANTI-VACUITY: an empty corpus is RED, not a silent pass", {
    args: [],
    files: { "agents/knowledge/roles/fixture-card.md": null },
    expectExit: 1,
    expectText: ["P0", "REACH", "citations found: 0"],
    denyText: ["catalog-claims FAILED: C1 0 · C2 0 · C3 0 (0 claim violation(s), 0 reach failure(s)"],
});

scenario("ANTI-VACUITY: --warn reports every claim finding and still exits 0", {
    args: ["--floors=off", "--warn"],
    files: { "agents/knowledge/roles/fixture-card.md": GOOD_CARD.replace("Thing.cs:5-9", "Thing.cs:5-30") },
    expectExit: 0,
    expectText: ["C3", "cited line 30 is past the end", "[--warn: exit 0]"],
});

// The tier applies to the catalog's baseline, never to the instrument: a run that measured nothing
// must be red even in advisory mode, or the advisory number is a fiction.
scenario("ANTI-VACUITY: --warn does NOT suppress a reach failure", {
    args: ["--warn"],
    expectExit: 1,
    expectText: ["P0", "REACH", "NOT RUN in full"],
});

console.log(
    failed
        ? `\ncheck-catalog-claims self-test: ${failed} FAILED`
        : "\ncheck-catalog-claims self-test: all scenarios passed",
);
process.exit(failed ? 1 : 0);
