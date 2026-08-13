#!/usr/bin/env node
/**
 * T-0574 — the catalog-claim liveness check.
 *
 * Rule: `agents/knowledge/conventions.md` §"A claim about the tree carries its own retirement
 * condition". Deviating forms: `agents/knowledge/consistency.md` §"Catalog claims about the tree".
 * Spec: `agents/process/enforcement.md` §"The catalog-claim liveness check".
 *
 * THE FAILURE IT CLOSES IS DECAY, NOT MIS-CITATION. Every one of the six instances measured on
 * 2026-08-09 cited the tree CORRECTLY at the moment of writing and became false when the tree moved
 * under it — a role card true for 2 h 11 m, two status banners outlived by same-day ADR acceptances,
 * a citation rotted by an unrelated refactor. There is no authoring event to gate: the artifact is
 * correct at the moment a writing-time reviewer would look at it. So the check is not "did you cite
 * carefully" but "does what you cited still say so today", and it has to run on changes to the CITED
 * tree, not only on changes to the citing page. That inversion is why the workflow
 * (`.github/workflows/catalog-claims.yml`) triggers on `src/**` as well as `agents/**`.
 *
 * WHY A PLAIN NODE SCRIPT WITH ITS OWN REPO-ROOT WORKFLOW (enforcement.md §"Shape", ADR-0032 §D).
 * No stack's CI watches `agents/`: `backend-ci` / `frontend-ci` / `ios-ci` / `android-ci` all trigger
 * on `src/` subtrees, and `nx affected` selects zero projects for a markdown-only diff. Same
 * structural argument ADR-0037 D7 recorded for `check-available-status-parity.mjs`, same answer.
 *
 * THREE OBLIGATIONS, all decidable from in-repo text with no compiler and no type graph:
 *
 *   C1  ADR status agreement — a status claim about an ADR must agree with that ADR's own
 *       `- **Status:**` line. ADRs resolve by the `NNNN-` filename prefix, never the full name.
 *   C2  A "not yet built" banner carries a `Retires when:` condition (C2-FORM), and a
 *       `Retires when: <path> exists` condition whose path EXISTS means the banner is already dead
 *       (C2-RETIRED).
 *   C3  Citation resolution — every `Path.ext:N`, `Type.Member:N-M` and continuation `:N-M`
 *       citation resolves: the file exists and has at least that many lines. A continuation's file is
 *       INFERRED, not read, so its verdict is `C3-SOFT` — printed, never counted, never blocking.
 *
 * WHAT C3 CANNOT DECIDE, AND DOES NOT CLAIM TO. It cannot decide that the cited lines SAY what the
 * entry claims. That residue is the reader's, permanently. C3B goes one step past existence without
 * pretending to close it: when a citation is immediately preceded by a backticked span, C3B asserts
 * that at least one identifier out of that span still appears inside the cited range (widened by
 * ANCHOR_SLACK). That catches a range that slid off its subject, which plain existence cannot. It is
 * ADVISORY and heuristic in both directions — a paraphrased subject misses, a common identifier can
 * be present by accident — so it never sets the exit code, and its hit rate is printed so a reader
 * can weigh it rather than trust it.
 *
 * ANTI-VACUITY (ADR-0032 D3, enforcement.md §"Anti-vacuity"). A checker that silently matches nothing
 * passes every file, so this one asserts its own REACH and prints it on every run:
 *   - floors on the corpus, the ADR set, the indexed tree, and the number of claims FOUND per
 *     obligation. Under any of them the tool did not read what it claims to have read: hard P0.
 *   - a dumb independent second scan (LOOSE_RE) for anything shaped like `.<known-ext>:N`,
 *     cross-checked against the character spans the real parser consumed. An uncovered loose hit is a
 *     parser miss reported as P0, so a broken glob or a regressed regex cannot go green.
 *
 * WHAT IS DELIBERATELY OUT OF SCOPE, AND THE PRICE OF IT.
 *   - `consistency.md` deviating form 4 ("there are exactly N …" about the tree) is NOT checked.
 *     Telling a count of tree instances from a count of domain facts ("two independent axes", "two
 *     evaluation forms") needs a reader, and a detector that cannot tell them apart trains people to
 *     ignore it.
 *   - Text inside double quotes is skipped for all three obligations: the catalog quotes the
 *     deviating forms VERBATIM when it defines them, and quotes a hypothetical review verdict with a
 *     made-up `file:line` in it. The price is that a genuine assertion written inside quotes is
 *     invisible here. Both skip counts are printed on every run.
 *
 * Usage:
 *   node agents/tools/check-catalog-claims.mjs              # strict: P0/P1 -> exit 1
 *   node agents/tools/check-catalog-claims.mjs --warn       # today's CI tier: advisory about the
 *                                                          #   catalog (P1 exits 0), still blocking
 *                                                          #   about the tool itself (P0 exits 1)
 *   node agents/tools/check-catalog-claims.mjs --verbose    # list every claim FOUND, not just failed
 *   node agents/tools/check-catalog-claims.mjs --root=DIR   # run against another tree (self-test)
 *   node agents/tools/check-catalog-claims.mjs --floors=off # self-test only: disable the reach floors
 */
import { existsSync, readFileSync, readdirSync, statSync } from "node:fs";
import { basename, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

// ── configuration ───────────────────────────────────────────────────────────

/** The corpus the rule governs — `conventions.md` names exactly these two trees. */
// The corpus follows the files, not the folder. CL-034 published the domain-truth half of
// agents/knowledge — the S1–S12 security laws, the per-component role contracts and the expandability
// doctrine — into docs/. Their claims about the tree did not stop needing checking just because they
// moved, so the paths move with them. Entries may be a directory OR a single file.
//
// Deliberately NOT "docs/**": that would sweep in the 51 migrated ADRs, whose thousands of line-number
// citations have never been C3-checked. Widening coverage is a decision to take on its own, not a side
// effect of a move.
const CORPUS_DIRS = [
    "agents/knowledge",
    "agents/process",
    "docs/domain/roles",
    "docs/architecture/security-rules.md",
    "docs/architecture/platform-expandability.md",
];
const ADR_DIR = "docs/decisions";

/** Trees a citation may point into. Anything outside them resolves as "missing". */
const INDEX_ROOTS = ["src", "agents", "docs", ".github", "scripts", "deploy", "sql-scripts"];

const IGNORE_DIRS = new Set([
    ".git", "node_modules", "bin", "obj", "dist", "out", "build", ".nx", ".angular", ".gradle",
    ".build", "DerivedData", "Pods", "graphify-out", ".venv", "__pycache__", ".idea", ".vs",
    "TestResults", "coverage", ".terraform",
]);

/** Extensions a `foo.EXT:12` citation may name. Explicit so the index stays small and legible. */
const EXTS = [
    "cs", "ts", "tsx", "js", "jsx", "mjs", "cjs", "kt", "kts", "swift", "java", "json", "yml",
    "yaml", "md", "html", "scss", "css", "sql", "sh", "ps1", "xml", "plist", "props", "targets",
    "csproj", "sln", "gradle", "toml", "xcstrings", "pbxproj", "bicep", "http", "txt", "csv",
    "storyboard", "xcconfig", "razor", "cshtml", "resx",
];
const EXT_SET = new Set(EXTS);
/** Where a bare `TypeName` or `TypeName.Member` citation could live. */
const CODE_EXTS = ["cs", "kt", "swift", "ts", "mjs", "java", "kts"];

/**
 * The status vocabulary of an ADR's own `- **Status:**` line. `draft` is DELIBERATELY absent from the
 * catalog-side matcher: no ADR header uses it, while the word appears constantly in prose about an
 * ADR's drafting ("the ADR's own draft had the direction backwards"), and admitting it turns a
 * precise check into a noisy one.
 */
const ADR_STATUS_TOKENS = ["proposed", "accepted", "superseded", "rejected", "declined"];
const HEADER_STATUS_TOKENS = [...ADR_STATUS_TOKENS, "draft"];

/**
 * Words that may sit BETWEEN an ADR id and a status token without breaking the claim. Anything not
 * listed stops the walk. That is what keeps *"ADR-0025 is violated or superseded"* — a conditional
 * about a future breach — from reading as a status claim. Tuned by eye against the whole corpus:
 * `--verbose` prints every claim this admits, which is how it should be re-tuned.
 */
const CONNECTORS = new Set([
    "is", "was", "are", "were", "be", "been", "remains", "reads", "read", "stands", "still", "now",
    "the", "its", "it", "their", "this", "that", "these", "own", "status", "line", "block", "s",
    "and", "a", "an", "as", "at", "by", "of", "on", "in", "to", "per", "see", "over", "under",
    "adr", "row", "rows", "ruling", "panel", "verdict", "dated", "date", "owner", "since", "from",
    "already", "here", "today", "above", "below",
    // Relative pronouns. Added 2026-08-11 after a live miss: an entry reading
    // "**ADR-0049**, which is `proposed`" was NOT parsed as a status claim, so the ADR was
    // stamped `accepted` mid-ticket and the stale sentence sailed through a BLOCKING gate.
    // The identical claim phrased "ADR-0049 is `proposed`" one file over WAS caught, which is
    // what makes this a parser gap rather than a judgement call.
    "which", "whose",
]);
/** The REVERSE form is only a claim behind an article: "an `accepted` ADR-0039". */
const REVERSE_ARTICLES = new Set(["a", "an", "the"]);

/** Identifier-ish tokens too common to be evidence for C3B. */
const ANCHOR_STOPWORDS = new Set([
    "true", "false", "null", "void", "async", "await", "public", "private", "return", "class",
    "struct", "record", "string", "value", "this", "self", "func", "then", "when", "case", "else",
    "with", "from", "into", "type", "data", "list", "using", "import", "export", "const",
]);

const MIN_ANCHOR_LEN = 5;
const ANCHOR_SLACK = 4;
/**
 * BINDING A BARE `:N-M` CONTINUATION IS THE ONE PLACE THIS TOOL CAN LIE, so it is deliberately timid.
 * A continuation binds only to the nearest preceding citation THAT ITSELF CARRIED LINE NUMBERS,
 * within CONT_WINDOW_LINES, and only when no other file-shaped token appears in between. Everything
 * looser mis-binds on this corpus and invents findings: a wide window bound *"partner spec
 * `:165-232`"* to an interceptor spec eight lines above, and letting any backticked type name be a
 * binding target bound a table row's `:421` to the previous row's screen file. An unbound
 * continuation is reported as an undecided advisory, which is true; a mis-bound one is a lie in the
 * shape of a finding.
 */
const CONT_WINDOW_LINES = 3;

/**
 * REACH FLOORS. Measured on the corpus at the commit that introduced this tool, then set well below
 * it so ordinary growth or pruning never trips them. Under any of these the tool did NOT read what it
 * claims to have read, and that is a hard failure, not a pass. `--floors=off` exists for the
 * self-test's synthetic roots and nothing else; a self-test scenario asserts the defaults still fire.
 */
const FLOORS = {
    corpusFiles: 20, // measured 33
    adrs: 30, // measured 46
    indexedFiles: 1500, // measured 6461
    citations: 250, // measured 500
    adrClaims: 8, // measured 20
};

// ── plumbing ────────────────────────────────────────────────────────────────

const args = process.argv.slice(2);
const warnOnly = args.includes("--warn");
const verbose = args.includes("--verbose");
const floorsOff = args.includes("--floors=off");
const arg = (n) => (args.find((a) => a.startsWith(`--${n}=`)) || "").split("=").slice(1).join("=");
const rootArg = arg("root");
const REPO = rootArg
    ? resolve(rootArg)
    : resolve(join(fileURLToPath(import.meta.url), "..", "..", "..")); // agents/tools -> repo root

const findings = []; // P0/P1 — set the exit code
const advisories = []; // P2 — printed, never set the exit code
const add = (sev, file, line, rule, msg) => findings.push({ sev, file, line, rule, msg });
const note = (file, line, rule, msg) => advisories.push({ sev: "P2", file, line, rule, msg });

const rel = (abs) => relative(REPO, abs).split(sep).join("/");

function readText(abs) {
    try {
        return readFileSync(abs, "utf8");
    } catch {
        return null;
    }
}

function walk(absDir, out, filter) {
    let entries;
    try {
        entries = readdirSync(absDir, { withFileTypes: true });
    } catch {
        return out;
    }
    for (const e of entries) {
        const abs = join(absDir, e.name);
        if (e.isDirectory()) {
            if (IGNORE_DIRS.has(e.name)) continue;
            walk(abs, out, filter);
        } else if (e.isFile() && filter(e.name)) {
            out.push(abs);
        }
    }
    return out;
}

const extOf = (name) => {
    const i = name.lastIndexOf(".");
    return i <= 0 ? "" : name.slice(i + 1).toLowerCase();
};
const stemOf = (name) => {
    const i = name.lastIndexOf(".");
    return i <= 0 ? name : name.slice(0, i);
};

// ── the file index (for citation resolution) ────────────────────────────────

const byBasename = new Map();
const allPaths = [];
const indexFile = (r) => {
    allPaths.push(r);
    const b = basename(r);
    if (!byBasename.has(b)) byBasename.set(b, []);
    byBasename.get(b).push(r);
};

for (const root of INDEX_ROOTS) {
    const absRoot = join(REPO, root);
    if (!existsSync(absRoot)) continue;
    for (const abs of walk(absRoot, [], (n) => EXT_SET.has(extOf(n)))) indexFile(rel(abs));
}
// Root-level files (CLAUDE.md, README.md, …) are cited too, but the root itself is not walked.
try {
    for (const e of readdirSync(REPO, { withFileTypes: true })) {
        if (e.isFile() && EXT_SET.has(extOf(e.name))) indexFile(e.name);
    }
} catch {
    /* an unreadable root is caught by the indexedFiles floor */
}

const lineCounts = new Map();
function lineCount(relPath) {
    if (lineCounts.has(relPath)) return lineCounts.get(relPath);
    const text = readText(join(REPO, relPath));
    let n = 0;
    if (text !== null) {
        const parts = text.split(/\r?\n/);
        if (parts.length && parts[parts.length - 1] === "") parts.pop();
        n = parts.length;
    }
    lineCounts.set(relPath, n);
    return n;
}

const resolveCache = new Map();

/**
 * Resolve a cited path token to candidate repo-relative paths.
 *
 * The catalog cites in five live dialects and all five have to resolve, or the tool reports its own
 * parser gaps as the catalog's defects:
 *   1. a repo-relative path                     `src/Cleansia.Core.Domain/Orders/Order.cs`
 *   2. a bare basename                          `Order.cs`
 *   3. an ELLIPSIS abbreviation                 `…EntityConfiguration.cs`, `customer-app/…/Foo.kt`
 *   4. an ABBREVIATED path whose segments are substrings of the real ones, in order
 *                                               `Web.Customer/ServiceCityController.cs`
 *   5. a ticket stem                            `T-0123.md` -> `agents/backlog/tickets/T-0123-*.md`
 * Ambiguity is not resolved by guessing: every candidate is returned and the caller decides under the
 * all/none/mixed rule, so an ambiguous citation can only fail when it fails under EVERY reading.
 */
function resolvePath(token) {
    if (resolveCache.has(token)) return resolveCache.get(token);
    const out = resolvePathUncached(token);
    resolveCache.set(token, out);
    return out;
}

function resolvePathUncached(rawToken) {
    const token = rawToken.replace(/[.,;)]+$/, "");
    if (!token) return { kind: "empty", paths: [] };
    const abbreviated = token.includes("…") || token.includes("...");

    if (!abbreviated && token.includes("/")) {
        const direct = join(REPO, token);
        if (existsSync(direct) && statSync(direct).isFile()) {
            return { kind: "exact", paths: [token] };
        }
    }

    if (abbreviated) {
        const pattern = token
            .split(/…|\.\.\./)
            .map((p) => p.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"))
            .join("[^\\n]*");
        const re = new RegExp(`(^|/)${pattern}$`);
        const hits = allPaths.filter((p) => re.test(p));
        return { kind: hits.length ? "abbrev" : "missing", paths: hits, abbreviated };
    }

    if (token.includes("/")) {
        const suffix = allPaths.filter((p) => p.endsWith(`/${token}`));
        if (suffix.length) return { kind: "suffix", paths: suffix };
        // Dialect 4: the last segment is the real basename; the earlier ones must appear, in order,
        // as substrings of the real path's segments (`Web.Customer` inside `Cleansia.Web.Customer`).
        const parts = token.split("/").filter(Boolean);
        const wantBase = parts[parts.length - 1];
        const lead = parts.slice(0, -1);
        const hits = (byBasename.get(wantBase) || []).filter((p) => {
            const segs = p.split("/").slice(0, -1);
            let i = 0;
            for (const seg of segs) if (i < lead.length && seg.includes(lead[i])) i++;
            return i === lead.length;
        });
        return { kind: hits.length ? "segment-subset" : "missing", paths: hits };
    }

    const exact = byBasename.get(token);
    if (exact && exact.length) return { kind: "basename", paths: exact };

    // Dialect 5: a stem prefix with the same extension — how tickets are cited (`T-0123.md`).
    const ext = extOf(token);
    const stem = stemOf(token);
    if (ext && stem) {
        const hits = allPaths.filter((p) => {
            const b = basename(p);
            return extOf(b) === ext && stemOf(b).startsWith(`${stem}-`);
        });
        if (hits.length) return { kind: "stem-prefix", paths: hits };
    }
    return { kind: "missing", paths: [] };
}

/** `TypeName` / `TypeName.Member` -> the file(s) that could declare it. */
function resolveType(typeName) {
    const hits = [];
    for (const ext of CODE_EXTS) {
        const c = byBasename.get(`${typeName}.${ext}`);
        if (c) hits.push(...c);
    }
    return hits;
}

// ── document masks: quoted spans and bold spans ─────────────────────────────

/**
 * A boolean mask over the whole document for text inside double quotes.
 *
 * The catalog QUOTES the deviating forms verbatim when it defines them — `conventions.md` and
 * `consistency.md` both carry *"ADR-0039 is `proposed`"* as an EXAMPLE of the thing they forbid, and
 * `quality-gates.md` carries a hypothetical review verdict citing a made-up `GetOrderById.cs:31`.
 * Those must not read as assertions. Quotes routinely span lines here, so the mask is built over the
 * whole text rather than per line. The price is stated in the header and counted in the summary.
 */
function quoteMask(text) {
    const mask = new Uint8Array(text.length);
    let open = -1;
    for (let i = 0; i < text.length; i++) {
        const c = text[i];
        if (c === '"' || c === "“" || c === "”") {
            if (open < 0) open = i;
            else {
                for (let j = open; j <= i; j++) mask[j] = 1;
                open = -1;
            }
        }
    }
    return mask;
}

/** Spans wrapped in `**…**`. A banner claim is bold; prose that merely mentions a phrase is not. */
function boldMask(text) {
    const mask = new Uint8Array(text.length);
    const re = /\*\*([\s\S]{1,1200}?)\*\*/g;
    let m;
    while ((m = re.exec(text))) {
        for (let j = m.index; j < m.index + m[0].length; j++) mask[j] = 1;
    }
    return mask;
}

const lineStarts = (text) => {
    const starts = [0];
    for (let i = 0; i < text.length; i++) if (text[i] === "\n") starts.push(i + 1);
    return starts;
};
const lineOf = (starts, idx) => {
    let lo = 0;
    let hi = starts.length - 1;
    while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (starts[mid] <= idx) lo = mid;
        else hi = mid - 1;
    }
    return lo + 1;
};

// ── the ADR set ─────────────────────────────────────────────────────────────

const adrs = new Map();
{
    const dir = join(REPO, ADR_DIR);
    if (existsSync(dir)) {
        for (const name of readdirSync(dir)) {
            // Records live at docs/decisions/adr-NNNN.md — the id IS the filename, so a retitle cannot
            // break a citation. The old agents/backlog/adr/NNNN-<slug>.md form is still accepted so a
            // stale checkout reports honestly rather than silently reading zero.
            const m = /^(?:adr-)?(\d{4})(?:-.*)?\.md$/.exec(name);
            if (!m) continue;
            const text = readText(join(dir, name));
            if (text === null) continue;
            const lines = text.split(/\r?\n/);
            let status = null;
            let statusLine = 0;
            // Status is in the YAML frontmatter for records that carry one, and in a `- **Status:**`
            // bullet for the rest. Read the frontmatter first; the bullet still wins when present,
            // because that is the line a human edits.
            const fm = /^---\n([\s\S]*?)\n---/.exec(text);
            if (fm) {
                const fmHit = new RegExp(
                    `^status:\\s*\`?(${HEADER_STATUS_TOKENS.join("|")})\\b`,
                    "im",
                ).exec(fm[1]);
                if (fmHit) {
                    status = fmHit[1].toLowerCase();
                    statusLine = 1;
                }
            }
            for (let i = 0; i < Math.min(lines.length, 40); i++) {
                if (!/^\s*[-*]?\s*\*\*Status:?\*\*/i.test(lines[i])) continue;
                // The header carries an authoring hint as an HTML comment listing every legal token.
                // Strip it, or every ADR reads as `proposed`.
                const cleaned = lines[i].replace(/<!--[\s\S]*?-->/g, "");
                const hit = new RegExp(`\\b(${HEADER_STATUS_TOKENS.join("|")})\\b`, "i").exec(cleaned);
                if (hit) {
                    status = hit[1].toLowerCase();
                    statusLine = i + 1;
                }
                break;
            }
            adrs.set(m[1], { id: m[1], file: `${ADR_DIR}/${name}`, status, statusLine });
        }
    }
}

// ── corpus ──────────────────────────────────────────────────────────────────

const corpus = [];
for (const d of CORPUS_DIRS) {
    const abs = join(REPO, d);
    if (!existsSync(abs)) continue;
    if (statSync(abs).isFile()) {
        if (abs.endsWith(".md")) corpus.push(abs);
        continue;
    }
    for (const f of walk(abs, [], (n) => n.endsWith(".md"))) corpus.push(f);
}
corpus.sort();

// ── obligation 1 — ADR status agreement ─────────────────────────────────────

const tokenize = (s) =>
    s
        .replace(/[’']s\b/g, " s ")
        .split(/[^A-Za-z0-9]+/)
        .filter(Boolean);

const isConnector = (w) => {
    const lw = w.toLowerCase();
    if (CONNECTORS.has(lw)) return true;
    if (/^\d+$/.test(lw)) return true; // dates, section numbers, amendment ordinals
    if (/^(d|ch|am|r|ft|q|s|c|e|b|v)\d+$/.test(lw)) return true; // D4, CH-M7, AM-3, FT-12, S12 …
    return false;
};

const isStatus = (w) => ADR_STATUS_TOKENS.includes(w.toLowerCase());

/**
 * Walk outward from an ADR id through CONNECTORS only. A status token reached that way is a claim
 * ABOUT that ADR; one reached by crossing an unlisted word is a coincidence. The BACKWARD walk
 * additionally demands an article in front of the token ("an `accepted` ADR-0039"), because without
 * it *"that coupling was explicitly rejected (ADR-0025 PA-5)"* reads as a status claim, and it is not.
 */
function claimNear(text, idIdx, idLen) {
    for (const w of tokenize(text.slice(idIdx + idLen, idIdx + idLen + 90))) {
        if (isStatus(w)) return { token: w.toLowerCase(), dir: "after" };
        if (!isConnector(w)) break;
    }
    const back = tokenize(text.slice(Math.max(0, idIdx - 60), idIdx)).reverse();
    for (let i = 0; i < back.length; i++) {
        const w = back[i];
        if (isStatus(w)) {
            const article = back[i + 1];
            if (article && REVERSE_ARTICLES.has(article.toLowerCase())) {
                return { token: w.toLowerCase(), dir: "before" };
            }
            break;
        }
        if (!isConnector(w)) break;
    }
    return null;
}

// ── obligation 2 — "not yet built" banners ──────────────────────────────────

/**
 * The banner vocabulary of `conventions.md` form 2, and no wider. *"does not exist yet"* was tried
 * and removed: the backend catalog uses it about a database ROW that does not exist yet at the moment
 * a handler returns, which is not a claim about the tree at all.
 */
const NOT_BUILT_RE =
    /\b(?:not\s+yet\s+built|not\s+built|not\s+yet\s+shipped|not\s+shipped\s+yet|no\s+ticket\s+is\s+cut|no\s+ticket\s+yet)\b/gi;
const RETIRES_RE = /\*{0,2}Retires\s+when:?\*{0,2}\s*([^\n]*)/gi;

const PATHISH_RE = /`([^`\s]{3,200})`/g;
const looksLikePath = (t) => {
    if (/[*?<>|"']/.test(t) || /\s/.test(t)) return false;
    const bare = t.replace(/:\d+(-\d+)?$/, "").replace(/[.,;)]+$/, "");
    if (!/[A-Za-z0-9_)\]]$/.test(bare)) return false;
    return EXT_SET.has(extOf(basename(bare)));
};

/** The banner's block: the whole blockquote run it sits in, or its paragraph. */
function blockRange(lines, idx) {
    const isQuote = (l) => /^\s*>/.test(l);
    const blank = (l) => l.trim() === "";
    let from = idx;
    let to = idx;
    if (isQuote(lines[idx])) {
        while (from > 0 && isQuote(lines[from - 1])) from--;
        while (to < lines.length - 1 && isQuote(lines[to + 1])) to++;
        return [from, to];
    }
    while (from > 0 && !blank(lines[from - 1])) from--;
    while (to < lines.length - 1 && !blank(lines[to + 1])) to++;
    return [from, to];
}

// ── obligation 3 — citations ────────────────────────────────────────────────

const EXT_ALT = EXTS.join("|");
const PATH_CHARS = "[A-Za-z0-9_@\\u2026.-]";
/** A named citation: an optional path, a filename with a known extension, then one or more `:N[-M]`. */
const NAMED_RE = new RegExp(
    `((?:${PATH_CHARS}+/)*\\.?[A-Za-z0-9_@\\u2026-]${PATH_CHARS}*\\.(?:${EXT_ALT}))((?::\\d+(?:-\\d+)?)+)`,
    "g",
);
/** The same shape with no line numbers — what a later continuation may bind to. */
const MENTION_RE = new RegExp(
    `((?:${PATH_CHARS}+/)*\\.?[A-Za-z0-9_@\\u2026-]${PATH_CHARS}*\\.(?:${EXT_ALT}))\\b`,
    "g",
);
/** A continuation citation: a bare `:N` / `:N-M` inside backticks, e.g. "(`:31-42`)". */
const CONT_RE = /`:(\d+)(?:-(\d+))?`/g;
/** The `Type.Member:N-M` dialect — `UserMembership.IsActive:84-85`. Inside backticks only. */
const TYPE_MEMBER_RE = /`([A-Z][A-Za-z0-9_]{3,})\.([A-Za-z_][A-Za-z0-9_]*)((?::\d+(?:-\d+)?)+)`/g;
/**
 * A bare backticked type name. It is NEVER a binding anchor — only a numbered citation is — but it
 * IS a blocker: once the prose has named a new subject that lives in a file, a following `:N` belongs
 * to that subject, not to the citation before it. Without this, *"guarded by
 * `DeclaredSuccessResponseMatchesTheReturnedTypeTests` — `:36-43` … (`:108-145`)"* bound both ranges
 * to the controller named one sentence earlier and reported the test's own line numbers as rot.
 */
const BARE_TYPE_RE = /`([A-Z][A-Za-z0-9_]{3,})`/g;
/**
 * The DUMB second scan, independent of the parser on purpose: anything shaped like `.<known-ext>:N`
 * should have been consumed, and whatever was not is a parser miss, not a clean file.
 */
const LOOSE_RE = new RegExp(`[.\\u2026](?:${EXT_ALT}):\\d+`, "g");

const stats = {
    adrClaimsFound: 0,
    adrClaimsQuoted: 0,
    notBuiltFound: 0,
    notBuiltQuoted: 0,
    retiresMarkers: 0,
    retiresPathMarkers: 0,
    citationsNamed: 0,
    citationsTypeMember: 0,
    citationsCont: 0,
    citationsQuoted: 0,
    citationsUnbound: 0,
    citationsAmbiguous: 0,
    anchorAttempted: 0,
    anchorOk: 0,
    anchorMiss: 0,
};
const failed = { C1: 0, C2: 0, C3: 0 };

for (const abs of corpus) {
    const file = rel(abs);
    const text = readText(abs);
    if (text === null) {
        add("P0", file, 0, "REACH", "corpus file could not be read");
        continue;
    }
    const lines = text.split(/\r?\n/);
    const qm = quoteMask(text);
    const bm = boldMask(text);
    const starts = lineStarts(text);
    const quoted = (i) => qm[i] === 1;

    // ── C1 ──────────────────────────────────────────────────────────────────
    for (const m of text.matchAll(/\bADR-(\d{4})\b/g)) {
        const claim = claimNear(text, m.index, m[0].length);
        if (!claim) continue;
        if (quoted(m.index)) {
            stats.adrClaimsQuoted++;
            continue;
        }
        stats.adrClaimsFound++;
        const ln = lineOf(starts, m.index);
        if (verbose) console.log(`  found C1  ${file}:${ln}  ADR-${m[1]} ${claim.dir} '${claim.token}'`);
        const adr = adrs.get(m[1]);
        if (!adr) {
            add("P1", file, ln, "C1", `claims ADR-${m[1]} is \`${claim.token}\` — no ADR ${m[1]} exists in ${ADR_DIR}/`);
            failed.C1++;
        } else if (!adr.status) {
            add("P1", file, ln, "C1", `claims ADR-${m[1]} is \`${claim.token}\` — ${adr.file} has no parseable \`- **Status:**\` line`);
            failed.C1++;
        } else if (adr.status !== claim.token) {
            add("P1", file, ln, "C1", `claims ADR-${m[1]} is \`${claim.token}\`; ${adr.file}:${adr.statusLine} reads \`${adr.status}\``);
            failed.C1++;
        }
    }

    // ── C2 ──────────────────────────────────────────────────────────────────
    for (const m of text.matchAll(NOT_BUILT_RE)) {
        if (quoted(m.index)) {
            stats.notBuiltQuoted++;
            continue;
        }
        if (bm[m.index] !== 1) continue; // a prose mention, not a banner
        stats.notBuiltFound++;
        const ln = lineOf(starts, m.index);
        if (verbose) console.log(`  found C2  ${file}:${ln}  "${m[0]}"`);
        const [from, to] = blockRange(lines, ln - 1);
        const block = lines.slice(from, to + 1).join("\n");
        if (!/Retires\s+when/i.test(block)) {
            add("P1", file, ln, "C2-FORM", `"${m[0].trim()}" banner carries no \`Retires when:\` condition — nothing can ever falsify it`);
            failed.C2++;
        }
    }

    // The liveness half: a retirement condition whose path already exists is a dead banner.
    for (const m of text.matchAll(RETIRES_RE)) {
        stats.retiresMarkers++;
        const tail = m[1];
        if (!/\bexists?\b/i.test(tail)) continue;
        const toks = [...tail.matchAll(PATHISH_RE)].map((p) => p[1]).filter(looksLikePath);
        if (!toks.length) continue;
        stats.retiresPathMarkers++;
        const ln = lineOf(starts, m.index);
        for (const t of toks) {
            const r = resolvePath(t.replace(/:\d+(-\d+)?$/, ""));
            if (r.paths.length) {
                add("P1", file, ln, "C2-RETIRED", `\`Retires when: ${t} exists\` — it exists (${r.paths[0]}); the banner it guards is now false`);
                failed.C2++;
            }
        }
    }

    // ── C3 ──────────────────────────────────────────────────────────────────
    const consumed = new Uint8Array(text.length);
    /** The last citation that carried line numbers — the only thing a continuation may bind to. */
    let anchorCite = null;
    /** The end offset of the last file-shaped token seen; a newer one invalidates the binding. */
    let lastFileTokenEnd = -1;

    const events = [];
    for (const m of text.matchAll(NAMED_RE)) events.push({ kind: "named", m, idx: m.index });
    for (const m of text.matchAll(TYPE_MEMBER_RE)) {
        // `Foo.cs:12` also matches Type.Member; it is a FILENAME and NAMED_RE owns it. Without this
        // every path citation was checked and reported twice.
        if (EXT_SET.has(m[2].toLowerCase())) continue;
        events.push({ kind: "typemember", m, idx: m.index });
    }
    for (const m of text.matchAll(MENTION_RE)) events.push({ kind: "mention", m, idx: m.index });
    for (const m of text.matchAll(BARE_TYPE_RE)) events.push({ kind: "baretype", m, idx: m.index });
    for (const m of text.matchAll(CONT_RE)) events.push({ kind: "cont", m, idx: m.index });
    const order = { named: 0, typemember: 1, mention: 2, baretype: 3, cont: 4 };
    events.sort((a, b) => a.idx - b.idx || order[a.kind] - order[b.kind]);

    const claimedAt = new Set();
    for (const ev of events) {
        const ln = lineOf(starts, ev.idx);
        const end = ev.idx + ev.m[0].length;

        if (ev.kind === "named" || ev.kind === "typemember") {
            claimedAt.add(ev.idx);
            for (let j = ev.idx; j < end; j++) consumed[j] = 1;
            const isType = ev.kind === "typemember";
            const token = isType ? ev.m[1] : ev.m[1];
            const range = isType ? ev.m[3] : ev.m[2];
            const r = isType ? { kind: "type", paths: resolveType(token) } : resolvePath(token);
            lastFileTokenEnd = end;
            if (r.paths.length) anchorCite = { r, token, line: ln, end };
            if (quoted(ev.idx)) {
                stats.citationsQuoted++;
                continue;
            }
            if (isType) {
                if (!r.paths.length) continue; // not a type this repo declares in a file
                stats.citationsTypeMember++;
                checkCitation(file, ln, `${token}.${ev.m[2]}`, range, r, text, ev.idx, " [type]");
            } else {
                stats.citationsNamed++;
                checkCitation(file, ln, token, range, r, text, ev.idx, "");
            }
            continue;
        }

        if (ev.kind === "mention") {
            if (!claimedAt.has(ev.idx)) lastFileTokenEnd = Math.max(lastFileTokenEnd, end);
            continue;
        }

        if (ev.kind === "baretype") {
            if (resolveType(ev.m[1]).length) lastFileTokenEnd = Math.max(lastFileTokenEnd, end);
            continue;
        }

        // continuation
        for (let j = ev.idx; j < end; j++) consumed[j] = 1;
        if (quoted(ev.idx)) {
            stats.citationsQuoted++;
            continue;
        }
        stats.citationsCont++;
        const bindable =
            anchorCite &&
            ln - anchorCite.line <= CONT_WINDOW_LINES &&
            lastFileTokenEnd <= anchorCite.end;
        if (!bindable) {
            stats.citationsUnbound++;
            note(file, ln, "C3-UNBOUND", `continuation \`${ev.m[0]}\` cannot be bound to a numbered citation within ${CONT_WINDOW_LINES} lines with no other subject named in between — not decided`);
            continue;
        }
        const range = `:${ev.m[1]}${ev.m[2] ? `-${ev.m[2]}` : ""}`;
        checkCitation(file, ln, anchorCite.token, range, anchorCite.r, text, ev.idx, " [cont]");
    }

    // ── anti-vacuity: the dumb second scan ──────────────────────────────────
    for (const m of text.matchAll(LOOSE_RE)) {
        if (consumed[m.index] === 1) continue;
        add(
            "P0",
            file,
            lineOf(starts, m.index),
            "REACH",
            `citation-shaped text \`${m[0]}\` was NOT consumed by the citation parser — the parser is stale; this is not a clean file`,
        );
    }
}

/**
 * Resolve one citation and check the line count. `range` is one or more `:N` / `:N-M` groups.
 *
 * AMBIGUITY IS NOT GUESSED. When a token matches several files the citation fails only if it fails
 * under EVERY candidate, passes silently if it holds under every candidate, and is reported as an
 * undecidable advisory in between. A checker that picked one would invent findings.
 *
 * A CONTINUATION'S VERDICT IS ADVISORY EVEN WHEN IT FAILS, because its FILE was inferred rather than
 * read: the entry wrote `:118-130`, not a path. The finding is worth printing — that is how the
 * reader sees it at all — but it must never be counted as a proven violation or set an exit code, or
 * the tool's own number would be built on a guess.
 */
function checkCitation(file, ln, token, range, r, text, idx, tag) {
    const soft = tag === " [cont]";
    const fail = (msg) => {
        if (soft) return note(file, ln, "C3-SOFT", msg);
        add("P1", file, ln, "C3", msg);
        failed.C3++;
    };
    const nums = [...range.matchAll(/:(\d+)(?:-(\d+))?/g)].map((g) => ({
        from: Number(g[1]),
        to: Number(g[2] ?? g[1]),
    }));
    if (!nums.length) return;
    const maxLine = Math.max(...nums.map((n) => n.to));

    if (!r.paths.length) {
        fail(`citation \`${token}${range}\`${tag} — no such file in the tree`);
        return;
    }
    if (r.paths.length > 1) stats.citationsAmbiguous++;

    const counts = r.paths.map((p) => ({ p, n: lineCount(p) }));
    const ok = counts.filter((c) => c.n >= maxLine);
    if (!ok.length) {
        const shown = counts.slice(0, 3).map((c) => `${c.p} (${c.n} lines)`).join(", ");
        fail(
            `citation \`${token}${range}\`${tag} — cited line ${maxLine} is past the end of ` +
                `${counts.length > 1 ? `every one of the ${counts.length} candidates` : "the file"}: ${shown}`,
        );
        return;
    }
    if (ok.length < counts.length) {
        note(file, ln, "C3-AMBIGUOUS", `citation \`${token}${range}\`${tag} matches ${counts.length} files, ${counts.length - ok.length} of them too short — cannot decide which was meant`);
        return;
    }
    if (ok.length !== 1) return; // safe under every reading; C3B needs a single file

    // ── C3B — does the cited range still mention its subject? ───────────────
    const anchors = anchorsBefore(text, idx);
    if (!anchors.length) return;
    stats.anchorAttempted++;
    const target = readText(join(REPO, ok[0].p));
    if (target === null) return;
    const tl = target.split(/\r?\n/);
    const lo = Math.max(0, Math.min(...nums.map((n) => n.from)) - 1 - ANCHOR_SLACK);
    const hi = Math.min(tl.length, maxLine + ANCHOR_SLACK);
    const window = tl.slice(lo, hi).join("\n");
    if (anchors.some((a) => new RegExp(`\\b${a}\\b`).test(window))) {
        stats.anchorOk++;
    } else {
        stats.anchorMiss++;
        note(file, ln, "C3B", `citation \`${token}${range}\`${tag} resolves, but lines ${lo + 1}-${hi} of ${ok[0].p} mention none of its stated subject(s): ${anchors.slice(0, 4).join(", ")}`);
    }
}

/**
 * Identifiers out of the backticked span immediately before a citation — the citation's declared
 * subject. Only a genuinely adjacent span counts (at most a comma / paren / dash / spaces between);
 * anything further away belongs to a different sentence. A span that is itself a path is the file,
 * not evidence about the LINES, so it yields nothing.
 */
function anchorsBefore(text, idx) {
    const lead = text.slice(Math.max(0, idx - 260), idx);
    const m = /`([^`\n]{2,200})`[\s,;:—–-]*\(?[\s]*`?$/.exec(lead);
    if (!m || looksLikePath(m[1])) return [];
    // The catalog also backticks PROSE ("`fresh fetch each open`"). Only a code-shaped span states a
    // subject: one unspaced token, or an expression carrying code punctuation.
    const span = m[1].trim();
    if (/\s/.test(span) && !/[(){}[\]<>=|&]/.test(span)) return [];
    const ids = [...span.matchAll(/[A-Za-z_][A-Za-z0-9_]{3,}/g)]
        .map((x) => x[0])
        .filter((x) => x.length >= MIN_ANCHOR_LEN && !ANCHOR_STOPWORDS.has(x.toLowerCase()));
    return [...new Set(ids)].slice(0, 6);
}

// ── reach floors ────────────────────────────────────────────────────────────

const citationsFound = stats.citationsNamed + stats.citationsTypeMember + stats.citationsCont;

if (!floorsOff) {
    const checks = [
        ["corpus files", corpus.length, FLOORS.corpusFiles],
        ["ADRs parsed", adrs.size, FLOORS.adrs],
        ["indexed tree files", allPaths.length, FLOORS.indexedFiles],
        ["citations found", citationsFound, FLOORS.citations],
        ["ADR status claims found", stats.adrClaimsFound, FLOORS.adrClaims],
    ];
    for (const [what, got, floor] of checks) {
        if (got < floor) {
            add("P0", "agents/tools/check-catalog-claims.mjs", 0, "REACH", `${what}: ${got} < floor ${floor} — the walker did not read the corpus it claims to have read; this is NOT a pass`);
        }
    }
    const noStatus = [...adrs.values()].filter((a) => !a.status);
    if (noStatus.length > 2) {
        add("P0", ADR_DIR, 0, "REACH", `${noStatus.length} ADRs have no parseable \`- **Status:**\` line — the header parser is stale`);
    }
}

// ── report ──────────────────────────────────────────────────────────────────

const p0 = findings.filter((f) => f.sev === "P0");
const p1 = findings.filter((f) => f.sev === "P1");
const bySeverity = (a, b) => a.file.localeCompare(b.file) || a.line - b.line;

if (findings.length) {
    console.log("\ncatalog-claim violations:");
    for (const f of [...findings].sort(bySeverity)) {
        console.log(`  ${f.sev}  ${f.file}:${f.line}  ${f.rule}  ${f.msg}`);
    }
}
if (advisories.length) {
    console.log("\nadvisories (reported, never blocking):");
    for (const f of [...advisories].sort(bySeverity)) {
        console.log(`  P2  ${f.file}:${f.line}  ${f.rule}  ${f.msg}`);
    }
}

console.log(
    `\ncatalog-claims REACH: ${corpus.length} corpus file(s), ${adrs.size} ADR(s), ${allPaths.length} indexed tree file(s)`,
);
console.log(
    `catalog-claims FOUND: ` +
        `C1 ${stats.adrClaimsFound} ADR status claim(s) (+${stats.adrClaimsQuoted} skipped as quoted) · ` +
        `C2 ${stats.notBuiltFound} not-yet-built banner(s) (+${stats.notBuiltQuoted} quoted), ` +
        `${stats.retiresMarkers} \`Retires when:\` marker(s) of which ${stats.retiresPathMarkers} name a path · ` +
        `C3 ${citationsFound} citation(s) = ${stats.citationsNamed} named + ${stats.citationsTypeMember} Type.Member + ` +
        `${stats.citationsCont} continuation (+${stats.citationsQuoted} skipped as quoted; ` +
        `${stats.citationsAmbiguous} ambiguous, ${stats.citationsUnbound} unbound)`,
);
console.log(
    `catalog-claims C3B (advisory — is the cited subject still on the cited lines): ` +
        `${stats.anchorOk}/${stats.anchorAttempted} hit, ${stats.anchorMiss} miss. ` +
        `It cannot decide that the lines SAY what the entry claims; that stays the reader's.`,
);
console.log(
    `catalog-claims FAILED: C1 ${failed.C1} · C2 ${failed.C2} · C3 ${failed.C3} ` +
        `(${p1.length} claim violation(s), ${p0.length} reach failure(s), ${advisories.length} advisory)` +
        (warnOnly ? " [--warn: exit 0]" : ""),
);
if (p0.length) console.log("catalog-claims: NOT RUN in full — see the P0 REACH findings above.");

/**
 * `--warn` is advisory about the CATALOG and blocking about the TOOL. It suppresses P1 (the claim
 * baseline, non-zero and being swept) but never P0: a reach failure means this run measured nothing,
 * and a checker allowed to report "0 violations" while blind is the exact defect it was built to
 * close. `enforcement.md`'s zero-baseline rule is about the subject's debt, not about the instrument.
 */
process.exit(p0.length || (!warnOnly && p1.length) ? 1 : 0);
