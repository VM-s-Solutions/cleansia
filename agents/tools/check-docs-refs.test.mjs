#!/usr/bin/env node
// Self-test for check-docs-refs.
//
// The gate exists to stop a docs pointer rotting into a dead link. A gate that cannot fail is worse
// than none — it reports "0 unresolved" forever and everyone believes it — so every "does NOT flag"
// case below is paired with one proving the same rule still FIRES.

import { execFileSync } from "child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "fs";
import { join, relative, sep } from "path";
import { fileURLToPath } from "url";
import assert from "assert";

const REPO = join(fileURLToPath(import.meta.url), "..", "..", "..");
const TOOL = join(REPO, "agents", "tools", "check-docs-refs.mjs");

// Build a throwaway docs tree + source tree, run the tool over them, return { code, out }.
function run({ pages = {}, sources = {} }) {
    const root = mkdtempSync(join(REPO, ".docsref-fixture-"));
    try {
        const docsDir = join(root, "docs");
        const srcDir = join(root, "src");
        for (const [path, body] of Object.entries(pages)) {
            const full = join(docsDir, path);
            mkdirSync(join(full, ".."), { recursive: true });
            writeFileSync(full, body, "utf8");
        }
        for (const [path, body] of Object.entries(sources)) {
            const full = join(srcDir, path);
            mkdirSync(join(full, ".."), { recursive: true });
            writeFileSync(full, body, "utf8");
        }
        const rel = relative(REPO, root).split(sep).join("/");
        let code = 0;
        let out = "";
        try {
            out = execFileSync(
                process.execPath,
                [TOOL, `--docs=${rel}/docs`, `--paths=${rel}/src`],
                { encoding: "utf8", cwd: REPO },
            );
        } catch (e) {
            code = e.status ?? 1;
            out = (e.stdout ?? "") + (e.stderr ?? "");
        }
        return { code, out };
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

const cases = [];
const test = (name, fn) => cases.push([name, fn]);

test("resolves a reference to a page file", () => {
    const r = run({
        pages: { "architecture/orchestration.md": "# Orchestration\n" },
        sources: {
            "Program.cs": "// Ports pinned. → /architecture/orchestration\n",
        },
    });
    assert.equal(r.code, 0, r.out);
    assert.match(r.out, /1 reference\(s\), 0 unresolved/);
});

test("resolves a reference to a section index", () => {
    const r = run({
        pages: { "flows/index.md": "# Flows\n" },
        sources: { "Handler.cs": "// See the map. → /flows/\n" },
    });
    assert.equal(r.code, 0, r.out);
});

test("FAILS on a reference to a page that does not exist", () => {
    const r = run({
        pages: { "flows/index.md": "# Flows\n" },
        sources: { "Handler.cs": "// → /flows/nope\n" },
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /no such page/);
});

test("resolves an anchor that matches a heading", () => {
    const r = run({
        pages: {
            "architecture/orchestration.md":
                "# Orchestration\n\n## Azurite ports\n\ntext\n",
        },
        sources: {
            "Program.cs": "// → /architecture/orchestration#azurite-ports\n",
        },
    });
    assert.equal(r.code, 0, r.out);
});

test("FAILS on an anchor no heading produces", () => {
    const r = run({
        pages: {
            "architecture/orchestration.md": "# Orchestration\n\n## Queue ports\n",
        },
        sources: {
            "Program.cs": "// → /architecture/orchestration#azurite-ports\n",
        },
    });
    assert.equal(r.code, 1, r.out);
    assert.match(r.out, /no heading with that anchor/);
});

test("honours an explicit {#custom-anchor}", () => {
    const r = run({
        pages: {
            "domain/index.md": "# Domain\n\n## Seats and duration {#seats}\n",
        },
        sources: { "Order.cs": "// → /domain/#seats\n" },
    });
    assert.equal(r.code, 0, r.out);
});

test("ignores a path in code that is not a comment", () => {
    const r = run({
        pages: {},
        sources: {
            "Route.ts": "const route = { path: '/flows/nope' };\n",
        },
    });
    assert.equal(r.code, 0, r.out);
    assert.match(r.out, /0 reference\(s\)/);
});

test("ignores a bare path with no arrow — the arrow IS the convention", () => {
    const r = run({
        pages: {},
        sources: { "Handler.cs": "// the /flows/nope endpoint\n" },
    });
    assert.equal(r.code, 0, r.out);
    assert.match(r.out, /0 reference\(s\)/);
});

test("accepts the ascii -> form as well as the arrow", () => {
    const r = run({
        pages: { "flows/index.md": "# Flows\n" },
        sources: { "Handler.kt": "// see -> /flows/\n" },
    });
    assert.equal(r.code, 0, r.out);
    assert.match(r.out, /1 reference\(s\)/);
});

test("skips generated clients", () => {
    const r = run({
        pages: {},
        sources: { "client/admin-client.ts": "// → /flows/nope\n" },
    });
    assert.equal(r.code, 0, r.out);
    assert.match(r.out, /0 reference\(s\)/);
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
        ? `\ncheck-docs-refs: ${cases.length} passed`
        : `\ncheck-docs-refs: ${failed}/${cases.length} FAILED`,
);
process.exit(failed === 0 ? 0 : 1);
