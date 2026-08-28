#!/usr/bin/env node
/**
 * Tests for the B10 dispute transition-guard rule and the E9 session-wipe-set advisory in
 * check-consistency.mjs.
 *
 * Dependency-free (Node's built-in assert + child_process), matching the tool itself. Writes
 * throwaway .cs/.kt fixtures under a temp dir inside the repo, runs the checker scoped to that dir
 * via --paths=, and asserts on the findings. B10 is a hard gate (exit 1); E9 is WARN-only (exit 0,
 * printed for the Reviewer). The temp dir is removed on exit.
 *
 * Run: node agents/tools/check-consistency.test.mjs
 */
import assert from "node:assert/strict";
import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";

const REPO = join(fileURLToPath(import.meta.url), "..", "..", "..");
const TOOL = join(REPO, "agents", "tools", "check-consistency.mjs");

// Run the checker over a single fixture file and return { code, out, b10 }.
function run(fixtureBody) {
    const root = mkdtempSync(join(REPO, ".b10-fixture-"));
    try {
        const fileName = fixtureBody.fileName ?? "Fixture.cs";
        const sub = join(root, fixtureBody.subdir ?? "Features");
        mkdirSync(sub, { recursive: true });
        writeFileSync(join(sub, fileName), fixtureBody.code, "utf8");
        const rel = relative(REPO, root).split(sep).join("/");
        let code = 0;
        let out = "";
        try {
            out = execFileSync(
                process.execPath,
                [TOOL, "backend", `--paths=${rel}`],
                { encoding: "utf8" },
            );
        } catch (e) {
            code = e.status ?? 1;
            out = (e.stdout ?? "") + (e.stderr ?? "");
        }
        return { code, out, b10: out.split(/\r?\n/).filter((l) => /\bB10\b/.test(l)) };
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

const cases = [];
const test = (name, fn) => cases.push([name, fn]);

// a deliberately-introduced fourth direct caller is flagged
test("flags a direct dispute.Resolve outside the allowlist", () => {
    const r = run({
        code: `namespace X;
public class RogueHandler
{
    public void DoIt(Dispute dispute)
    {
        dispute.Resolve("actor", null, "notes");
    }
}`,
    });
    assert.equal(r.b10.length, 1, `expected 1 B10, got: ${r.out}`);
    assert.equal(r.code, 1, "checker must exit 1 on a violation");
});

test("flags direct dispute.Close and dispute.Escalate outside the allowlist", () => {
    const r = run({
        code: `namespace X;
public class RogueHandler
{
    public void DoIt(Dispute dispute)
    {
        dispute.Close("actor");
        dispute.Escalate("actor");
    }
}`,
    });
    assert.equal(r.b10.length, 2, `expected 2 B10, got: ${r.out}`);
});

// the sanctioned writers are allowlisted by enclosing method
test("allows ReflectChargebackStatus (sanctioned webhook reflector)", () => {
    const r = run({
        code: `namespace X;
public class H
{
    private void ReflectChargebackStatus(Dispute dispute)
    {
        dispute.Resolve("a", null, "n");
        dispute.Close("a");
        dispute.Escalate("a");
    }
}`,
    });
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

// HandleChargeback no longer gets a direct-call exception: it routes its new dispute's escalation
// through dispute.UpdateStatus(Escalated) (the guard), so a *direct* Escalate inside it is now a
// genuine B10 violation. This pins that the funnel is enforced going forward (it regresses if anyone
// re-introduces a bare dispute.Escalate in the chargeback creator).
test("flags a direct dispute.Escalate inside HandleChargeback (no longer allowlisted)", () => {
    const r = run({
        code: `namespace X;
public class H
{
    private void HandleChargeback(Dispute dispute)
    {
        dispute.Escalate("a");
    }
}`,
    });
    assert.equal(r.b10.length, 1, `expected 1 B10, got: ${r.out}`);
    assert.equal(r.code, 1, "checker must exit 1 on a violation");
});

// The guarded funnel the creator now uses is allowed: dispute.UpdateStatus(...) is not a
// Close/Escalate/Resolve call, so HandleChargeback routing through it produces no B10.
test("allows HandleChargeback routing through dispute.UpdateStatus (the guarded funnel)", () => {
    const r = run({
        code: `namespace X;
public class H
{
    private void HandleChargeback(Dispute dispute)
    {
        dispute.UpdateStatus(DisputeStatus.Escalated, "a");
    }
}`,
    });
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

test("allows the in-app guard router Dispute.UpdateStatus", () => {
    const r = run({
        code: `namespace X;
public class Dispute
{
    public bool UpdateStatus(int newStatus, string by)
    {
        Close(by);
        Escalate(by);
        return true;
    }
}`,
    });
    // Note: this domain shape uses bare Close(by)/Escalate(by) (no `dispute.` receiver) so it is not
    // matched at all; UpdateStatus stays in the allowlist for the defensive case of a `dispute.`-style
    // call landing inside it.
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

test("allows ResolveDispute.cs Handle (money-path owner, pinned by file)", () => {
    const r = run({
        fileName: "ResolveDispute.cs",
        code: `namespace X;
public class Handler
{
    public void Handle(Dispute dispute)
    {
        dispute.Resolve("a", null, "n");
    }
}`,
    });
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

test("a generic Handle in another file is NOT allowlisted (Handle is not blanket-sanctioned)", () => {
    const r = run({
        fileName: "OtherHandler.cs",
        code: `namespace X;
public class Handler
{
    public void Handle(Dispute dispute)
    {
        dispute.Resolve("a", null, "n");
    }
}`,
    });
    assert.equal(r.b10.length, 1, `expected 1 B10, got: ${r.out}`);
});

// Receiver discrimination — no false positives on other types' .Close/.Resolve.
test("does NOT flag payPeriod.Close / period.Close (different receiver)", () => {
    const r = run({
        code: `namespace X;
public class H
{
    public void DoIt(PayPeriod payPeriod, PayPeriod period)
    {
        payPeriod.Close("a", "n");
        period.Close("a");
    }
}`,
    });
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

test("does NOT flag FiscalSequenceScope.Resolve (static, different type)", () => {
    const r = run({
        code: `namespace X;
public class H
{
    public void DoIt()
    {
        var x = FiscalSequenceScope.Resolve("cz-eet2", 2026);
    }
}`,
    });
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

test("does NOT flag a *Resolver receiver's .Resolve (DI resolver, not a Dispute)", () => {
    const r = run({
        code: `namespace X;
public class H
{
    public void DoIt(IFiscalServiceResolver fiscalServiceResolver)
    {
        var s = fiscalServiceResolver.Resolve("cz");
    }
}`,
    });
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

// Receiver-name independence — a Dispute can be bound to any local name, so the rule must flag
// .Close/.Escalate/.Resolve( regardless of the receiver token, not only literal `dispute.`.
test("flags a Dispute bound to a non-'dispute' local (existing.Resolve, d.Close, theDispute.Escalate)", () => {
    const r = run({
        code: `namespace X;
public class RogueHandler
{
    public void DoIt(Dispute existing, Dispute d, Dispute theDispute)
    {
        existing.Resolve("a", null, "n");
        d.Close("a");
        theDispute.Escalate("a");
    }
}`,
    });
    assert.equal(r.b10.length, 3, `expected 3 B10, got: ${r.out}`);
    assert.equal(r.code, 1, "checker must exit 1 on a violation");
});

// Scan-root coverage — a direct caller outside Features/ (e.g. a domain service or the unguarded
// domain methods under Core.Domain/Disputes) must still be scanned.
test("flags a direct caller located outside Features/ (Services/ dir)", () => {
    const r = run({
        subdir: "Services",
        code: `namespace X;
public class RogueService
{
    public void DoIt(Dispute existing)
    {
        existing.Resolve("a", null, "n");
    }
}`,
    });
    assert.equal(r.b10.length, 1, `expected 1 B10, got: ${r.out}`);
    assert.equal(r.code, 1, "checker must exit 1 on a violation");
});

// Run the checker over a single Kotlin fixture under src/cleansia_android/... and return
// { code, out, e9 }. E9 is WARN-only (non-blocking), so a flagged fixture must exit 0.
function runKt(code, fileName = "Fixture.kt") {
    const root = mkdtempSync(join(REPO, ".e9-fixture-"));
    try {
        // The checker's mobile default root is src/cleansia_android; scope with --paths= to the temp dir.
        const sub = join(root, "app");
        mkdirSync(sub, { recursive: true });
        writeFileSync(join(sub, fileName), code, "utf8");
        const rel = relative(REPO, root).split(sep).join("/");
        let rc = 0;
        let out = "";
        try {
            out = execFileSync(
                process.execPath,
                [TOOL, "mobile", `--paths=${rel}`],
                { encoding: "utf8" },
            );
        } catch (e) {
            rc = e.status ?? 1;
            out = (e.stdout ?? "") + (e.stderr ?? "");
        }
        return { code: rc, out, e9: out.split(/\r?\n/).filter((l) => /\bE9\b/.test(l)) };
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

// Run the checker over a single TypeScript facade fixture and return { code, out, c3 }.
function runTs(code, fileName = "fixture.facade.ts") {
    const root = mkdtempSync(join(REPO, ".c3-fixture-"));
    try {
        const sub = join(root, "lib");
        mkdirSync(sub, { recursive: true });
        writeFileSync(join(sub, fileName), code, "utf8");
        const rel = relative(REPO, root).split(sep).join("/");
        let rc = 0;
        let out = "";
        try {
            out = execFileSync(
                process.execPath,
                [TOOL, "frontend", `--paths=${rel}`],
                { encoding: "utf8" },
            );
        } catch (e) {
            rc = e.status ?? 1;
            out = (e.stdout ?? "") + (e.stderr ?? "");
        }
        return { code: rc, out, c3: out.split(/\r?\n/).filter((l) => /\bC3\b/.test(l)) };
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
}

// B10 — the type-token gate. Without it the rule fired on any `X.Resolve(` in the tree; it was
// reporting TimeZoneResolution.Resolve(...) in two files that never mention a Dispute.
test("B10 does NOT flag .Resolve( in a file that never names Dispute", () => {
    const r = run({
        code: `namespace X;
public class H
{
    public TimeZoneInfo DoIt(string id) => TimeZoneResolution.Resolve(id);
}`,
    });
    assert.equal(r.b10.length, 0, `expected 0 B10, got: ${r.out}`);
});

test("B10 still flags a bare .Resolve( once the file names the type", () => {
    const r = run({
        code: `namespace X;
public class H
{
    public void DoIt(Dispute existing)
    {
        existing.Resolve("actor", null, "notes");
    }
}`,
    });
    assert.equal(r.b10.length, 1, `expected 1 B10, got: ${r.out}`);
});

// B1 — the record window. A flat 4-line lookahead bled into the NEXT record, so an HTTP body DTO
// sitting immediately above the real Command read as a mis-named command.
test("B1 does NOT flag an HTTP body DTO declared above the real Command", () => {
    const r = run({
        code: `namespace X;
public class ApproveThing
{
    public record Request(string WorkCountryId, string? Notes);

    public record Command(string Id, string WorkCountryId) : ICommand<Response>;
}`,
    });
    const b1 = r.out.split(/\r?\n/).filter((l) => /\bB1\b/.test(l));
    assert.equal(b1.length, 0, `expected 0 B1, got: ${r.out}`);
});

// B3 — the four exempt validator bases. Narrowed 2026-08-14 after the rule was found to be telling
// agents to remove a security control: UserEmailValidator's constructor rule is what stops an erased or
// unconfirmed principal acting on a still-valid token, and the three web hosts have no revocation
// directory to catch it otherwise. Paired with the STILL-flags case below, per the P4 discipline.
test("B3 does NOT flag the four exempt validator bases", () => {
    for (const base of [
        "AbstractValidator",
        "BaseAuthValidator",
        "BaseUserValidator",
        "LoginValidator",
        "UserEmailValidator",
    ]) {
        const r = run({
            code: `namespace X;
public class DoThing
{
    public class Validator : ${base}<Command>
    {
    }
}`,
        });
        const b3 = r.out.split(/\r?\n/).filter((l) => /\bB3\b/.test(l));
        assert.equal(b3.length, 0, `${base} should be exempt, got: ${r.out}`);
    }
});

test("B3 STILL flags a validator inheriting anything else", () => {
    const r = run({
        code: `namespace X;
public class DoThing
{
    public class Validator : SomeOtherBaseValidator<Command>
    {
    }
}`,
    });
    const b3 = r.out.split(/\r?\n/).filter((l) => /\bB3\b/.test(l));
    assert.equal(b3.length, 1, `expected 1 B3, got: ${r.out}`);
});

// conv `: any` — the twelve narrowings CL-024 made and never pinned, added 2026-08-15 (CL-071).
// The exemption is FILE-scoped and keyed on tokens as ordinary as `onChange`, so a new file that
// merely mentions ControlValueAccessor blinds the rule over everything in it. Today it correctly
// suppresses 11 `: any` lines across 9 design-system components — correct, and until now unpinned.
test("conv does NOT flag `: any` on a real ControlValueAccessor member", () => {
    const r = runTs(`export class Input implements ControlValueAccessor {
  writeValue(value: any): void {}
  registerOnChange(fn: any): void {}
}`, "cleansia-input.component.ts");
    const conv = r.out.split(/\r?\n/).filter((l) => /\bconv\b/.test(l));
    assert.equal(conv.length, 0, r.out);
});

test("conv does NOT flag `: any` on a trackBy — Angular declares TrackByFunction returning any", () => {
    const r = runTs(`export class ListComponent {
  trackByRow(index: number, row: any): any { return row.id; }
}`, "list.component.ts");
    const conv = r.out.split(/\r?\n/).filter((l) => /\bconv\b/.test(l));
    assert.equal(conv.length, 0, r.out);
});

test("conv STILL flags `: any` in a file with no CVA and no trackBy", () => {
    const r = runTs(`export class PlainFacade {
  load(payload: any): void {}
}`);
    const conv = r.out.split(/\r?\n/).filter((l) => /\bconv\b/.test(l));
    assert.equal(conv.length, 1, `expected 1 conv, got: ${r.out}`);
});

// The one that matters, and it is NARROWER than it looks. `implementsCva` is computed over the whole
// file, but the suppression is `implementsCva && CVA_ANY.test(ln)` — the second half is per LINE. So a
// CVA file does not go blind: only lines that themselves name a CVA member are exempt, and an
// unrelated `: any` two lines down is still flagged. Pin that, because "the exemption is file-scoped"
// is the reasonable misreading, and acting on it would narrow a rule that is already tight.
test("conv STILL flags an unrelated `: any` inside a ControlValueAccessor file", () => {
    const r = runTs(`export class Widget implements ControlValueAccessor {
  writeValue(value: any): void {}
  unrelatedHelper(payload: any): void {}
}`, "cleansia-widget.component.ts");
    const conv = r.out.split(/\r?\n/).filter((l) => /\bconv\b/.test(l));
    assert.equal(conv.length, 1, `expected the unrelated any to be flagged, got: ${r.out}`);
});

// And the token match is word-BOUNDED, which is the second reason this is tighter than it reads:
// `onChangeHandler` does not satisfy /\bonChange\b/, so a field whose name merely starts with a CVA
// token is still flagged. The exemption reaches the CVA members themselves and nothing adjacent.
test("conv STILL flags a field whose name only STARTS with a CVA token", () => {
    const r = runTs(`export class Widget implements ControlValueAccessor {
  writeValue(value: any): void {}
  private onChangeHandler: any = null;
}`, "cleansia-widget2.component.ts");
    const conv = r.out.split(/\r?\n/).filter((l) => /\bconv\b/.test(l));
    assert.equal(conv.length, 1, `expected the near-miss token to be flagged, got: ${r.out}`);
});

test("B1 STILL flags a command record that does not end in Command", () => {
    const r = run({
        code: `namespace X;
public class DoThing
{
    public record Request(string Id) : ICommand<Response>;
}`,
    });
    const b1 = r.out.split(/\r?\n/).filter((l) => /\bB1\b/.test(l));
    assert.equal(b1.length, 1, `expected 1 B1, got: ${r.out}`);
});

// C3 — teardown the rule could not see. Both shapes below are CORRECT and were reported as leaks.
test("C3 does NOT flag a pipe whose takeUntil is far above the subscribe", () => {
    const filler = Array.from({ length: 34 }, (_, i) => `          // padding ${i}`).join("\n");
    const r = runTs(`export class F extends UnsubscribeControlDirective {
  load(): void {
    this.svc.get()
      .pipe(
${filler}
        takeUntil(this.destroyed$),
        finalize(() => this.loading.set(false))
      )
      .subscribe(() => this.done());
  }
}`);
    assert.equal(r.c3.length, 0, `expected 0 C3, got: ${r.out}`);
});

test("C3 does NOT flag a subscribe whose teardown lives on the stream definition", () => {
    const r = runTs(`export class F extends UnsubscribeControlDirective {
  private readonly data$ = this.svc.get().pipe(takeUntil(this.destroyed$));

  load(): void {
    this.data$.subscribe();
  }
}`);
    assert.equal(r.c3.length, 0, `expected 0 C3, got: ${r.out}`);
});

test("C3 STILL flags a subscribe with no teardown anywhere", () => {
    const r = runTs(`export class F extends UnsubscribeControlDirective {
  load(): void {
    this.svc.get().subscribe(() => this.done());
  }
}`);
    assert.equal(r.c3.length, 1, `expected 1 C3, got: ${r.out}`);
});

// E9 — session-wipe-set membership (WARN-only).
test("E9 flags a @Singleton StateFlow cache holder NOT implementing SessionScopedCache", () => {
    const r = runKt(`package x
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
@Singleton
class ProfileRepository @Inject constructor(private val api: Api) {
    private val _me = MutableStateFlow<Me?>(null)
}`);
    assert.equal(r.e9.length, 1, `expected 1 E9, got: ${r.out}`);
    assert.equal(r.code, 0, "E9 is WARN-only — must not fail the build");
});

test("E9 does NOT flag a member (: SessionScopedCache on the class line)", () => {
    const r = runKt(`package x
import javax.inject.Singleton
import cz.cleansia.core.auth.SessionScopedCache
import kotlinx.coroutines.flow.MutableStateFlow
@Singleton
class OrderRepository @Inject constructor(private val api: Api) : SessionScopedCache {
    private val _orders = MutableStateFlow<List<Order>>(emptyList())
    override suspend fun clear() {}
}`);
    assert.equal(r.e9.length, 0, `expected 0 E9, got: ${r.out}`);
});

test("E9 does NOT flag a member bound behind an interface (: Repo, SessionScopedCache)", () => {
    const r = runKt(`package x
import javax.inject.Singleton
import cz.cleansia.core.auth.SessionScopedCache
import kotlinx.coroutines.flow.MutableStateFlow
@Singleton
class OrdersRepositoryImpl @Inject constructor(private val api: Api) : OrdersRepository, SessionScopedCache {
    private val _orders = MutableStateFlow<List<Order>>(emptyList())
    override suspend fun clear() {}
}`);
    assert.equal(r.e9.length, 0, `expected 0 E9, got: ${r.out}`);
});

test("E9 does NOT flag an allowlisted public cache (CatalogRepository)", () => {
    const r = runKt(`package x
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableStateFlow
@Singleton
class CatalogRepository @Inject constructor(private val api: Api) {
    private val _services = MutableStateFlow<List<Svc>>(emptyList())
}`);
    assert.equal(r.e9.length, 0, `expected 0 E9 (allowlisted), got: ${r.out}`);
});

test("E9 does NOT flag a stateless pass-through (no cache field)", () => {
    const r = runKt(`package x
import javax.inject.Singleton
@Singleton
class PaymentRepository @Inject constructor(private val api: Api) {
    suspend fun createIntent(id: String): ApiResult<Resp> = safeApiCall { api.create(id) }
}`);
    assert.equal(r.e9.length, 0, `expected 0 E9 (no cache field), got: ${r.out}`);
});

test("E9 does NOT flag a replay=0 SharedFlow event bus (retains nothing)", () => {
    const r = runKt(`package x
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableSharedFlow
@Singleton
class SomeEventBus @Inject constructor() {
    private val _events = MutableSharedFlow<Ev>(replay = 0, extraBufferCapacity = 8)
}`);
    assert.equal(r.e9.length, 0, `expected 0 E9 (SharedFlow, not a StateFlow cache), got: ${r.out}`);
});

// E1 — the UiState phase-bag rule, narrowed on 2026-08-28. It used to fire on every
// `data class *UiState`: nine hits in the tree, one defensible. These four pin both halves — that it
// still catches the shape it exists for, and that each exemption is the one the corpus forced.
const linesFor = (r, rule) =>
    r.out.split(/\r?\n/).filter((l) => new RegExp(`\\b${rule}\\b`).test(l));

test("E1 flags a genuine phase bag (one in-flight flag + error + data)", () => {
    const r = runKt(`package x
data class FeedUiState(
    val isLoading: Boolean = false,
    val error: String? = null,
    val items: List<String> = emptyList(),
)`);
    assert.equal(linesFor(r, "E1").length, 1, `expected 1 E1, got: ${r.out}`);
});

test("E1 does NOT flag a single-signal state (nothing to contradict)", () => {
    const r = runKt(`package x
data class SettingsUiState(
    val isSignedOut: Boolean = false,
)`);
    assert.equal(linesFor(r, "E1").length, 0, `expected 0 E1, got: ${r.out}`);
});

test("E1 does NOT flag two concurrent in-flight signals (a union would erase them)", () => {
    const r = runKt(`package x
data class ListUiState(
    val isUserRefreshing: Boolean = false,
    val isBackgroundRefreshing: Boolean = false,
    val error: String? = null,
    val hasLoadedOnce: Boolean = false,
)`);
    assert.equal(linesFor(r, "E1").length, 0, `expected 0 E1, got: ${r.out}`);
});

test("E1 does NOT flag a form carrying per-field validation errors", () => {
    const r = runKt(`package x
data class SignUpUiState(
    val email: String = "",
    val isLoading: Boolean = false,
    val error: String? = null,
    val emailError: String? = null,
)`);
    assert.equal(linesFor(r, "E1").length, 0, `expected 0 E1, got: ${r.out}`);
});

// conv `: any` — the rule checked only the CURRENT line for a disable directive, so it reported
// error.codes.ts, where the exception is both explained in prose and sanctioned by ESLint itself.
test("conv flags a bare ': any'", () => {
    const r = runTs(`export interface Thing {
  value: any;
}`);
    assert.equal(linesFor(r, "conv").length, 1, `expected 1 conv, got: ${r.out}`);
});

test("conv does NOT flag ': any' under an eslint-disable-next-line", () => {
    const r = runTs(`// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type Fn = (value?: any) => string;`);
    assert.equal(linesFor(r, "conv").length, 0, `expected 0 conv, got: ${r.out}`);
});

test("conv does NOT flag ': any' under a file-level no-explicit-any disable", () => {
    const r = runTs(`/* eslint-disable @typescript-eslint/no-explicit-any */
export interface Thing {
  value: any;
}`);
    assert.equal(linesFor(r, "conv").length, 0, `expected 0 conv, got: ${r.out}`);
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
        ? `\ncheck-consistency rules (B1 + B10 + C3 + E9 + E1 + conv): ${cases.length} passed`
        : `\ncheck-consistency rules (B1 + B10 + C3 + E9 + E1 + conv): ${failed}/${cases.length} FAILED`,
);
process.exit(failed === 0 ? 0 : 1);
