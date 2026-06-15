#!/usr/bin/env node
/**
 * Tests for the B10 dispute transition-guard rule in check-consistency.mjs.
 *
 * Dependency-free (Node's built-in assert + child_process), matching the tool itself. Writes
 * throwaway .cs fixtures under a temp dir inside the repo, runs the checker scoped to that dir via
 * --paths=, and asserts on the B10 findings. The temp dir is removed on exit.
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
        ? `\nB10 rule: ${cases.length} passed`
        : `\nB10 rule: ${failed}/${cases.length} FAILED`,
);
process.exit(failed === 0 ? 0 : 1);
