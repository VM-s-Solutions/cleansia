#!/usr/bin/env node
/**
 * Cleansia consistency checker — project-specific rules that no off-the-shelf linter covers.
 *
 * Enforces the rules in agents/knowledge/consistency.md (sections A/B backend, C/D frontend,
 * E mobile) and the F web-surface rules by line-scanning source files. The F-rules are stated in
 * this file alone — each rule's comment is its statement — until consistency.md carries their
 * rows. Prints `file:line  RULE  message` per violation and exits 1 if any are found.
 * Dependency-free Node (works on Windows dev boxes AND ubuntu CI — the repo already requires
 * Node 22 for the frontend build).
 *
 * Usage:
 *   node agents/tools/check-consistency.mjs                 # all stacks
 *   node agents/tools/check-consistency.mjs backend         # one stack: backend|frontend|mobile
 *   node agents/tools/check-consistency.mjs --warn          # report but exit 0 (use during rollout)
 *   node agents/tools/check-consistency.mjs --paths a,b     # only scan these dirs (e.g. a diff)
 *
 * These are heuristic, line-based checks: a clean run is necessary, not sufficient — the Reviewer
 * still reads the diff. Intended to graduate into backend-ci.yml / frontend-ci.yml once the existing
 * violations declared in agents/cleanup/consistency-baseline.md are cleared.
 *
 * What CI runs and what is local. No workflow runs this file: it is the Reviewer's on-demand pass
 * (agents/process/enforcement.md). The web rules that CI does execute are the jest guard specs
 * under src/Cleansia.App/apps/<app>/src/app/theme/*.spec.ts — page shell, list, detail, form and
 * dialog shapes, the feedback idioms, the shared primitives (status badge, date, money), the filter
 * drawer and the font stack — which frontend-ci.yml runs through `nx affected -t test`. The F-rules
 * below are the line-scannable complement to those specs: the same conventions, over every
 * template, stylesheet and locale bundle, without a jest boot. A rule whose default-root count is
 * zero is a hard gate (`add`); one that still has sites is advisory (`warn`) and names its measured
 * count so the next sweep can flip it once the count reaches zero — the advisory summary line
 * prints that count per rule, so the baseline is read from the run, not from the comment.
 */
import { readFileSync, readdirSync, statSync } from "node:fs";
import { isAbsolute, join, relative, resolve, sep } from "node:path";
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

// Advisory (warn-only) findings — heuristics that can't be a hard gate (e.g. E9, which needs a
// type-graph the line-scanner lacks). These NEVER set the exit code; they print so the Reviewer looks.
const advisories = [];
const advisoryCounts = new Map();
const warn = (file, line, rule, msg) => {
    advisories.push(
        `${relative(REPO, file).split(sep).join("/")}:${line}  ${rule}  ${msg}`,
    );
    advisoryCounts.set(rule, (advisoryCounts.get(rule) ?? 0) + 1);
};

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
// Accepts repo-relative or absolute paths. Agents are instructed to pass absolute paths, and
// join(REPO, "/abs/path") silently yields a directory that cannot exist — which walked to nothing
// and reported "OK (0 files scanned)". A checker that reports a pass for a path it never read is
// worse than no checker.
const dir = (rel) => (isAbsolute(rel) ? resolve(rel) : join(REPO, rel));

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
// HandleChargeback (the webhook creator) is intentionally NOT allowlisted: it now routes its new
// dispute's escalation through dispute.UpdateStatus(Escalated) (the guard), so it makes no direct
// Close/Escalate/Resolve call for the rule to flag — the rule enforces that funnel going forward.
const DISPUTE_WRITE_ALLOW = new Set([
    "UpdateStatus",
    "ReflectChargebackStatus",
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
            // B3 — validator must inherit AbstractValidator.
            //
            // NARROWED 2026-08-14, after the rule was found to be flagging a security control and two
            // kinds of nothing. The 21 sites it fired on were three populations:
            //
            //   BaseAuthValidator / BaseUserValidator — declare NO rules in a constructor, only
            //     `protected void AddEmailRules(...)` helpers the derived class calls explicitly. The
            //     rules land exactly as if written inline, so the flag was about the `: Base…` token
            //     and nothing observable.
            //   LoginValidator — its rule ORDER is the point ("Cascade.Stop so a locked account never
            //     evaluates the password"). Composing it away would remove a deliberate gate.
            //   UserEmailValidator — its constructor declares a rule that re-checks the caller against
            //     the database on every request. That is load-bearing: the three WEB hosts install no
            //     revocation directory (UserRevocationWiringPinTests pins that they must not), a
            //     Partner access token lives 1440 minutes, and GDPR erasure rewrites User.Email to
            //     deleted_{id}@anonymized.local — so this lookup is what stops an erased or
            //     unconfirmed principal acting on a still-valid token. Owner confirmed the intent on
            //     2026-08-14.
            //
            // A validator inheriting SOMETHING ELSE is still worth a look, so the rule survives with an
            // exemption list rather than being deleted. Add to it only with the same kind of reason.
            const VALIDATOR_BASE_EXEMPT = new Set([
                "AbstractValidator",
                "BaseAuthValidator",
                "BaseUserValidator",
                "LoginValidator",
                "UserEmailValidator",
            ]);
            const vb = ln.match(/class\s+Validator\s*:\s*(\w+)</);
            if (vb && !VALIDATOR_BASE_EXEMPT.has(vb[1]))
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
                // Bound the window at THIS record's own declaration end. A flat 4 lines bled into the
                // NEXT record: `public record Request(...)` immediately followed by
                // `public record Command(...) : ICommand<Response>` made the HTTP body DTO look like a
                // mis-named command (ApproveEmployee, RejectEmployee).
                const slice = [];
                for (let k = i; k < Math.min(lines.length, i + 4); k++) {
                    if (k > i && /public\s+record\s+\w+/.test(lines[k])) break;
                    slice.push(lines[k]);
                    if (/;\s*$/.test(lines[k])) break;
                }
                const window = slice.join(" ");
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
        // A file that never names the type cannot write a Dispute's state. Without this the rule fires
        // on any `X.Resolve(` anywhere — it was reporting TimeZoneResolution.Resolve(...) in
        // BenefitPeriodKeyFactory and GetDashboardStats, neither of which contains the token `Dispute`.
        // Costs no sensitivity: reaching a Dispute instance requires naming the type or a
        // Dispute-named repository/property somewhere in the same file.
        if (!/\bDispute/.test(text)) continue;
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

// E9 — session-wipe-set membership (security-rules.md S11 / consistency.md E9).
// A per-user @Singleton cache MUST implement SessionScopedCache (Android) so it is flushed on
// sign-out / forced-401 / account-deletion; leaving one out leaks the prior user's data to the next
// account on a shared device. A full "is this @Singleton per-user?" decision needs Kotlin type-graph
// resolution this line-scanner can't do (see enforcement.md) — so this is a WARN-only heuristic:
// flag a @Singleton class that declares a *cache field* (StateFlow / DataStore / Staleness watermark)
// but does NOT list SessionScopedCache on its class declaration, unless it is on the reason-annotated
// allowlist below. This is non-blocking: a Room-DAO-backed (or otherwise field-invisible) per-user cache
// slips past it, so it prompts the Reviewer, it does not gate. The HARD gate is a roster-equality
// assertion test (SessionScopedModuleTest / SessionScopedCacheRegistryTest) — SPECIFIED, not yet built
// (enforcement.md). This SESSION_WIPE_ALLOW mirrors the consistency.md E9 allowlist — keep them in sync.
// Keyed by class name; each entry states WHY it is not per-user.
const SESSION_WIPE_ALLOW = new Map([
    // Public / device-level caches — value is identical for every user, so nothing to leak.
    ["CatalogRepository", "public services/packages/extras catalog — anonymous-fetchable, no account data"],
    ["MarketRepository", "public market directory + the device's chosen market — anonymous-fetchable, no account data"],
    ["CustomerServiceAreaDataSource", "public serviced-countries/cities — device-level, not per-user"],
    ["PartnerServiceAreaDataSource", "public serviced-countries/cities — device-level, not per-user"],
    ["AppSettingsStore", "device UI prefs (lang/theme/onboarding); per-user onboarding keyed by userId"],
    ["AppSettingsRepository", "device UI prefs (lang/theme/onboarding); per-user onboarding keyed by userId"],
    // Transient buses / delegators — hold no retained state across a session boundary.
    ["OrderEventBus", "SharedFlow(replay=0) event bus — retains nothing after emit"],
    ["SnackbarController", "SharedFlow(replay=0) UI channel — retains nothing after emit"],
    ["PushTokenSessionObserver", "delegates to PushTokenRepository, which IS in the wipe set"],
]);
// A @Singleton is a *cache holder* if its body declares any of these (a retained per-user surface).
const CACHE_FIELD_RE = /\b(MutableStateFlow\s*<|DataStore\s*<|preferencesDataStore\b|=\s*Staleness\s*\(|ConcurrentHashMap\s*<[^>]*Staleness)/;

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
                // Teardown is often not on this chain at all. Two shapes are correct and were being
                // reported as leaks (P2, 4 of 10 C3 hits):
                //   this.someStream$.subscribe(...)      — takeUntil is on the stream's DEFINITION
                //   this.someHelper(id).subscribe(...)   — takeUntil is inside the helper's own pipe
                // Resolve the symbol in this file and accept its teardown before scanning the chain.
                const held = ln.match(/this\.(\w+\$?)\s*\(?/);
                if (held) {
                    const sym = held[1];
                    const defRe = new RegExp(
                        `(${sym}\\s*[:=]|\\b${sym}\\s*\\()`,
                    );
                    const defIdx = lines.findIndex(
                        (l, k) => k !== i && defRe.test(l),
                    );
                    if (defIdx >= 0) {
                        const body = lines
                            .slice(defIdx, Math.min(lines.length, defIdx + 25))
                            .join(" ");
                        if (
                            /takeUntil\(\s*this\.destroyed\$\s*\)/.test(body)
                        )
                            return;
                    }
                }
                // Walk back to the start of this pipe chain (the line that opens `.pipe(` or the call)
                // and check the whole chain for takeUntil — pipes here span many lines (catchError/
                // finalize). The bound was 25 and a real admin pipe measured 33, so the chain that
                // DID carry takeUntil was reported as though it carried none.
                let start = i;
                while (
                    start > 0 &&
                    !/\b\w+\$?\s*\n?\s*\.pipe\(|\.pipe\(/.test(lines[start]) &&
                    i - start < 60
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
    //
    // ControlValueAccessor is exempt, and it has to be: Angular DECLARES those members with `any` —
    // `writeValue(obj: any)`, `registerOnChange(fn: any)`, `registerOnTouched(fn: any)`. A narrower
    // type does not implement the interface. Every `: any` this rule reported in the design system was
    // one of these, so the rule was asking for code that will not compile.
    const CVA_ANY =
        /\b(writeValue|registerOnChange|registerOnTouched|onChange|onTouch|onTouched)\b/;
    // Angular: type TrackByFunction<T> = (index: number, item: T) => any;
    const FRAMEWORK_ANY = /\btrackBy\w*\s*\(/i;
    for (const f of all) {
        if (/\.spec\.ts$/.test(f) || /[\\/]client[\\/]/.test(f)) continue;
        const body = read(f).join("\n");
        // A component that EXTENDS the shared CVA base never names the interface itself, so an
        // `override writeValue(...)` is the tell. TrackByFunction<T> is likewise declared
        // `(index: number, item: T) => any` by Angular, so a trackBy's return type cannot be narrowed.
        const implementsCva =
            /ControlValueAccessor/.test(body) ||
            /override\s+(writeValue|registerOnChange|registerOnTouched)\b/.test(
                body,
            );
        // A file-level `/* eslint-disable @typescript-eslint/no-explicit-any */` silences ESLint for
        // the whole file. This rule has to honour it, or it contradicts the linter the repo already
        // runs and asks for a change ESLint has been told not to want.
        const fileDisablesAny =
            /eslint-disable\b[^\n]*@typescript-eslint\/no-explicit-any/.test(body);
        const src = read(f);
        src.forEach((ln, i) => {
            if (implementsCva && CVA_ANY.test(ln)) return;
            if (FRAMEWORK_ANY.test(ln)) return;
            if (fileDisablesAny) return;
            if (/eslint-disable/.test(ln)) return;
            // The disable that matters is almost always on the PREVIOUS line, because
            // `eslint-disable-next-line` is the idiom. Checking only the current line meant this rule
            // reported error.codes.ts — a deliberate exception carrying both a paragraph explaining
            // why `any` is required there (bivariant assignment into the handler map, which
            // `unknown` blocks) and the disable directive itself. A convention checker that reports
            // documented, linter-sanctioned exceptions teaches people to skim past it.
            if (i > 0 && /eslint-disable-next-line/.test(src[i - 1])) return;
            if (/:\s*any(\b|\[)/.test(ln))
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

// E1 support — is this UiState a PHASE BAG, i.e. does it model mutually exclusive states as
// independent flags that can contradict each other?
//
// The rule was `/data class \w*UiState\b/` with no qualification. Measured against the tree on
// 2026-08-28 that was nine hits, of which ONE was defensible. In three of the eight the prescribed
// cure — a Loading/Error/Loaded union — would have deleted a distinction the code documents at
// length. A rule wrong eight times in nine does not get obeyed, it gets skimmed past, and then it
// protects nothing. Each exemption below names the case that forced it, so the next person can tell
// a considered narrowing from a convenient one.
const isUiStatePhaseBag = (lines, at) => {
    // The parameter list spans many lines in the real cases, so take it by matching parentheses.
    const text = lines.slice(at, at + 80).join("\n");
    const open = text.indexOf("(");
    if (open < 0) return false;
    let depth = 0;
    let close = -1;
    for (let k = open; k < text.length; k++) {
        if (text[k] === "(") depth++;
        else if (text[k] === ")" && --depth === 0) {
            close = k;
            break;
        }
    }
    if (close < 0) return false;
    const body = text
        .slice(open + 1, close)
        .replace(/\/\*[\s\S]*?\*\//g, "")
        .replace(/\/\/[^\n]*/g, "");
    const fields = [
        ...body.matchAll(/\bval\s+(\w+)\s*:\s*([\w<>?., ]+?)\s*(?:=|,|$)/g),
    ].map((m) => ({ name: m[1], type: m[2].trim() }));
    if (!fields.length) return false;

    // (a) One phase signal has nothing to contradict. SettingsUiState is a lone
    //     `isSignedOut: Boolean`; a union over it would be ceremony, not clarity.
    //
    //     Fields are counted by NAME as well as by type, and that over-counts in one direction: a
    //     field called `outcome` whose TYPE is already a sealed class IS the phase union this rule
    //     asks for, not a flag standing in for one. AuthUiState(loading, outcome: AuthOutcome?)
    //     was flagged on exactly that basis while holding the shape E1 prescribes — and the cure it
    //     named, Loading/Error/Loaded, would have had to invent an Error case, because that screen
    //     reports failures over a snackbar and never puts one in state. Sealed types declared
    //     anywhere in the file are subtracted before the count; a Boolean beside one still counts,
    //     so a genuine flag-bag that happens to share a file with a union is still caught.
    const sealedTypes = new Set(
        [...lines.join("\n").matchAll(/\bsealed\s+(?:class|interface)\s+(\w+)/g)].map((m) => m[1]),
    );
    const phase = fields.filter(
        (x) =>
            !sealedTypes.has(x.type.replace(/\?$/, "").trim()) &&
            (x.type === "Boolean" || /error|outcome|status/i.test(x.name)),
    );
    if (phase.length < 2) return false;

    // (b) Two or more CONCURRENT in-flight signals are deliberately distinct, and one `Loading` case
    //     erases them. InvoicesListUiState and RegistrationLockUiState each carry a paragraph saying
    //     the pull-to-refresh indicator must never subscribe to the background refresh — collapsing
    //     the two is precisely the bug those comments were written to prevent. `has*` is excluded:
    //     hasLoadedOnce records the past, it is not something in flight.
    const inFlight = fields.filter(
        (x) =>
            x.type === "Boolean" &&
            !/^has/i.test(x.name) &&
            /load|refresh|saving|sending|submit|report|process/i.test(x.name),
    );
    if (inFlight.length >= 2) return false;

    // (c) Per-field validation errors are FORM state and a phase union has nowhere to keep them —
    //     the user goes on typing while a request is in flight. RegisterUiState carries six. The
    //     diagnosis "too many flags" may still be fair there; the prescribed cure is not, and a rule
    //     should not name a fix that does not fit.
    if (fields.filter((x) => /error/i.test(x.name)).length >= 2) return false;

    return true;
};

// ---------------------------------------------------------------------------- MOBILE (E)
function checkMobile(roots) {
    const files = roots.flatMap((r) => walk(dir(r), [".kt"]));
    for (const f of files) {
        const lines = read(f);
        const text = lines.join("\n");
        // E10 — every HttpLoggingInterceptor construction must redact the Authorization header,
        // or a DEBUG/HEADERS build logs live bearer tokens to logcat. File-level: the redactHeader
        // call rides the same .apply block as the constructor.
        if (/HttpLoggingInterceptor\s*\(/.test(text) &&
            !/redactHeader\(\s*"Authorization"\s*\)/.test(text)) {
            const n = lines.findIndex((l) => /HttpLoggingInterceptor\s*\(/.test(l)) + 1;
            add(
                f,
                n,
                "E10",
                'HttpLoggingInterceptor without redactHeader("Authorization") — a HEADERS-level build logs bearer tokens',
            );
        }
        lines.forEach((ln, i) => {
            const n = i + 1;
            if (
                /data class\s+\w*UiState\b/.test(ln) &&
                isUiStatePhaseBag(lines, i)
            )
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
            // `\w+Text(` excludes builders that merely END in Text — newPlainText("referral_code", …)
            // is a clipboard LABEL, not a rendered string, and was the only hit this rule produced.
            if (
                /(^|[^.\w])Text\(\s*"[^"]+"/.test(ln) &&
                !/stringResource/.test(ln)
            )
                add(
                    f,
                    n,
                    "conv",
                    "Hardcoded string in Text(...) — use stringResource(R.string.x)",
                );
        });
        if (/Repository(Impl)?\.kt$/.test(f))
            lines.forEach((ln, i) => {
                // ApiError joins the exclusions. ADR-0011's three harms are a discarded typed
                // error, a snackbar in the data layer, and failure colliding with empty success.
                // `suspend fun refresh(...): ApiError?` commits none of them: the typed error IS
                // the return, the snackbar is in the ViewModel, and null cannot be mistaken for an
                // empty body because there is no body on this channel — the data leaves over a
                // StateFlow. The regex listed ApiResult|Flow|Unit and simply never named this one.
                if (
                    /suspend fun .*\)\s*:\s*[A-Za-z0-9_<>]+\?\s*$/.test(ln) &&
                    !/ApiResult|ApiError|Flow|Unit/.test(ln)
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

        // E9 (WARN-only) — a @Singleton cache holder that isn't in the session-wipe set (S11).
        // Find each `@Singleton` and the next `class <Name>` declaration; read that class's body up
        // to the next top-level `class`/EOF; if it declares a cache field but its declaration line(s)
        // don't name SessionScopedCache and it's not allowlisted, warn. ViewModels are exempt (they
        // are not @Singleton and hold no cross-session cache). See enforcement.md for why WARN-only.
        for (let i = 0; i < lines.length; i++) {
            if (!/^\s*@Singleton\b/.test(lines[i])) continue;
            // find the class declaration following the annotation (skip other annotations/blank lines)
            let d = i + 1;
            while (d < lines.length && !/\bclass\s+\w+/.test(lines[d]) && d - i < 8) d++;
            const decl = lines[d] || "";
            const nameM = decl.match(/\bclass\s+(\w+)/);
            if (!nameM) continue;
            const className = nameM[1];
            // the declaration may wrap over several lines before the `{` opening the body
            let openIdx = d;
            while (openIdx < lines.length && !/\{/.test(lines[openIdx]) && openIdx - d < 10) openIdx++;
            const declText = lines.slice(d, openIdx + 1).join(" ");
            if (/\bSessionScopedCache\b/.test(declText)) continue; // already a member
            if (SESSION_WIPE_ALLOW.has(className)) continue; // reason-annotated exclusion
            // scan the class body (until the next @Singleton or a top-level `class ` at column 0)
            let end = openIdx + 1;
            while (
                end < lines.length &&
                !/^@Singleton\b/.test(lines[end]) &&
                !/^(?:internal |private |public |abstract |sealed |data )*class\s+\w/.test(lines[end])
            )
                end++;
            const body = lines.slice(openIdx, end).join("\n");
            if (CACHE_FIELD_RE.test(body))
                warn(
                    f,
                    d + 1,
                    "E9",
                    `@Singleton '${className}' holds a cache field but is not in the SessionScopedCache wipe set and not on the consistency.md E9 allowlist — confirm it is per-user (join the set) or add a reason-annotated allowlist entry (S11)`,
                );
        }
    }
    return files.length;
}

// ---------------------------------------------------------------------------- FRONTEND SURFACE (F)
// The admin and partner web surface: templates, stylesheets and locale bundles. The customer app is
// outside every F-rule — it had its own passes, carries its own shell and spacing tokens, and the
// jest guards that hold these conventions live under the admin and partner apps alone.
const F_SKIP =
    /[\\/](node_modules|dist|bin|obj|build|generated|\.angular|\.git|cleansia-customer-features|cleansia\.app|cleansia-customer)[\\/]/;
// A template or class under a feature lib or an app shell — the surface the F-rules govern. The
// shared component library is a wrapper over raw elements by design, so F1/F2/F6 stop at its edge;
// the rules on strings (F14) and teardown (F8) reach it.
const FEATURE_OR_SHELL =
    /[\\/](cleansia-(?:admin|partner)-features|apps[\\/][^\\/]+[\\/]src[\\/]app)[\\/]/;
const SHARED_COMPONENTS = /[\\/]libs[\\/]shared[\\/]components[\\/]/;
const APP_SHELL_TEMPLATE = /[\\/]apps[\\/][^\\/]+[\\/]src[\\/]app[\\/]app\.component\.html$/;
const PAGE_STYLESHEET =
    /[\\/]pages[\\/]cleansia-(?:admin|partner)[\\/]([\w-]+)\.component\.scss$/;
const I18N_DIR = /[\\/]assets[\\/]i18n$/;
const LOCALES = ["en", "cs", "sk", "uk", "ru"];
// The namespaces a bundle may carry at its top level. `common` is not one: its last reader moved
// to `global.actions.*` and a key filed under it is read by nothing.
const I18N_NAMESPACES = new Set([
    "global", "pages", "page_titles", "components", "sidebar", "api", "validation", "auth",
    "cookies", "help", "enums", "primeng",
]);
// A page template: it carries the shared header, the page card, or the auth backdrop.
const PAGE_TEMPLATE_RE =
    /class="cleansia-page-header"|class="[^"]*\b(?:page-wrapper|cleansia-page)\b|<cleansia-dynamic-background/;
// The radius scale of the design language, plus the two pill and circle idioms and the tokens.
const RADIUS_SCALE = new Set(["0", "6px", "12px", "16px", "24px", "32px", "50%", "999px", "9999px"]);
const RADIUS_TOKEN_RE = /^var\(--(?:cleansia-radius-\w+|p-[\w-]+)(?:,\s*[^)]+)?\)$/;
// The tokens every bundle declares: the shared variables plus each app's own root overrides.
const TOKEN_DECLARATION_FILES = [
    "src/Cleansia.App/libs/shared/assets/src/styles/common/variables.scss",
    "src/Cleansia.App/apps/cleansia-admin.app/src/styles.scss",
    "src/Cleansia.App/apps/cleansia-partner.app/src/styles.scss",
];
// The web tree: the feature and shared libs, and the app shells (toolbar, sidebar, config, locale
// bundles) — surface too, not only the libs. The frontend rules scan it by default, and F12 resolves
// a page's component from it whatever --paths narrows the scan to.
const WEB_ROOTS = ["src/Cleansia.App/libs", "src/Cleansia.App/apps"];

const lineOf = (text, index) => text.slice(0, index).split("\n").length;
// Every `<tag …>` opening tag in a template with its 1-based line — the attributes of a
// `<cleansia-button` span several lines, so a line-scan cannot see the tag as one thing.
function openingTags(text, tagName) {
    const out = [];
    const re = new RegExp(`<${tagName}\\b[^>]*>`, "g");
    let m;
    while ((m = re.exec(text)) !== null) out.push({ tag: m[0], line: lineOf(text, m.index) });
    return out;
}
const flatKeys = (obj, prefix = "") =>
    Object.entries(obj).flatMap(([k, v]) =>
        v !== null && typeof v === "object" && !Array.isArray(v)
            ? flatKeys(v, `${prefix}${k}.`)
            : [`${prefix}${k}`],
    );

function checkFrontendSurface(roots) {
    const files = roots.flatMap((r) => walk(dir(r), [".html", ".scss", ".json", ".ts"], F_SKIP));
    const templates = files.filter((f) => f.endsWith(".component.html"));
    const sources = files.filter((f) => f.endsWith(".ts") && !f.endsWith(".spec.ts"));
    const stylesheets = files.filter((f) => f.endsWith(".scss"));
    const bundles = files.filter((f) => I18N_DIR.test(f.slice(0, f.lastIndexOf(sep))));

    for (const f of templates) {
        const text = read(f).join("\n");
        const governed = FEATURE_OR_SHELL.test(f);
        if (governed) {
            // F1 — a raw form control where a <cleansia-*> wrapper exists. The hidden file picker is
            // the one raw input a wrapper cannot replace. ADVISORY — 4 sites on 2026-09-22 (the partner
            // dashboard's three quick-action cards and the registration-lock button); flip to `add`
            // when a default-root run reports zero.
            for (const { tag, line } of openingTags(text, "(?:button|input|select|textarea)")) {
                if (/^<input\b/.test(tag) && /type="file"/.test(tag)) continue;
                warn(f, line, "F1", `raw ${tag.match(/^<(\w+)/)[1]} in a feature template — use the <cleansia-*> wrapper`);
            }
            // F2 — a PrimeNG widget bound directly where a wrapper exists.
            for (const { tag, line } of openingTags(text, "(?:p-button|p-select|p-multiSelect|p-checkbox|p-inputNumber)"))
                add(f, line, "F2", `${tag.match(/^<([\w-]+)/)[1]} outside libs/shared/components — use the <cleansia-*> wrapper`);
            for (const m of text.matchAll(/\b(pButton|pTextarea)\b/g))
                add(f, lineOf(text, m.index), "F2", `${m[1]} directive outside libs/shared/components — use the <cleansia-*> wrapper`);
            // F6 — the legacy <cleansia-button> API: (clickFn), [title], and the two inputs bound to
            // their own default. ADVISORY — 3 sites on 2026-09-22, all on the admin login's submit;
            // flip to `add` at zero, then delete `title` and `clickFn` from the component.
            for (const { tag, line } of openingTags(text, "cleansia-button")) {
                if (/\(clickFn\)=/.test(tag)) warn(f, line, "F6", "(clickFn) on <cleansia-button> — bind (onClick)");
                if (/\[title\]=/.test(tag)) warn(f, line, "F6", "[title] on <cleansia-button> — bind [label]");
                if (/\[buttonType\]="'button'"/.test(tag)) warn(f, line, "F6", "[buttonType]=\"'button'\" is the default — drop it");
                if (/\[style\]="'raised-button'"/.test(tag)) warn(f, line, "F6", "[style]=\"'raised-button'\" is the default — drop it");
            }
            // F7 — the structural directives and two-way template binding the features left behind.
            for (const m of text.matchAll(/\*ngIf=|\*ngFor=|\[\(ngModel\)\]/g))
                add(f, lineOf(text, m.index), "F7", `${m[0]} in a feature template — use @if/@for and a reactive form`);
            // F13 — a page's first title is its h1.
            if (PAGE_TEMPLATE_RE.test(text)) {
                const [first] = openingTags(text, "cleansia-title");
                if (first && !/\[level\]="1"/.test(first.tag))
                    add(f, first.line, "F13", "the first <cleansia-title> of a page template is its h1 — bind [level]=\"1\"");
            }
        }
        // F3 — the confirmation dialog is mounted by the shell once; DialogService opens it.
        if (/<p-confirmDialog\b/i.test(text) && !APP_SHELL_TEMPLATE.test(f))
            add(f, lineOf(text, text.search(/<p-confirmDialog\b/i)), "F3", "<p-confirmDialog> outside the app shell — the root one is what DialogService opens");
        // F14 — an aria-label the user hears must come from the bundle.
        if (governed || SHARED_COMPONENTS.test(f))
            for (const m of text.matchAll(/(?<=^|\s)aria-label="([^"]*)"/g))
                if (!m[1].includes("{{"))
                    add(f, lineOf(text, m.index), "F14", `literal aria-label="${m[1]}" — bind [attr.aria-label] to a translated key`);
    }

    for (const f of sources) {
        const text = read(f).join("\n");
        const base = f.split(/[\\/]/).pop();
        if (f.endsWith(".component.ts")) {
            // F4 — a component-scoped ConfirmationService renders into no dialog; the root provides it.
            for (const m of text.matchAll(/providers:\s*\[[^\]]*\bConfirmationService\b[^\]]*\]/g))
                add(f, lineOf(text, m.index), "F4", "ConfirmationService in a component's providers — the root provides it and DialogService opens it");
            // F8 — a component with its own teardown subject or DestroyRef. ADVISORY — 27 sites on
            // 2026-09-22; flip to `add` at zero. Facades are held to this by C1.
            if (
                (FEATURE_OR_SHELL.test(f) || SHARED_COMPONENTS.test(f)) &&
                /new\s+Subject<void>\(\)|inject\(\s*DestroyRef\s*\)|takeUntilDestroyed/.test(text) &&
                !/extends\s+UnsubscribeControlDirective/.test(text)
            )
                warn(f, 1, "F8", "component owns its teardown (Subject<void>/DestroyRef) — extend UnsubscribeControlDirective");
        }
        // F5 — PrimeNG's confirm is called from one place.
        if (base !== "dialog.service.ts")
            for (const m of text.matchAll(/\bconfirmationService\.confirm\s*\(/g))
                add(f, lineOf(text, m.index), "F5", "confirmationService.confirm( outside dialog.service.ts — use DialogService.confirmTranslated/confirmDelete");
        // F15 — the error toast is the interceptor's; a per-feature key map toasts twice.
        if (FEATURE_OR_SHELL.test(f))
            for (const m of text.matchAll(/\bresolve(?!Api)\w+ErrorKey\s*\(|\b\w*_ERROR_KEY_MAP\b/g))
                add(f, lineOf(text, m.index), "F15", `${m[0].trim()} — the interceptor maps api.* keys; delete the per-feature map`);
    }

    // F9 — a var(--cleansia-*) read must be declared somewhere the bundle loads.
    const declared = new Set();
    for (const f of [...TOKEN_DECLARATION_FILES.map((p) => join(REPO, p)), ...stylesheets])
        for (const m of read(f).join("\n").matchAll(/(--cleansia-[\w-]+)\s*:/g)) declared.add(m[1]);
    // F12 asks whether a page's component exists anywhere on the web surface, so the lookup reads
    // the whole tree, not only the scanned roots: under --paths=<a stylesheet dir> the scan holds no
    // .component.ts at all, and every page read as orphaned. The scanned sources are unioned in so
    // a component planted beside a fixture stylesheet is still found.
    const componentBasenames = new Set(
        [...sources, ...WEB_ROOTS.flatMap((r) => walk(dir(r), [".component.ts"], F_SKIP))]
            .filter((f) => f.endsWith(".component.ts"))
            .map((f) => f.split(/[\\/]/).pop().replace(/\.ts$/, "")),
    );
    for (const f of stylesheets) {
        const text = read(f).join("\n");
        for (const m of text.matchAll(/var\((--cleansia-[\w-]+)/g))
            if (!declared.has(m[1]))
                add(f, lineOf(text, m.index), "F9", `${m[1]} is read but declared nowhere — add it to common/variables.scss`);
        // F10 — the radius scale and the colour of a shadow. ADVISORY — 77 off-scale radii and 6
        // primary-tinted shadows on 2026-09-22; flip to `add` at zero. A `0 0 0 Npx` spread with no
        // blur is a focus ring, not a glow.
        for (const m of text.matchAll(/border-radius:\s*([^;!]+?)\s*(?:!important)?;/g)) {
            const value = m[1].trim();
            if (value === "inherit" || RADIUS_TOKEN_RE.test(value)) continue;
            if (value.split(/\s+/).every((part) => RADIUS_SCALE.has(part))) continue;
            warn(f, lineOf(text, m.index), "F10", `border-radius: ${value} — use 6/12/16/24/32px, 50%, 999px or a --cleansia-radius-* token`);
        }
        for (const m of text.matchAll(/box-shadow:\s*([^;]+);/g))
            if (/rgba\(var\(--cleansia-primary-rgb\)/.test(m[1]) && !/^0 0 0 \d+px rgba\(var\(--cleansia-primary-rgb\)/.test(m[1].trim()))
                warn(f, lineOf(text, m.index), "F10", "primary-tinted box-shadow — a shadow is neutral; a glow is the design language's named tell");
        // F12 — a page stylesheet named for a component that no longer exists. ADVISORY — 1 site on
        // 2026-09-22 (template-form.component.scss, which now holds only the `.hint` rule); flip to
        // `add` at zero.
        const page = f.match(PAGE_STYLESHEET);
        if (page && !componentBasenames.has(`${page[1]}.component`))
            warn(f, 1, "F12", `${page[1]}.component.scss has no ${page[1]}.component.ts — move its rules to a shared partial or delete it`);
    }

    // F11 — the five locale bundles of an app carry one key set under the fixed namespaces.
    const byDir = new Map();
    for (const f of bundles) {
        const d = f.slice(0, f.lastIndexOf(sep));
        const locale = f.split(/[\\/]/).pop().replace(/\.json$/, "");
        if (!LOCALES.includes(locale)) continue;
        if (!byDir.has(d)) byDir.set(d, new Map());
        byDir.get(d).set(locale, f);
    }
    for (const [d, locales] of byDir) {
        const parsed = new Map();
        for (const [locale, f] of locales) {
            try {
                parsed.set(locale, JSON.parse(readFileSync(f, "utf8")));
            } catch {
                add(f, 1, "F11", `${locale}.json does not parse`);
            }
        }
        const en = parsed.get("en");
        if (!en) continue;
        const enFile = locales.get("en");
        const enKeys = new Set(flatKeys(en));
        for (const ns of Object.keys(en)) {
            if (ns === "common") add(enFile, 1, "F11", "a `common` namespace — its readers moved to global.actions.*; nothing reads it");
            // ADVISORY — 9 stray namespaces on 2026-09-22 (admin: admin_roles, fiscal_failures,
            // pay_periods, profile, recurring_booking; partner: name, profile, recurring_booking,
            // registration_lock); flip to `add` once a sweep files them under pages.* or components.*.
            else if (!I18N_NAMESPACES.has(ns)) warn(enFile, 1, "F11", `top-level namespace '${ns}' is outside the fixed set — file it under pages.* or components.*`);
        }
        for (const locale of LOCALES) {
            if (locale === "en") continue;
            const f = locales.get(locale);
            if (!f) {
                add(join(d, `${locale}.json`), 1, "F11", `${locale}.json is missing — every key ships in all five locales`);
                continue;
            }
            const other = parsed.get(locale);
            if (!other) continue;
            const keys = new Set(flatKeys(other));
            const missing = [...enKeys].filter((k) => !keys.has(k));
            const extra = [...keys].filter((k) => !enKeys.has(k));
            if (missing.length)
                add(f, 1, "F11", `${missing.length} key(s) in en.json are missing here (first: ${missing[0]})`);
            if (extra.length)
                add(f, 1, "F11", `${extra.length} key(s) here are not in en.json (first: ${extra[0]})`);
        }
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
    frontend: WEB_ROOTS,
    mobile: ["src/cleansia_android"],
};
const custom = pathsArg ? pathsArg.split(",") : null;
let scanned = 0;
if (onlyStacks.includes("backend")) {
    scanned += checkBackend(custom || DEFAULTS.backend);
    checkDisputeWrites(custom || DEFAULTS.disputeWrites);
}
if (onlyStacks.includes("frontend")) {
    scanned += checkFrontend(custom || DEFAULTS.frontend);
    scanned += checkFrontendSurface(custom || DEFAULTS.frontend);
}
if (onlyStacks.includes("mobile"))
    scanned += checkMobile(custom || DEFAULTS.mobile);

if (advisories.length) {
    const tally = [...advisoryCounts]
        .sort(([a], [b]) => a.localeCompare(b, undefined, { numeric: true }))
        .map(([rule, n]) => `${rule} ${n}`)
        .join(", ");
    console.log(`consistency: ${advisories.length} advisory warning(s) (non-blocking) — ${tally}`);
    for (const w of advisories.sort()) console.log("  " + w);
}
// Explicit --paths that matched nothing is a non-run, not a pass: the caller asked for specific
// directories and got no coverage at all. Fail loudly so it cannot be recorded as a green gate.
// (No --paths means the defaults are in play, and a stack legitimately having no files is fine.)
if (custom && scanned === 0) {
    console.log(
        `consistency: NOT RUN — --paths matched no scannable files (${custom.join(", ")})`,
    );
    console.log(
        "  Check the path exists and holds files this stack scans (backend .cs, frontend .ts/.html/.scss/.json, mobile .kt).",
    );
    process.exit(1);
}
if (violations.length === 0) {
    console.log(
        `consistency: OK (${scanned} files scanned, stacks: ${onlyStacks.join(", ")})`,
    );
    process.exit(0);
}
console.log(`consistency: ${violations.length} violation(s)`);
for (const v of violations.sort()) console.log("  " + v);
process.exit(warnOnly ? 0 : 1);

