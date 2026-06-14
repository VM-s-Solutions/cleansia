#!/usr/bin/env node
/**
 * Cleansia consistency checker — project-specific rules that no off-the-shelf linter covers.
 *
 * Enforces the rules in agents/knowledge/consistency.md (sections A/B backend, C/D frontend,
 * E mobile) by line-scanning source files. Prints `file:line  RULE  message` per violation and
 * exits 1 if any are found. Dependency-free Node (works on Windows dev boxes AND ubuntu CI — the
 * repo already requires Node 22 for the frontend build).
 *
 * Usage:
 *   node agents/tools/check-consistency.mjs                 # all stacks
 *   node agents/tools/check-consistency.mjs backend         # one stack: backend|frontend|mobile
 *   node agents/tools/check-consistency.mjs --warn          # report but exit 0 (use during rollout)
 *   node agents/tools/check-consistency.mjs --paths a,b     # only scan these dirs (e.g. a diff)
 *
 * These are heuristic, line-based checks: a clean run is necessary, not sufficient — the Reviewer
 * still reads the diff. Intended to graduate into backend-ci.yml / frontend-ci.yml once the existing
 * violations in agents/backlog/audits/consistency-violations.md are cleared.
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const REPO = join(fileURLToPath(import.meta.url), "..", "..", ".."); // agents/tools -> repo root
const args = process.argv.slice(2);
const warnOnly = args.includes("--warn");
const stacks = args.filter((a) =>
    ["backend", "frontend", "mobile"].includes(a),
);
const pathsArg = (args.find((a) => a.startsWith("--paths=")) || "").split(
    "=",
)[1];
const onlyStacks = stacks.length ? stacks : ["backend", "frontend", "mobile"];

const violations = [];
const add = (file, line, rule, msg) =>
    violations.push(
        `${relative(REPO, file).split(sep).join("/")}:${line}  ${rule}  ${msg}`,
    );

function walk(
    dir,
    exts,
    skip = /[\\/](node_modules|dist|bin|obj|build|generated|\.angular|\.git)[\\/]/,
) {
    const out = [];
    let entries;
    try {
        entries = readdirSync(dir);
    } catch {
        return out;
    }
    for (const e of entries) {
        const p = join(dir, e);
        if (skip.test(p + sep)) continue;
        let st;
        try {
            st = statSync(p);
        } catch {
            continue;
        }
        if (st.isDirectory()) out.push(...walk(p, exts, skip));
        else if (exts.some((x) => p.endsWith(x))) out.push(p);
    }
    return out;
}
const read = (f) => {
    try {
        return readFileSync(f, "utf8").split(/\r?\n/);
    } catch {
        return [];
    }
};
const dir = (rel) => join(REPO, rel);

// The enclosing C# method/local-function name for a 0-based line index, or "" if none found.
// Walks backwards to the nearest `<modifiers> <returnType> <Name>(` signature, skipping the
// generic-suffix `> Name(` case. Heuristic, sufficient for the dispute-guard allowlist below.
function enclosingMethod(lines, idx) {
    const sig = /^\s*(?:public|private|protected|internal|static|async|override|virtual|sealed|\s)+[\w.<>\[\],?]+\s+(\w+)\s*\(/;
    for (let i = idx; i >= 0; i--) {
        const m = lines[i].match(sig);
        if (m && m[1] !== "if" && m[1] !== "while" && m[1] !== "for" &&
            m[1] !== "switch" && m[1] !== "foreach" && m[1] !== "catch")
            return m[1];
    }
    return "";
}

// B10 — the sanctioned writers of the Dispute terminal state-machine (ADR-0006 D4).
// A direct Dispute.Close/Escalate/Resolve outside these bypasses CanTransitionTo and can force an
// illegal terminal overwrite (e.g. Closed→Resolved on a late Stripe event). Keyed by enclosing
// method name; the ResolveDispute.Handle path is additionally pinned to its file basename.
//   - UpdateStatus            : the guarded in-app routing method itself (Dispute.cs)
//   - Handle (ResolveDispute) : owns the Resolve money-path; gates on IsTerminal at the seam
//   - ReflectChargebackStatus : webhook reflector; gates on CanTransitionTo/IsTerminal itself
//   - HandleChargeback        : webhook creator; Escalates a freshly-built Pending dispute
//                               (Pending→Escalated is a legal edge) before persisting it
const DISPUTE_WRITE_ALLOW = new Set([
    "UpdateStatus",
    "ReflectChargebackStatus",
    "HandleChargeback",
]);
const DISPUTE_WRITE_ALLOW_HANDLE_FILES = new Set(["ResolveDispute.cs"]);

// B10 matches .Close/.Escalate/.Resolve( on ANY receiver (a Dispute can be bound to any local name,
// e.g. `existing`/`d`), so the same method names on unrelated types must be excluded explicitly
// rather than relying on an allow-only `dispute.` token. Excluded receivers:
//   - period / payPeriod  : PayPeriod.Close (PayPeriodBackgroundService)
//   - FiscalSequenceScope : static FiscalSequenceScope.Resolve (numbering)
//   - *Resolver           : DI resolver services' .Resolve (e.g. fiscalServiceResolver.Resolve)
const DISPUTE_WRITE_RECEIVER_EXCLUDE = new Set([
    "period",
    "payPeriod",
    "FiscalSequenceScope",
]);
const DISPUTE_WRITE_RECEIVER_EXCLUDE_RE = /Resolver$/;

// ---------------------------------------------------------------------------- BACKEND (A, B)
function checkBackend(roots) {
    const files = roots.flatMap((r) => walk(dir(r), [".cs"]));
    for (const f of files) {
        const lines = read(f);
        const text = lines.join("\n");
        // B5 — Error code first arg must be a field, never nameof(Command)/nameof(request).
        // `new Error(` often wraps to the next line, so match across a small window per occurrence.
        {
            const re =
                /new Error\(\s*nameof\(\s*(Command|request|query|command)\s*\)/g;
            let m;
            while ((m = re.exec(text)) !== null) {
                const lineNo = text.slice(0, m.index).split("\n").length;
                add(
                    f,
                    lineNo,
                    "B5",
                    "Error code uses nameof(Command/request) — use nameof(command.<Field>)",
                );
            }
        }
        // B10 runs over its own (wider) roots — see checkDisputeWrites — not the general A/B loop.
        lines.forEach((ln, i) => {
            const n = i + 1;
            // B1 — command must not return a raw scalar; wrap it in a Response record.
            // (Bare `ICommand` with no payload is allowed for operations with nothing to return —
            //  delete/toggle/status-change — so we only flag the scalar-return anti-pattern here.)
            if (
                /:\s*ICommand<\s*(string|int|long|bool|Guid|decimal)\s*>/.test(
                    ln,
                )
            )
                add(
                    f,
                    n,
                    "B1",
                    "Command returns a raw scalar — wrap it in a Response record",
                );
            // B3 — validator must inherit AbstractValidator
            const vb = ln.match(/class\s+Validator\s*:\s*(\w+)</);
            if (vb && vb[1] !== "AbstractValidator")
                add(
                    f,
                    n,
                    "B3",
                    `Validator inherits ${vb[1]} — use AbstractValidator<Command> + composed rules`,
                );
            // convention — no `dynamic`
            if (
                /(^|[^\w])dynamic([^\w]|$)/.test(ln) &&
                !ln.trim().startsWith("//")
            )
                add(f, n, "conv", "`dynamic` is banned — use a real type");
            // B1 naming trap — a record implementing ICommand must be named/suffixed Command
            const rec = ln.match(/public\s+record\s+(\w+)\s*\(/);
            if (rec) {
                const window = lines.slice(i, i + 4).join(" ");
                if (
                    /:\s*ICommand/.test(window) &&
                    rec[1] !== "Command" &&
                    !/Command$/.test(rec[1])
                )
                    add(
                        f,
                        n,
                        "B1",
                        `Command record '${rec[1]}' should end in 'Command' (UoW commits on the suffix)`,
                    );
            }
        });
        // A1/A5 — paged queries
        if (/IRequest<\s*PagedData</.test(text)) {
            if (!/:\s*DataRangeRequest/.test(text)) {
                const n =
                    lines.findIndex((l) => /IRequest<\s*PagedData</.test(l)) +
                    1;
                add(
                    f,
                    n || 1,
                    "A1",
                    "Paged query (PagedData<T>) but Request does not inherit DataRangeRequest",
                );
            }
            if (/new PagedData</.test(text)) {
                const n = lines.findIndex((l) => /new PagedData</.test(l)) + 1;
                add(
                    f,
                    n || 1,
                    "A5",
                    "Hand-built `new PagedData<T>` — return via items.MapToDto(total, request)",
                );
            }
        }
    }
    return files.length;
}

// B10 — direct Dispute terminal-state write outside the transition-guard allowlist. Scans the
// domain/handler call sites (not just Features/**): the unguarded public Close/Escalate/Resolve live
// on Dispute itself (Core.Domain/Disputes), and a direct caller can also sit in AppServices/Services
// or any other handler dir. Matches .Close/.Escalate/.Resolve( on ANY receiver, excluding the known
// non-Dispute receivers, then allowlists the sanctioned writers by enclosing method (ADR-0006 D4).
function checkDisputeWrites(roots) {
    const files = roots.flatMap((r) => walk(dir(r), [".cs"]));
    for (const f of files) {
        const lines = read(f);
        const text = lines.join("\n");
        const base = f.split(/[\\/]/).pop();
        const re = /\b(\w+)\.(Close|Escalate|Resolve)\s*\(/g;
        let m;
        while ((m = re.exec(text)) !== null) {
            const receiver = m[1];
            if (
                DISPUTE_WRITE_RECEIVER_EXCLUDE.has(receiver) ||
                DISPUTE_WRITE_RECEIVER_EXCLUDE_RE.test(receiver)
            )
                continue;
            const lineNo = text.slice(0, m.index).split("\n").length;
            const method = enclosingMethod(lines, lineNo - 1);
            const allowed =
                DISPUTE_WRITE_ALLOW.has(method) ||
                (method === "Handle" &&
                    DISPUTE_WRITE_ALLOW_HANDLE_FILES.has(base));
            if (!allowed)
                add(
                    f,
                    lineNo,
                    "B10",
                    "direct Dispute state-write bypasses the T-0172 transition guard; route through CanTransitionTo/UpdateStatus or the sanctioned webhook path",
                );
        }
    }
    return files.length;
}

// ---------------------------------------------------------------------------- FRONTEND (C, D)
function checkFrontend(roots) {
    const all = roots.flatMap((r) => walk(dir(r), [".ts"]));
    const facades = all.filter((f) => f.endsWith(".facade.ts"));
    const components = all.filter((f) => f.endsWith(".component.ts"));
    for (const f of facades) {
        const lines = read(f);
        const text = lines.join("\n");
        if (!/extends\s+UnsubscribeControlDirective/.test(text))
            add(
                f,
                1,
                "C1",
                "Facade does not extend UnsubscribeControlDirective",
            );
        lines.forEach((ln, i) => {
            const n = i + 1;
            if (/takeUntilDestroyed|inject\(\s*DestroyRef\s*\)/.test(ln))
                add(
                    f,
                    n,
                    "C1",
                    "Uses DestroyRef/takeUntilDestroyed — standardize on UnsubscribeControlDirective",
                );
            if (/new\s+BehaviorSubject</.test(ln))
                add(f, n, "C2", "State uses BehaviorSubject — use signal<T>()");
            if (/\.subscribe\(/.test(ln)) {
                // Walk back to the start of this pipe chain (the line that opens `.pipe(` or the call)
                // and check the whole chain for takeUntil — pipes here span many lines (catchError/finalize).
                let start = i;
                while (
                    start > 0 &&
                    !/\b\w+\$?\s*\n?\s*\.pipe\(|\.pipe\(/.test(lines[start]) &&
                    i - start < 25
                ) {
                    if (/\.pipe\(/.test(lines[start])) break;
                    start--;
                }
                const w = lines.slice(Math.max(0, start - 1), i + 1).join(" ");
                if (
                    /\.pipe\(/.test(w) &&
                    !/takeUntil\(\s*this\.destroyed\$\s*\)/.test(w)
                )
                    add(
                        f,
                        n,
                        "C3",
                        ".subscribe() pipe has no takeUntil(this.destroyed$)",
                    );
                // a .subscribe with no .pipe at all in range is also a leak risk
                else if (!/\.pipe\(/.test(w))
                    add(
                        f,
                        n,
                        "C3",
                        ".subscribe() with no .pipe(takeUntil(this.destroyed$))",
                    );
            }
        });
    }
    for (const f of components) {
        const lines = read(f);
        const text = lines.join("\n");
        if (
            /@Component\(/.test(text) &&
            !/ChangeDetectionStrategy\.OnPush/.test(text)
        )
            add(f, 1, "C7", "Component is not OnPush");
        if (/form\.component\.ts$/.test(f))
            lines.forEach((ln, i) => {
                if (/\bfb\.group\(/.test(ln) && !/nonNullable/.test(ln))
                    add(
                        f,
                        i + 1,
                        "D2",
                        "fb.group(...) in a form — prefer fb.nonNullable.group(...)",
                    );
            });
    }
    // no `any` in feature TS (skip specs + generated client)
    for (const f of all) {
        if (/\.spec\.ts$/.test(f) || /[\\/]client[\\/]/.test(f)) continue;
        read(f).forEach((ln, i) => {
            if (/:\s*any(\b|\[)/.test(ln) && !/eslint-disable/.test(ln))
                add(
                    f,
                    i + 1,
                    "conv",
                    "': any' type — use a real type (generated DTO / interface)",
                );
        });
    }
    return all.length;
}

// ---------------------------------------------------------------------------- MOBILE (E)
function checkMobile(roots) {
    const files = roots.flatMap((r) => walk(dir(r), [".kt"]));
    for (const f of files) {
        const lines = read(f);
        const text = lines.join("\n");
        lines.forEach((ln, i) => {
            const n = i + 1;
            if (/data class\s+\w*UiState\b/.test(ln))
                add(
                    f,
                    n,
                    "E1",
                    "UiState is a data class (flag-bag) — use a sealed interface (Loading/Error/Loaded)",
                );
            // E6 — only flag collectAsState() on a *ViewModel* flow; it's legitimate for purely-local
            // component state (a sheet's own mutableStateOf), which doesn't need lifecycle awareness.
            if (/\b(viewModel|vm)\.\w[\w.]*\.collectAsState\(\)/.test(ln))
                add(
                    f,
                    n,
                    "E6",
                    "viewModel flow uses collectAsState() — use collectAsStateWithLifecycle()",
                );
            if (/Text\(\s*"[^"]+"/.test(ln) && !/stringResource/.test(ln))
                add(
                    f,
                    n,
                    "conv",
                    "Hardcoded string in Text(...) — use stringResource(R.string.x)",
                );
        });
        if (/Repository(Impl)?\.kt$/.test(f))
            lines.forEach((ln, i) => {
                if (
                    /suspend fun .*\)\s*:\s*[A-Za-z0-9_<>]+\?\s*$/.test(ln) &&
                    !/ApiResult|Flow|Unit/.test(ln)
                )
                    add(
                        f,
                        i + 1,
                        "E5",
                        "Repository returns a nullable body (legacy) — prefer ApiResult<T> (tracked migration)",
                    );
            });
        if (
            /ViewModel\.kt$/.test(f) &&
            /class\s+\w*ViewModel/.test(text) &&
            !/@HiltViewModel/.test(text)
        )
            add(f, 1, "E3", "ViewModel is not annotated @HiltViewModel");
    }
    return files.length;
}

// ---------------------------------------------------------------------------- run
const DEFAULTS = {
    backend: ["src/Cleansia.Core.AppServices/Features"],
    // B10 scans the dispute call sites wherever they live: the unguarded domain methods
    // (Core.Domain/Disputes) plus the handler/service dirs that can call them directly.
    disputeWrites: [
        "src/Cleansia.Core.AppServices/Features",
        "src/Cleansia.Core.AppServices/Services",
        "src/Cleansia.Core.Domain/Disputes",
    ],
    frontend: ["src/Cleansia.App/libs"],
    mobile: ["src/cleansia_android"],
};
const custom = pathsArg ? pathsArg.split(",") : null;
let scanned = 0;
if (onlyStacks.includes("backend")) {
    scanned += checkBackend(custom || DEFAULTS.backend);
    checkDisputeWrites(custom || DEFAULTS.disputeWrites);
}
if (onlyStacks.includes("frontend"))
    scanned += checkFrontend(custom || DEFAULTS.frontend);
if (onlyStacks.includes("mobile"))
    scanned += checkMobile(custom || DEFAULTS.mobile);

if (violations.length === 0) {
    console.log(
        `consistency: OK (${scanned} files scanned, stacks: ${onlyStacks.join(", ")})`,
    );
    process.exit(0);
}
console.log(`consistency: ${violations.length} violation(s)`);
for (const v of violations.sort()) console.log("  " + v);
process.exit(warnOnly ? 0 : 1);

