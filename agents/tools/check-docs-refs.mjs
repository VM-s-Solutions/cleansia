#!/usr/bin/env node
// check-docs-refs — every docs pointer in the code resolves to a page that exists.
//
// The cleanup moves documentation out of the code and into docs/. What stays behind in a source file
// is a one-line WARNING plus a pointer to the page carrying the explanation:
//
//     // Ports pinned — random ports split producer/consumer across Azurite. → /architecture/local-orchestration#azurite-ports
//
// A pointer nobody checks is worse than no pointer: it sends a reader somewhere that used to exist,
// which is exactly the decay this repository keeps re-learning. This tool is the check.
//
// It verifies two things per reference:
//   1. the page resolves — docs/<path>.md or docs/<path>/index.md
//   2. the #anchor, if present, matches a heading on that page
//
// Usage:
//   node agents/tools/check-docs-refs.mjs                 # scan the default source roots
//   node agents/tools/check-docs-refs.mjs --paths=a,b      # scan specific roots (used by the self-test)
//   node agents/tools/check-docs-refs.mjs --docs=docs      # override the docs root
//
// Exit 1 on any unresolved reference.

import { existsSync, readdirSync, readFileSync, statSync } from "fs";
import { join, relative, sep } from "path";

const args = process.argv.slice(2);
const argValue = (name, fallback) => {
    const hit = args.find((a) => a.startsWith(`--${name}=`));
    return hit ? hit.slice(name.length + 3) : fallback;
};

const REPO = process.cwd();
const DOCS_ROOT = join(REPO, argValue("docs", "docs"));
const SOURCE_ROOTS = argValue("paths", "src")
    .split(",")
    .map((p) => join(REPO, p.trim()))
    .filter(Boolean);

const SOURCE_EXT = [".cs", ".ts", ".kt", ".swift", ".mjs", ".js"];
const SKIP_DIR = new Set([
    "node_modules",
    "bin",
    "obj",
    "dist",
    ".vitepress",
    ".git",
    "client", // NSwag-generated clients are never hand-edited
]);

// `→ /path` or `-> /path`, optionally with an #anchor. The arrow is the marker: a bare `/foo` in a
// comment is far too common to treat as a docs reference, and requiring the arrow keeps the
// convention greppable in both directions.
const REF_RE = /(?:→|->)\s*(\/[a-z0-9][a-z0-9\-/]*)(#[a-z0-9\-]+)?/gi;

function walk(dir, out = []) {
    if (!existsSync(dir)) return out;
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        if (entry.isDirectory()) {
            if (SKIP_DIR.has(entry.name)) continue;
            walk(join(dir, entry.name), out);
            continue;
        }
        if (SOURCE_EXT.some((e) => entry.name.endsWith(e))) out.push(join(dir, entry.name));
    }
    return out;
}

// VitePress resolves /a/b to a/b.md or a/b/index.md; /a/ to a/index.md.
function resolvePage(refPath) {
    const clean = refPath.replace(/\/$/, "");
    const candidates = [
        join(DOCS_ROOT, `${clean}.md`),
        join(DOCS_ROOT, clean, "index.md"),
    ];
    return candidates.find((c) => existsSync(c) && statSync(c).isFile()) ?? null;
}

// GitHub/VitePress heading slug: lowercase, strip anything not a word char/space/hyphen, spaces to
// hyphens. Good enough for the headings this project writes; a miss reports rather than passes.
function headingAnchors(file) {
    const anchors = new Set();
    for (const line of readFileSync(file, "utf8").split(/\r?\n/)) {
        const m = line.match(/^#{1,6}\s+(.*)$/);
        if (!m) continue;
        const explicit = m[1].match(/\{#([a-z0-9-]+)\}/i);
        if (explicit) {
            anchors.add(explicit[1].toLowerCase());
            continue;
        }
        anchors.add(
            m[1]
                .replace(/`/g, "")
                .replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
                .trim()
                .toLowerCase()
                .replace(/[^\w\s-]/g, "")
                .replace(/\s+/g, "-"),
        );
    }
    return anchors;
}

const problems = [];
let scanned = 0;
let refs = 0;

for (const root of SOURCE_ROOTS) {
    for (const file of walk(root)) {
        scanned++;
        const lines = readFileSync(file, "utf8").split(/\r?\n/);
        lines.forEach((line, i) => {
            // Only comment lines carry docs pointers; a string literal that happens to contain an
            // arrow and a path is not a reference.
            if (!/^\s*(\/\/|\*|#|<!--)/.test(line)) return;
            let m;
            REF_RE.lastIndex = 0;
            while ((m = REF_RE.exec(line)) !== null) {
                refs++;
                const [, refPath, anchor] = m;
                const page = resolvePage(refPath);
                const where = `${relative(REPO, file).split(sep).join("/")}:${i + 1}`;
                if (!page) {
                    problems.push(`${where}  ${refPath}  — no such page`);
                    continue;
                }
                if (anchor) {
                    const slug = anchor.slice(1).toLowerCase();
                    if (!headingAnchors(page).has(slug))
                        problems.push(
                            `${where}  ${refPath}${anchor}  — page exists, no heading with that anchor`,
                        );
                }
            }
        });
    }
}

for (const p of problems) console.log(`  ${p}`);
console.log(
    `\ndocs-refs: ${scanned} source file(s), ${refs} reference(s), ${problems.length} unresolved`,
);
process.exit(problems.length === 0 ? 0 : 1);
