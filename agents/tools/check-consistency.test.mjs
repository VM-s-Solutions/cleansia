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
        ? `\ncheck-consistency rules (B10 + E9): ${cases.length} passed`
        : `\ncheck-consistency rules (B10 + E9): ${failed}/${cases.length} FAILED`,
);
process.exit(failed === 0 ? 0 : 1);
