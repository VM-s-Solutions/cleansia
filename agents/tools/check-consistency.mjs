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
 * violations declared in agents/cleanup/consistency-baseline.md are cleared.
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
const warn = (file, line, rule, msg) =>
    advisories.push(
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
    const phase = fields.filter(
        (x) => x.type === "Boolean" || /error|outcome|status/i.test(x.name),
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

if (advisories.length) {
    console.log(`consistency: ${advisories.length} advisory warning(s) (non-blocking)`);
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
        "  Check the path exists and holds files this stack scans (backend .cs, frontend .ts, mobile .kt).",
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

