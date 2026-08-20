#!/usr/bin/env node
/**
 * check-ios-symbols — a compiler-free gate over the iOS tree, for machines with no Swift compiler.
 *
 * WHY IT EXISTS. Development happens on Windows. `.github/workflows/ios-ci.yml` does the real thing —
 * xcodegen, SwiftFormat, SwiftLint, and a full xcodebuild build+test of all three targets on a macOS
 * runner — and it is the only thing that can say "this compiles". It is also the only thing that
 * cannot run when GitHub Actions is billing-blocked, which is what happened for seven merges. Two
 * broken builds reached the owner's Mac in that window:
 *
 *   1. #214 added `case deleteAccount` to `ProfileRoute`. `RegistrationLockView.sectionDestination`
 *      switches over that enum with NO `default`, deliberately — so the switch stopped being
 *      exhaustive and the CleansiaPartner target stopped compiling. Nothing in the change touched
 *      that file, which is exactly why a per-file review missed it. Fixed in #215.
 *   2. A `L10n.DeleteAccount` reference that could not be shown to resolve without building.
 *
 * Both are SYMBOL-LEVEL facts that a text reader can decide. This tool decides them, on Node, with no
 * Swift toolchain, no macOS, no network. It does not replace ios-ci.yml and cannot: it knows nothing
 * about types, protocol conformance, availability or SwiftUI's ViewBuilder. It is the pre-push signal
 * that exists on the machine where the code is written.
 *
 * FOUR CHECKS.
 *   1. exhaustive-switch  a `switch` with no `default` whose subject type is a locally-declared enum,
 *                         and which omits one of that enum's cases. (The #215 defect.)
 *   2. l10n-members       every `L10n.<path>` reference resolves to a declared static member.
 *   3. l10n-keys          every NON-interpolated `localized("k")` / `format("k")` key exists in the
 *                         Localizable.xcstrings the call actually resolves against.
 *   4. design-system      every `CleansiaColors.` / `Spacing.` / `CleansiaTypography.` member exists.
 *
 * FALSE POSITIVES ARE THE ONLY FAILURE MODE THAT MATTERS. A gate that cries wolf gets ignored, and an
 * ignored gate is worse than none — so every check here is deliberately INCOMPLETE in the safe
 * direction. Where the tool cannot be certain, it says nothing rather than guessing:
 *
 *   - MODULE SCOPING. Symbols resolve within {own module} + CleansiaCore, never across the two apps.
 *     A bare-name enum registry matched the customer's `CustomerSplashOutcome` against the partner's
 *     `SplashOutcome` and reported 7 phantom missing cases. If one name resolves to two different
 *     case sets inside one module's visibility, the name is AMBIGUOUS and is never anchored to.
 *   - SWITCHES ARE ANCHORED BY DECLARED TYPE, NOT BY GUESSING FROM THE LABELS. The subject must be
 *     `self` inside an enum/extension, or a plain identifier with exactly one declared type LIVE AT
 *     THAT POINT — annotations and inferred bindings both counted, so a closure parameter or a
 *     `for x in` shadows an annotation rather than being invisible to it. Anything else — a
 *     property chain, a call, a name two live bindings disagree about — is skipped. Inferring the
 *     enum from the case labels alone is what produced every false hit in the prototype.
 *   - NON-ENUM SWITCH SHAPES ARE SKIPPED WHOLE. A tuple pattern, a `_` wildcard, a string/integer
 *     literal, `true`/`false` — one unparseable arm and the whole switch is dropped.
 *   - INTERPOLATED KEYS ARE NOT KEYS. `localized("push.\(eventKey).title")` is assembled at runtime
 *     and cannot be checked statically. Reporting it as missing is noise, so it is excluded. The
 *     CODE inside a `\(…)` is a different matter and is read — see the lexer.
 *   - GENERATED PACKAGES ARE INVISIBLE. CleansiaPartnerApi / CleansiaCustomerApi are gitignored and
 *     absent here, so their enums (ContractStatus, PaymentType, …) are simply not in the registry —
 *     a switch over one anchors to nothing and is skipped, never reported as unresolved.
 *   - DECLARATION HEADERS ARE NOT USAGES. `extension L10n.Orders {` names a namespace; the prototype
 *     counted it as a reference to a member called `Orders` and reported it missing three times.
 *   - CONDITIONAL COMPILATION IS OPAQUE. An enum whose body holds a `#if` has a build-dependent case
 *     list (`EvidenceSource` declares `case image(UIImage)` only under `canImport(UIKit)`), and a
 *     switch whose body holds one has build-dependent arms. Neither is anchored.
 *
 * That costs coverage, and the summary says so out loud rather than implying otherwise: at the commit
 * that introduced this tool it anchors 149 of 359 default-less switches, and prints both numbers. The
 * 210 it declines are overwhelmingly `switch await client.foo()` and `switch vm.state` — subjects
 * whose type lives behind a call or another type's member, which needs a type-checker, not a reader.
 * The shape it DOES decide is the shape that broke the build: a switch over a locally-declared enum
 * reached through `self` or a typed parameter.
 *
 * ANTI-VACUITY (ADR-0032 D3). Every one of those "say nothing" rules is also a way for the tool to go
 * quietly blind — a lexer regression that finds zero switches prints a clean green. The floors below
 * fail the run when the tool read less than it should have. A green run always states the corpus it
 * was green over.
 *
 * Usage:
 *   node agents/tools/check-ios-symbols.mjs                  # strict: any problem -> exit 1
 *   node agents/tools/check-ios-symbols.mjs --warn           # findings report but do not fail; a
 *                                                           #   REACH failure still exits 1
 *   node agents/tools/check-ios-symbols.mjs --ios=DIR        # run against another tree (self-test)
 *   node agents/tools/check-ios-symbols.mjs --verbose        # list every anchored switch
 *   node agents/tools/check-ios-symbols.mjs --min-files=N …  # lower the anti-vacuity floors
 */
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

// ── CLI ─────────────────────────────────────────────────────────────────────────────────────────
const args = process.argv.slice(2);
const warnOnly = args.includes("--warn");
const verbose = args.includes("--verbose");
const argValue = (name, fallback) => {
    const hit = args.find((a) => a.startsWith(`--${name}=`));
    return hit ? hit.slice(name.length + 3) : fallback;
};

const REPO = resolve(argValue("root", join(fileURLToPath(import.meta.url), "..", "..", "..")));
const IOS = resolve(argValue("ios", join(REPO, "src", "cleansia_ios")));

/**
 * The three modules and who can see whom. CleansiaCore is a package both apps depend on; the apps
 * cannot see each other, which is the fact the bare-name enum registry threw away.
 */
const MODULES = [
    { name: "CleansiaCore", sees: ["CleansiaCore"] },
    { name: "CleansiaPartner", sees: ["CleansiaPartner", "CleansiaCore"] },
    { name: "CleansiaCustomer", sees: ["CleansiaCustomer", "CleansiaCore"] },
];

const CORE = "CleansiaCore";
const DESIGN_NAMESPACES = ["CleansiaColors", "Spacing", "CleansiaTypography"];

// Build artifacts and the two gitignored openapi-generated packages. The Api packages live beside
// the module dirs rather than inside them, so this is belt-and-braces — but if a Mac checkout ever
// nests them, their machine-owned sources must still stay out of the corpus.
const SKIP_DIRS = new Set([
    ".build",
    "DerivedData",
    ".swiftpm",
    ".git",
    "Pods",
    "node_modules",
    "CleansiaPartnerApi",
    "CleansiaCustomerApi",
]);

/**
 * Floors, at roughly half the counts at the commit that introduced this tool: 818 swift files,
 * 1_712 L10n references, 1_360 catalog keys, 3_333 design-system references, 143 enums and 149
 * anchored switches. They exist to catch a BROKEN READER — a lexer regression, a renamed module, a
 * wrong --ios — not to track growth, and they are overridable so the self-test can run its fixture.
 */
const FLOORS = {
    files: Number(argValue("min-files", "400")),
    l10n: Number(argValue("min-l10n", "850")),
    keys: Number(argValue("min-keys", "650")),
    design: Number(argValue("min-design", "1600")),
    switches: Number(argValue("min-switches", "70")),
};

// ── Lexer ───────────────────────────────────────────────────────────────────────────────────────
/**
 * Blank out comments and string literals, preserving every byte offset so a match in the blanked
 * text still points at the right line and at the literal that follows it.
 *
 * This is the whole foundation: brace depth is how a switch body, an enum body and a namespace are
 * found, and a `{` inside `"a { b"` or inside a `/* … *\/` block corrupts every one of them.
 * Handles Swift's line comments, NESTED block comments, regular strings with escapes, multi-line
 * `"""` strings and `#`-delimited raw strings.
 *
 * `\(…)` INTERPOLATION CONTENTS ARE CODE, NOT TEXT. Blanking them along with the literal made every
 * symbol inside one invisible, and that is where symbols hide: `L10n.Orders.servicesMore` is
 * referenced three times in this tree and all three sit inside a `\(…)`, so the member the gate
 * exists to guard was the one member it could not see. The literal TEXT around them is still blanked
 * — `"Spacing.enormous"` is a string, not a reference — and the contents are balanced, so restoring
 * them is brace-depth-neutral. A literal nested inside an interpolation is lexed in turn, which is
 * what lets `"x \(L10n.localized("k")) y"` be read as the key lookup it is.
 *
 * Returns { blanked, literals } where a literal records its own offsets and whether it interpolates.
 */
function lex(text) {
    const n = text.length;
    const out = new Array(n);
    const literals = [];
    // Default every position to itself; the loop overwrites what it blanks.
    for (let i = 0; i < n; i++) out[i] = text[i];

    const blank = (from, to) => {
        for (let i = from; i < to && i < n; i++) out[i] = text[i] === "\n" ? "\n" : " ";
    };
    const restore = (from, to) => {
        for (let i = from; i < to && i < n; i++) out[i] = text[i];
    };

    /** Lex `[from, to)` of the ORIGINAL text into `out`. Re-entered for interpolation contents. */
    function scan(from, to) {
        let i = from;
        while (i < to) {
            const c = text[i];

            // line comment
            if (c === "/" && text[i + 1] === "/") {
                let j = i;
                while (j < to && text[j] !== "\n") j++;
                blank(i, j);
                i = j;
                continue;
            }

            // block comment — Swift nests them
            if (c === "/" && text[i + 1] === "*") {
                let depth = 1;
                let j = i + 2;
                while (j < to && depth > 0) {
                    if (text[j] === "/" && text[j + 1] === "*") {
                        depth++;
                        j += 2;
                    } else if (text[j] === "*" && text[j + 1] === "/") {
                        depth--;
                        j += 2;
                    } else j++;
                }
                blank(i, j);
                i = j;
                continue;
            }

            // string literal, with any number of leading `#`
            let hashes = 0;
            let k = i;
            while (text[k] === "#") {
                hashes++;
                k++;
            }
            if (text[k] === '"') {
                const literal = readString(text, i, k, hashes);
                literals.push(literal);
                blank(literal.start, literal.end);
                // …then put the code back inside every `\(…)` and lex THAT as code.
                for (const span of literal.interpolations) {
                    restore(span.start, span.end);
                    scan(span.start, span.end);
                }
                i = literal.end;
                continue;
            }
            // `#` that opens nothing (`#if`, `#available`) is ordinary code
            i += hashes > 0 ? hashes : 1;
        }
    }

    scan(0, n);
    // `firstLiteralAfter` binary-searches this, so it must be ordered by `start`. Recursion already
    // emits it that way (an inner literal lies inside the outer one it follows); sorting is cheap
    // insurance against that ceasing to be true.
    literals.sort((a, b) => a.start - b.start);
    return { blanked: out.join(""), literals };
}

/** Read one string literal starting at `start` (quote at `quoteAt`, after `hashes` leading `#`). */
function readString(text, start, quoteAt, hashes) {
    const n = text.length;
    const pound = "#".repeat(hashes);
    const multiline = text.slice(quoteAt, quoteAt + 3) === '"""';
    const open = multiline ? '"""' : '"';
    const close = multiline ? `"""${pound}` : `"${pound}`;
    const escape = `\\${pound}`;

    let i = quoteAt + open.length;
    let value = "";
    let interpolated = false;
    /** The CONTENTS of each `\(…)`, exclusive of the parens — code the lexer must not blank. */
    const interpolations = [];

    while (i < n) {
        if (!multiline && text[i] === "\n") break; // unterminated — bail at the newline
        if (text.startsWith(escape, i)) {
            // `\(` (or `\#(`) opens an interpolation: skip a balanced paren run, which may itself
            // contain strings and braces.
            if (text[i + escape.length] === "(") {
                interpolated = true;
                const paren = i + escape.length;
                const after = skipInterpolation(text, paren);
                // Only a run that actually closed is code we can trust; an unbalanced one would
                // "restore" the rest of the file.
                if (text[after - 1] === ")" && after - 1 > paren + 1) {
                    interpolations.push({ start: paren + 1, end: after - 1 });
                }
                i = after;
                continue;
            }
            value += text[i + escape.length] ?? "";
            i += escape.length + 1;
            continue;
        }
        if (text.startsWith(close, i)) {
            return { start, end: i + close.length, value, interpolated, multiline, interpolations };
        }
        value += text[i];
        i++;
    }
    return { start, end: Math.min(i, n), value, interpolated, multiline, interpolations, unterminated: true };
}

/** From the `(` of a `\(…)`, return the offset just past its matching `)`. */
function skipInterpolation(text, open) {
    const n = text.length;
    let depth = 0;
    let i = open;
    while (i < n) {
        const c = text[i];
        if (c === '"') {
            // a nested literal inside the interpolation
            let j = i + 1;
            while (j < n && text[j] !== '"' && text[j] !== "\n") {
                if (text[j] === "\\") j++;
                j++;
            }
            i = j + 1;
            continue;
        }
        if (c === "(") depth++;
        else if (c === ")") {
            depth--;
            if (depth === 0) return i + 1;
        }
        i++;
    }
    return n;
}

// ── Offset helpers ──────────────────────────────────────────────────────────────────────────────
function lineIndex(text) {
    const starts = [0];
    for (let i = 0; i < text.length; i++) if (text[i] === "\n") starts.push(i + 1);
    return starts;
}

function lineOf(starts, offset) {
    let lo = 0;
    let hi = starts.length - 1;
    while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (starts[mid] <= offset) lo = mid;
        else hi = mid - 1;
    }
    return lo + 1;
}

/** Offset just past the `}` matching the `{` at `open`, over already-blanked text. */
function matchBrace(blanked, open) {
    let depth = 0;
    for (let i = open; i < blanked.length; i++) {
        if (blanked[i] === "{") depth++;
        else if (blanked[i] === "}") {
            depth--;
            if (depth === 0) return i;
        }
    }
    return blanked.length;
}

/**
 * A keyword only introduces a clause when it STARTS one. `if case .a = x`, `guard case`, `while
 * case` all put `case` mid-expression, and reading those as switch arms invents cases that were
 * never written. A real arm follows a newline or the switch's own `{`.
 */
function startsClause(blanked, at, allowedPrefixes = []) {
    let i = at - 1;
    while (i >= 0 && (blanked[i] === " " || blanked[i] === "\t")) i--;
    if (i < 0) return true;
    if (blanked[i] === "\n" || blanked[i] === "{" || blanked[i] === ";") return true;
    for (const word of allowedPrefixes) {
        const from = i - word.length + 1;
        if (from >= 0 && blanked.slice(from, i + 1) === word) return startsClause(blanked, from);
    }
    return false;
}

/** Split on commas that are not inside (), [] or <>. */
function splitTopLevel(text) {
    const parts = [];
    let depth = 0;
    let current = "";
    for (const c of text) {
        if (c === "(" || c === "[" || c === "<") depth++;
        else if (c === ")" || c === "]" || c === ">") depth--;
        if (c === "," && depth === 0) {
            parts.push(current);
            current = "";
            continue;
        }
        current += c;
    }
    if (current.trim()) parts.push(current);
    return parts;
}

// ── Declaration tree ────────────────────────────────────────────────────────────────────────────
const DECL_RE = /\b(enum|struct|class|actor|extension|protocol)\s+([A-Za-z_][\w.]*)/g;

/**
 * Every type declaration in a file, with its body range and its fully-qualified name. An extension
 * names an ABSOLUTE type (`extension L10n.Orders`), a nested declaration inherits its parent's path
 * (`enum L10n { enum Splash }` -> `L10n.Splash`).
 */
function scanDecls(blanked) {
    const decls = [];
    DECL_RE.lastIndex = 0;
    let m;
    while ((m = DECL_RE.exec(blanked)) !== null) {
        // `case foo(enum: Int)` cannot happen, but `indirect enum` and attributes can precede.
        const brace = findDeclBrace(blanked, m.index + m[0].length);
        if (brace === -1) continue;
        decls.push({
            kind: m[1],
            name: m[2],
            headerStart: m.index,
            bodyStart: brace,
            bodyEnd: matchBrace(blanked, brace),
        });
    }
    // Nest by containment, then qualify.
    decls.sort((a, b) => a.bodyStart - b.bodyStart || b.bodyEnd - a.bodyEnd);
    for (const d of decls) {
        let parent = null;
        for (const other of decls) {
            if (other === d) continue;
            if (other.bodyStart < d.headerStart && other.bodyEnd > d.bodyEnd) {
                if (!parent || other.bodyStart > parent.bodyStart) parent = other;
            }
        }
        d.parent = parent;
    }
    for (const d of decls) {
        d.qualified =
            d.kind === "extension" || !d.parent ? d.name : `${d.parent.qualified}.${d.name}`;
        d.depth = 0;
        for (let p = d.parent; p; p = p.parent) d.depth++;
    }
    return decls;
}

/** The `{` that opens a declaration body — skipping the inheritance clause and any `where`. */
function findDeclBrace(blanked, from) {
    let depth = 0;
    for (let i = from; i < blanked.length; i++) {
        const c = blanked[i];
        if (c === "(" || c === "[" || c === "<") depth++;
        else if (c === ")" || c === "]" || c === ">") depth--;
        else if (c === "{" && depth <= 0) return i;
        else if (c === "\n" && blanked.slice(from, i).trim().endsWith("}")) return -1;
        else if (c === "=" && depth <= 0) return -1; // `typealias`-shaped, no body
    }
    return -1;
}

/** True when `offset` sits directly in `decl`'s body rather than inside a nested declaration. */
function directlyIn(decl, decls, offset) {
    if (offset <= decl.bodyStart || offset >= decl.bodyEnd) return false;
    for (const other of decls) {
        if (other === decl) continue;
        if (other.bodyStart >= decl.bodyStart && other.bodyEnd <= decl.bodyEnd) {
            if (offset > other.bodyStart && offset < other.bodyEnd) return false;
        }
    }
    return true;
}

/** Brace depth at `offset` relative to `from`. */
function depthAt(blanked, from, offset) {
    let depth = 0;
    for (let i = from; i < offset; i++) {
        if (blanked[i] === "{") depth++;
        else if (blanked[i] === "}") depth--;
    }
    return depth;
}

// ── Enum cases ──────────────────────────────────────────────────────────────────────────────────
const CASE_DECL_RE = /\bcase\b/g;

function enumCases(blanked, decl, decls) {
    // A `#if` inside the body means the case list is BUILD-DEPENDENT (`EvidenceSource` declares
    // `case image(UIImage)` only under `canImport(UIKit)`). A text reader cannot say which
    // configuration a given switch is compiled for, so such an enum is never anchored to.
    if (blanked.slice(decl.bodyStart, decl.bodyEnd).includes("#if")) return null;

    const cases = [];
    CASE_DECL_RE.lastIndex = decl.bodyStart;
    let m;
    while ((m = CASE_DECL_RE.exec(blanked)) !== null && m.index < decl.bodyEnd) {
        if (!directlyIn(decl, decls, m.index)) continue;
        if (depthAt(blanked, decl.bodyStart, m.index) !== 1) continue;
        if (!startsClause(blanked, m.index, ["indirect"])) continue;
        // A case declaration runs to the end of the line, unless an associated-value list is open.
        let i = m.index + 4;
        let depth = 0;
        let body = "";
        while (i < blanked.length && i < decl.bodyEnd) {
            const c = blanked[i];
            if (c === "(" || c === "[" || c === "<") depth++;
            else if (c === ")" || c === "]" || c === ">") depth--;
            if (depth <= 0 && (c === "\n" || c === ";" || c === "}")) break;
            body += c;
            i++;
        }
        for (const piece of splitTopLevel(body)) {
            const name = piece.trim().match(/^([A-Za-z_]\w*)/);
            if (name) cases.push(name[1]);
        }
    }
    return cases;
}

// ── Bindings (`name` -> declared type, within the block it is live in) ──────────────────────────
const LET_VAR_RE = /\b(?:let|var)\s+([a-z_]\w*)\s*:\s*([A-Z]\w*(?:\.[A-Za-z_]\w*)*)/g;
const FUNC_HEAD_RE = /\b(?:func\s+[A-Za-z_]\w*|init\??)\s*(?:<[^>\n]*>)?\s*\(/g;
// The INFERRED shapes. None of them writes a type, and every one is ordinary Swift, so a scanner
// that only reads annotations believes the name still means whatever the last annotation said —
// which is how a `for route in sections` loop makes a switch anchor to `ProfileRoute`.
const INFERRED_RE = /\b(?:let|var)\s+([a-z_]\w*)\b(?!\s*[:\w])/g; //  let x = …, if/guard let x
const FOR_IN_RE = /\bfor\s+(?:try\s+)?(?:await\s+)?(?:case\s+)?(?:let\s+|var\s+)?(?:\(([^)\n]+)\)|([a-z_]\w*))\s+in\b/g;
// All four legal spellings of a closure parameter list, because a shadow the scanner cannot see is
// a shadow that anchors the switch to the WRONG type: `{ x in }`, `{ x -> T in }`, `{ (x: T) in }`
// and `{ (x) -> T in }`. Matching only the bare form fixed one spelling in four.
const CLOSURE_PARAM_RE =
    /\{\s*(?:\[[^\]\n]*\]\s*)?\(?\s*([a-z_]\w*(?:\s*:\s*[^,)\n]+?)?(?:\s*,\s*[a-z_]\w*(?:\s*:\s*[^,)\n]+?)?)*)\s*\)?\s*(?:->\s*[^\n{]+?\s*)?\bin\b/g;
const CASE_LET_RE = /\bcase\s+let\s+\.?[A-Za-z_][\w.]*\s*\(([^)\n]*)\)/g;

/**
 * Every identifier the file BINDS, each with the brace block it is live in and the type it was
 * declared with — `null` when the type is INFERRED. Parameter lists are read only from a real
 * signature — a labelled CALL argument (`Foo(bar: Baz.qux)`) looks identical to a parameter and
 * would bind `bar` to a type it never has.
 *
 * TWO THINGS THIS HAS TO GET RIGHT, and one file-global map got both wrong.
 *
 *   A name whose type is INFERRED must still be recorded, or it cannot shadow anything. Recording
 *   only annotations meant a closure parameter, a `for x in`, an `if let x =` or a plain
 *   `let x = expr` was invisible, the outer annotation stayed the answer, and the switch anchored to
 *   an enum the subject does not have — a false positive against code that compiles.
 *
 *   A binding is live in a BLOCK, not in a file. Dropping a name because it is bound twice
 *   ANYWHERE in the file meant one unrelated `func logRoute(route: String)` de-anchored the #215
 *   switch two functions above it, and the run then printed "clean" over a build that does not
 *   compile — the gate going blind on the exact defect it was built for, with no signal but the
 *   anchored count moving by one.
 *
 * So each binding carries its scope, and `bindingAt` answers per switch. Where two live bindings of
 * one name disagree — including an annotation against an inferred one — there is no answer and the
 * switch is not anchored, which is the original rule, now asked at the right granularity.
 */
function scanBindings(blanked) {
    // `{` offset -> matching `}` offset
    const closeOf = new Map();
    {
        const stack = [];
        for (let i = 0; i < blanked.length; i++) {
            if (blanked[i] === "{") stack.push(i);
            else if (blanked[i] === "}" && stack.length > 0) closeOf.set(stack.pop(), i);
        }
    }

    // `ownBrace` names the `{` a binding's scope opens at (a function body, a closure); `null`
    // means "the block enclosing `at`", resolved in one sweep below.
    const hits = [];
    const push = (name, type, at, ownBrace = null) => {
        if (/^[a-z_]\w*$/.test(name)) hits.push({ name, type, at, ownBrace });
    };

    let m;
    LET_VAR_RE.lastIndex = 0;
    while ((m = LET_VAR_RE.exec(blanked)) !== null) push(m[1], m[2], m.index);

    INFERRED_RE.lastIndex = 0;
    while ((m = INFERRED_RE.exec(blanked)) !== null) push(m[1], null, m.index);

    FOR_IN_RE.lastIndex = 0;
    while ((m = FOR_IN_RE.exec(blanked)) !== null) {
        for (const piece of m[1] ? splitTopLevel(m[1]) : [m[2]]) push(piece.trim(), null, m.index);
    }

    CLOSURE_PARAM_RE.lastIndex = 0;
    while ((m = CLOSURE_PARAM_RE.exec(blanked)) !== null) {
        // `(x: T)` spells the name and its type in one piece; the name is what binds.
        for (const piece of m[1].split(",")) push(piece.trim().replace(/\s*:.*$/, ""), null, m.index, m.index);
    }

    CASE_LET_RE.lastIndex = 0;
    while ((m = CASE_LET_RE.exec(blanked)) !== null) {
        for (const piece of splitTopLevel(m[1])) {
            push(piece.trim().replace(/^[a-z_]\w*\s*:\s*/, ""), null, m.index);
        }
    }

    FUNC_HEAD_RE.lastIndex = 0;
    while ((m = FUNC_HEAD_RE.exec(blanked)) !== null) {
        const open = m.index + m[0].length - 1;
        let depth = 0;
        let i = open;
        for (; i < blanked.length; i++) {
            if (blanked[i] === "(") depth++;
            else if (blanked[i] === ")") {
                depth--;
                if (depth === 0) break;
            }
        }
        // A parameter is live in the FUNCTION BODY. A protocol requirement has none, and grabbing
        // the next `{` in the file would hand its parameter names a scope they never had — so the
        // gap between `)` and `{` must read as a return clause and nothing else.
        const brace = blanked.indexOf("{", i);
        if (brace === -1) continue;
        const gap = blanked.slice(i + 1, brace);
        if (gap.includes("}") || /\b(?:func|var|let|case|init|subscript|deinit|typealias|associatedtype)\b/.test(gap)) continue;
        for (const piece of splitTopLevel(blanked.slice(open + 1, i))) {
            const p = piece.trim().match(/^(?:[A-Za-z_]\w*\s+|_\s+)?([a-z_]\w*)\s*:\s*(?:inout\s+)?([A-Z]\w*(?:\.[A-Za-z_]\w*)*)/);
            if (p) push(p[1], p[2], m.index, brace);
        }
    }

    // One left-to-right sweep answers "which block encloses this offset" for every hit that needs it.
    const need = [...new Set(hits.filter((h) => h.ownBrace === null).map((h) => h.at))].sort((a, b) => a - b);
    const enclosing = new Map();
    {
        const stack = [];
        let q = 0;
        for (let i = 0; i < blanked.length && q < need.length; i++) {
            while (q < need.length && need[q] === i) {
                enclosing.set(need[q], stack.length > 0 ? stack[stack.length - 1] : null);
                q++;
            }
            if (blanked[i] === "{") stack.push(i);
            else if (blanked[i] === "}" && stack.length > 0) stack.pop();
        }
        while (q < need.length) enclosing.set(need[q++], null);
    }

    return hits.map((h) => {
        const brace = h.ownBrace !== null ? h.ownBrace : (enclosing.get(h.at) ?? null);
        return {
            name: h.name,
            type: h.type,
            start: brace === null ? -1 : brace,
            end: brace === null ? blanked.length : (closeOf.get(brace) ?? blanked.length),
        };
    });
}

/**
 * The declared type of `name` at `offset`, or null when there is no answer — no binding is live
 * there, the live one has no annotation, or two live ones disagree.
 */
function bindingAt(bindings, name, offset) {
    let type = null;
    let found = false;
    for (const b of bindings) {
        if (b.name !== name || offset <= b.start || offset >= b.end) continue;
        if (!found) {
            type = b.type;
            found = true;
            continue;
        }
        if (b.type !== type) return null;
    }
    return found ? type : null;
}

// ── Switches ────────────────────────────────────────────────────────────────────────────────────
const SWITCH_RE = /\bswitch\b/g;
const DEFAULT_RE = /(?:@unknown\s+)?\bdefault\b\s*:/g;

/** Every switch in the file: subject text, arm labels, and whether it has a `default`. */
function scanSwitches(blanked) {
    const found = [];
    SWITCH_RE.lastIndex = 0;
    let m;
    while ((m = SWITCH_RE.exec(blanked)) !== null) {
        if (!/[\s({[,=:]/.test(blanked[m.index - 1] ?? "\n")) continue;
        // subject runs to the `{` that opens the body, at paren depth 0
        let depth = 0;
        let brace = -1;
        for (let i = m.index + 6; i < blanked.length; i++) {
            const c = blanked[i];
            if (c === "(" || c === "[") depth++;
            else if (c === ")" || c === "]") depth--;
            else if (c === "{" && depth === 0) {
                brace = i;
                break;
            }
        }
        if (brace === -1) continue;
        const subject = blanked.slice(m.index + 6, brace).trim();
        const bodyEnd = matchBrace(blanked, brace);

        // default?
        let hasDefault = false;
        DEFAULT_RE.lastIndex = brace;
        let d;
        while ((d = DEFAULT_RE.exec(blanked)) !== null && d.index < bodyEnd) {
            if (depthAt(blanked, brace, d.index) !== 1) continue;
            if (!startsClause(blanked, d.index)) continue;
            hasDefault = true;
            break;
        }

        // arms
        const labels = [];
        // A `#if` inside the body conditionally compiles ARMS, so the arms this reader sees are not
        // the arms the compiler sees. Same reasoning as the conditional-enum guard above.
        let parseable = !blanked.slice(brace, bodyEnd).includes("#if");
        let armCount = 0;
        CASE_DECL_RE.lastIndex = brace;
        let a;
        while ((a = CASE_DECL_RE.exec(blanked)) !== null && a.index < bodyEnd) {
            if (depthAt(blanked, brace, a.index) !== 1) continue;
            if (!startsClause(blanked, a.index)) continue;
            armCount++;
            // pattern runs to the `:` at paren depth 0
            let pdepth = 0;
            let pattern = "";
            for (let i = a.index + 4; i < bodyEnd; i++) {
                const c = blanked[i];
                if (c === "(" || c === "[") pdepth++;
                else if (c === ")" || c === "]") pdepth--;
                else if (c === ":" && pdepth === 0) break;
                pattern += c;
            }
            const guarded = pattern.split(/\bwhere\b/)[0];
            for (const piece of splitTopLevel(guarded)) {
                const cleaned = piece.trim().replace(/^(?:let|var)\s+/, "");
                const hit = cleaned.match(/^(?:[A-Za-z_][\w.]*)?\.([A-Za-z_]\w*)/);
                if (!hit) {
                    // `_`, a tuple, a literal, a range — not a plain enum arm. Drop the switch.
                    parseable = false;
                    break;
                }
                labels.push(hit[1]);
            }
            if (!parseable) break;
        }

        found.push({
            offset: m.index,
            subject,
            hasDefault,
            labels: [...new Set(labels)],
            armCount,
            parseable,
        });
        SWITCH_RE.lastIndex = brace + 1;
    }
    return found;
}

// ── Walk ────────────────────────────────────────────────────────────────────────────────────────
function walk(dir, out = []) {
    if (!existsSync(dir)) return out;
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        if (entry.isDirectory()) {
            if (SKIP_DIRS.has(entry.name) || entry.name.endsWith(".xcodeproj")) continue;
            walk(join(dir, entry.name), out);
            continue;
        }
        if (entry.name.endsWith(".swift")) out.push(join(dir, entry.name));
    }
    return out;
}

function findCatalog(dir, out = []) {
    if (!existsSync(dir)) return out;
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
        if (entry.isDirectory()) {
            if (SKIP_DIRS.has(entry.name) || entry.name.endsWith(".xcodeproj")) continue;
            findCatalog(join(dir, entry.name), out);
            continue;
        }
        if (entry.name === "Localizable.xcstrings") out.push(join(dir, entry.name));
    }
    return out;
}

// ── Analysis ────────────────────────────────────────────────────────────────────────────────────
const L10N_USE_RE = /\bL10n\.((?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*)/g;
const KEY_CALL_RE = /(?:\b([A-Za-z_]\w*)\s*\.\s*)?\b(localized|format)\s*\(/g;
// `(?<![\w.])` and not just `\b`: a word boundary also sits after a dot, so `Legacy.Spacing.gutter`
// read `Spacing` as THE design namespace and reported `Spacing.gutter` — a member of a type that has
// nothing to do with the design system — as undeclared.
const DESIGN_USE_RE = new RegExp(`(?<![\\w.])(${DESIGN_NAMESPACES.join("|")})\\s*\\.\\s*([A-Za-z_]\\w*)`, "g");
// `.self` / `.Type` are metatype access, not members; `.init` is the memberwise initialiser.
const NON_MEMBERS = new Set(["self", "Type", "init", "Protocol"]);

const problems = [];
const counts = { files: 0, l10n: 0, keys: 0, design: 0, switches: 0, skipped: 0, enums: 0 };
const anchored = [];

const rel = (p) => relative(REPO, p).split(sep).join("/");

/** Binary search: the first string literal whose `start` is at or after `offset`. */
function firstLiteralAfter(literals, offset) {
    let lo = 0;
    let hi = literals.length;
    while (lo < hi) {
        const mid = (lo + hi) >> 1;
        if (literals[mid].start < offset) lo = mid + 1;
        else hi = mid;
    }
    return literals[lo] ?? null;
}

if (!existsSync(IOS)) {
    console.error(`ios-symbols: no iOS tree at ${IOS} — wrong --ios, or the tree moved`);
    process.exit(1);
}

// ── pass 0: read + lex every file, once ─────────────────────────────────────────────────────────
/** @type {Map<string, {module:string, path:string, blanked:string, literals:any[], starts:number[], decls:any[], bindings:Map<string,string>}>} */
const files = new Map();
const catalogs = new Map();

for (const mod of MODULES) {
    const dir = join(IOS, mod.name);
    if (!existsSync(dir)) continue;

    for (const path of walk(dir)) {
        const text = readFileSync(path, "utf8");
        const { blanked, literals } = lex(text);
        const decls = scanDecls(blanked);
        files.set(path, {
            module: mod.name,
            path,
            text,
            blanked,
            literals,
            starts: lineIndex(text),
            decls,
            bindings: scanBindings(blanked),
        });
        counts.files++;
    }

    for (const catalog of findCatalog(dir)) {
        try {
            const parsed = JSON.parse(readFileSync(catalog, "utf8"));
            const keys = new Set(Object.keys(parsed.strings ?? {}));
            if (!catalogs.has(mod.name)) catalogs.set(mod.name, { path: catalog, keys });
            else for (const k of keys) catalogs.get(mod.name).keys.add(k);
        } catch (e) {
            problems.push({ check: "l10n-keys", where: rel(catalog), message: `unreadable string catalog: ${e.message}` });
        }
    }
}

// ── pass 1: registries, per module ──────────────────────────────────────────────────────────────
/** module -> Map(bare enum name -> {cases:Set, qualified:string, ambiguous:boolean}) */
const enumsByModule = new Map(MODULES.map((m) => [m.name, new Map()]));
/** module -> Set of declared `L10n.…` member paths */
const l10nMembers = new Map(MODULES.map((m) => [m.name, new Set()]));
/** module -> namespace -> Set of declared design-system members */
const designMembers = new Map(MODULES.map((m) => [m.name, new Map(DESIGN_NAMESPACES.map((n) => [n, new Set()]))]));

// `public static private(set) var accent` is legal Swift, and requiring `var|let|func` IMMEDIATELY
// after `static` meant the member was never registered and every reference to it read as undeclared.
const MEMBER_RE = /\bstatic\s+(?:(?:private|fileprivate|internal|public|package)\s*\(\s*set\s*\)\s+)*(?:var|let|func)\s+([A-Za-z_]\w*)/g;

function declaredMembers(blanked, decl, decls) {
    const members = [];
    MEMBER_RE.lastIndex = decl.bodyStart;
    let m;
    while ((m = MEMBER_RE.exec(blanked)) !== null && m.index < decl.bodyEnd) {
        if (!directlyIn(decl, decls, m.index)) continue;
        if (depthAt(blanked, decl.bodyStart, m.index) !== 1) continue;
        members.push(m[1]);
    }
    return members;
}

for (const f of files.values()) {
    for (const decl of f.decls) {
        // enums
        if (decl.kind === "enum") {
            const cases = enumCases(f.blanked, decl, f.decls);
            if (cases && cases.length > 0) {
                const registry = enumsByModule.get(f.module);
                const existing = registry.get(decl.name);
                if (!existing) {
                    registry.set(decl.name, {
                        cases: new Set(cases),
                        qualified: decl.qualified,
                        owner: f.module,
                        ambiguous: false,
                    });
                    counts.enums++;
                } else if (existing.qualified !== decl.qualified) {
                    existing.ambiguous = true;
                }
            }
        }

        // L10n namespaces
        if (decl.qualified === "L10n" || decl.qualified.startsWith("L10n.")) {
            for (const member of declaredMembers(f.blanked, decl, f.decls)) {
                l10nMembers.get(f.module).add(`${decl.qualified}.${member}`);
            }
        }

        // design-system namespaces
        if (DESIGN_NAMESPACES.includes(decl.qualified)) {
            for (const member of declaredMembers(f.blanked, decl, f.decls)) {
                designMembers.get(f.module).get(decl.qualified).add(member);
            }
        }

        // A NESTED TYPE resolves under the namespace just as a static member does, and the usage
        // regex cannot tell them apart. `CleansiaTypography` already declares `public struct Scale`,
        // so counting only `static var|let|func` made writing that type's name anywhere a finding.
        if (decl.parent && DESIGN_NAMESPACES.includes(decl.parent.qualified)) {
            designMembers.get(f.module).get(decl.parent.qualified).add(decl.name);
        }
    }
}

/** Merge each module's own registry with what CleansiaCore exports to it. */
function visible(moduleName) {
    const mod = MODULES.find((m) => m.name === moduleName);
    return mod ? mod.sees : [moduleName];
}

/**
 * The registry is keyed by BARE name, because that is how most annotations are written — but the
 * annotation's qualifier, when it has one, has to agree. Reducing `UIBlurEffect.Style` to `Style`
 * resolved it to the local `SlideToConfirm.Style` and reported a missing case naming a type the
 * source file never mentions: the cross-type name collision this anchoring exists to prevent,
 * reintroduced one `.pop()` below the fix. Swift lets a nested type be named by any suffix of its
 * path from an enclosing scope, so a suffix is what the annotation must be.
 */
function nameMatches(annotation, qualified) {
    const want = annotation.split(".");
    const have = qualified.split(".");
    if (want.length > have.length) return false;
    return want.every((part, k) => part === have[have.length - want.length + k]);
}

function lookupEnum(moduleName, typeName) {
    const bare = typeName.split(".").pop();
    let hit = null;
    for (const source of visible(moduleName)) {
        const entry = enumsByModule.get(source)?.get(bare);
        if (!entry) continue;
        if (!nameMatches(typeName, entry.qualified)) continue;
        if (entry.ambiguous) return null;
        // The same bare name declared in BOTH the app and Core resolves by Swift's own shadowing
        // rules, which a text reader does not implement. Two owners, no answer.
        if (hit && (hit.owner !== entry.owner || hit.qualified !== entry.qualified)) return null;
        hit = entry;
    }
    return hit;
}

function l10nDeclared(moduleName) {
    const set = new Set();
    for (const source of visible(moduleName)) for (const m of l10nMembers.get(source) ?? []) set.add(m);
    return set;
}

function designDeclared(moduleName, ns) {
    const set = new Set();
    for (const source of visible(moduleName)) for (const m of designMembers.get(source)?.get(ns) ?? []) set.add(m);
    return set;
}

const l10nDeclaredCache = new Map(MODULES.map((m) => [m.name, l10nDeclared(m.name)]));
const designDeclaredCache = new Map(
    MODULES.map((m) => [m.name, new Map(DESIGN_NAMESPACES.map((ns) => [ns, designDeclared(m.name, ns)]))])
);

// ── pass 2: the four checks ─────────────────────────────────────────────────────────────────────
for (const f of files.values()) {
    const at = (offset) => `${rel(f.path)}:${lineOf(f.starts, offset)}`;

    // Header ranges — `extension L10n.Orders {` NAMES a namespace, it does not USE a member.
    const headers = f.decls.map((d) => [d.headerStart, d.bodyStart]);
    const inHeader = (offset) => headers.some(([a, b]) => offset >= a && offset < b);

    // The bodies of `enum L10n`, `extension L10n.Orders`, `enum CoreL10n` — where a bare
    // `localized(…)` / `format(…)` really is the catalog lookup it looks like.
    const l10nBodies = f.decls
        .filter((d) => /^(?:Core)?L10n(?:\.|$)/.test(d.qualified))
        .map((d) => [d.bodyStart, d.bodyEnd]);
    const inL10nNamespace = (offset) => l10nBodies.some(([a, b]) => offset > a && offset < b);

    // 1 ── exhaustive-switch
    for (const sw of scanSwitches(f.blanked)) {
        if (sw.hasDefault) continue;
        if (!sw.parseable || sw.labels.length === 0) {
            counts.skipped++;
            continue;
        }

        // Anchor the switch to a DECLARED type. Guessing from the labels is what produced every
        // false positive the prototype had.
        let typeName = null;
        if (sw.subject === "self") {
            let innermost = null;
            for (const d of f.decls) {
                if (sw.offset > d.bodyStart && sw.offset < d.bodyEnd) {
                    if (!innermost || d.bodyStart > innermost.bodyStart) innermost = d;
                }
            }
            // `switch self` inside a nested func of a struct that holds an enum must not anchor to
            // the struct — lookupEnum returning null for a non-enum name handles that.
            if (innermost) typeName = innermost.name;
        } else if (/^[a-z_]\w*$/.test(sw.subject)) {
            typeName = bindingAt(f.bindings, sw.subject, sw.offset);
        }
        if (!typeName) {
            counts.skipped++;
            continue;
        }

        const target = lookupEnum(f.module, typeName);
        if (!target) {
            counts.skipped++;
            continue;
        }

        // Labels the enum does not declare mean this is an Optional switch (`.none`/`.some`), a
        // nested pattern, or a parse we should not trust. Either way: not our call to make.
        if (sw.labels.some((l) => !target.cases.has(l))) {
            counts.skipped++;
            continue;
        }

        counts.switches++;
        anchored.push(`${at(sw.offset)}  switch ${sw.subject} -> ${target.qualified} (${sw.labels.length}/${target.cases.size})`);

        const missing = [...target.cases].filter((c) => !sw.labels.includes(c));
        if (missing.length > 0) {
            problems.push({
                check: "exhaustive-switch",
                where: at(sw.offset),
                message: `switch over ${target.qualified} has no \`default\` and omits ${missing.map((c) => `.${c}`).join(", ")}`,
            });
        }
    }

    // 2 ── every `L10n.<path>` resolves to a declared member
    const declared = l10nDeclaredCache.get(f.module);
    L10N_USE_RE.lastIndex = 0;
    let use;
    while ((use = L10N_USE_RE.exec(f.blanked)) !== null) {
        if (inHeader(use.index)) continue;
        counts.l10n++;
        const parts = use[1].split(".");
        let resolved = false;
        for (let n = parts.length; n > 0; n--) {
            if (declared.has(`L10n.${parts.slice(0, n).join(".")}`)) {
                resolved = true;
                break;
            }
        }
        if (!resolved) {
            problems.push({
                check: "l10n-members",
                where: at(use.index),
                message: `L10n.${use[1]} — no such declared member in ${f.module}`,
            });
        }
    }

    // 3 ── every non-interpolated key exists in the catalog it resolves against
    KEY_CALL_RE.lastIndex = 0;
    let call;
    while ((call = KEY_CALL_RE.exec(f.blanked)) !== null) {
        const receiver = call[1];
        // `RecurringTime.format(date)` is a different `format`. Only the L10n ones count.
        if (receiver && receiver !== "L10n" && receiver !== "CoreL10n") continue;
        // A BARE call is the shape the L10n extensions are written in (`localized("orders")`), so it
        // counts as a lookup only where that is what it means: inside an L10n namespace. Anywhere
        // else it is somebody's own helper, and `format("%d items", n)` is a format string, not a
        // key the catalog was ever going to hold.
        if (!receiver && !inL10nNamespace(call.index)) continue;
        // `static func localized(_ key: String) -> String { "" }` DECLARES the helper; the literal
        // that follows is a return value, not a catalog key.
        if (/\bfunc\s+$/.test(f.blanked.slice(Math.max(0, call.index - 12), call.index))) continue;
        // The key is the FIRST argument, and it must be a literal. The blanked text cannot say where
        // one starts (a literal and whitespace are both spaces there), so the gap is measured on the
        // ORIGINAL text: only whitespace may sit between the `(` and the opening quote. `L10n.localized(`
        // with the key on the next line is the common shape and must still be read.
        const openParen = call.index + call[0].length;
        const literal = firstLiteralAfter(f.literals, openParen);
        if (!literal || f.text.slice(openParen, literal.start).trim() !== "") continue;
        if (literal.interpolated) continue; // assembled at runtime; nothing static to check
        // `CoreL10n.localized` reads CleansiaCore's catalog wherever it is called from.
        const owner = receiver === "CoreL10n" ? CORE : f.module;
        const catalog = catalogs.get(owner);
        if (!catalog) continue;
        counts.keys++;
        if (!catalog.keys.has(literal.value)) {
            problems.push({
                check: "l10n-keys",
                where: at(call.index),
                message: `"${literal.value}" is not in ${rel(catalog.path)}`,
            });
        }
    }

    // 4 ── design-system members
    DESIGN_USE_RE.lastIndex = 0;
    let dm;
    while ((dm = DESIGN_USE_RE.exec(f.blanked)) !== null) {
        if (inHeader(dm.index)) continue;
        if (NON_MEMBERS.has(dm[2])) continue;
        counts.design++;
        if (!designDeclaredCache.get(f.module).get(dm[1]).has(dm[2])) {
            problems.push({
                check: "design-system",
                where: at(dm.index),
                message: `${dm[1]}.${dm[2]} is referenced but never declared`,
            });
        }
    }
}

// ── Report ──────────────────────────────────────────────────────────────────────────────────────
const modulesFound = MODULES.filter((m) => existsSync(join(IOS, m.name))).map((m) => m.name);

console.log(
    `ios-symbols: ${counts.files} swift file(s) across ${modulesFound.length} module(s) [${modulesFound.join(", ")}], ` +
        `${counts.enums} enum(s), ${counts.switches} anchored default-less switch(es) ` +
        `(${counts.skipped} not anchored, deliberately undecided), ` +
        `${counts.l10n} L10n reference(s), ${counts.keys} catalog key(s), ${counts.design} design-system reference(s)`
);

if (verbose) for (const line of anchored.sort()) console.log(`  anchored  ${line}`);

const byCheck = new Map();
for (const p of problems) byCheck.set(p.check, (byCheck.get(p.check) ?? 0) + 1);

// ANTI-VACUITY. Every "skip when unsure" rule above is also a way to read nothing and print green.
const blind = [];
if (modulesFound.length < 3) blind.push(`found only ${modulesFound.length} of 3 modules under ${IOS}`);
if (counts.files < FLOORS.files) blind.push(`read only ${counts.files} swift file(s) (floor ${FLOORS.files})`);
if (counts.l10n < FLOORS.l10n) blind.push(`found only ${counts.l10n} L10n reference(s) (floor ${FLOORS.l10n})`);
if (counts.keys < FLOORS.keys) blind.push(`checked only ${counts.keys} catalog key(s) (floor ${FLOORS.keys})`);
if (counts.design < FLOORS.design) blind.push(`found only ${counts.design} design-system reference(s) (floor ${FLOORS.design})`);
if (counts.switches < FLOORS.switches) blind.push(`anchored only ${counts.switches} switch(es) (floor ${FLOORS.switches})`);

if (problems.length > 0) {
    console.error("");
    for (const p of problems.sort((a, b) => a.check.localeCompare(b.check) || a.where.localeCompare(b.where))) {
        console.error(`  ${p.check.padEnd(18)} ${p.where}  ${p.message}`);
    }
    console.error(`\nios-symbols: ${problems.length} problem(s)`);
    for (const [check, count] of [...byCheck].sort((a, b) => b[1] - a[1])) {
        console.error(`  ${String(count).padStart(4)}  ${check}`);
    }
}

for (const b of blind) console.error(`ios-symbols REACH: ${b} — the reader is broken, not the tree`);

if (problems.length === 0 && blind.length === 0) console.log("\nios-symbols: clean");

// A REACH failure exits 1 EVEN UNDER `--warn`, the shape check-catalog-claims settled on
// (`agents/process/enforcement.md`): `--warn` downgrades findings ABOUT THE TREE, which is what a
// rollout needs, but a run that did not read the tree has no findings to downgrade. Letting it exit 0
// would let the only iOS signal on a Windows machine report clean while blind, which is the exact lie
// that let two broken builds through.
process.exit(blind.length > 0 || (!warnOnly && problems.length > 0) ? 1 : 0);
