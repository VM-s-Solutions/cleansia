import { expect, test } from '@playwright/test';
import { mkdirSync, rmSync } from 'node:fs';
import { CaseReport, caseNote, mergeReports } from './support/case-reporter';
import { flag, RunState, runDir, saveRun, setFlag } from './support/run';

const one = (status: string, note = '') => ({ status, note });

test.describe('case report merge', () => {
  test('a skip of a job an earlier run finished keeps the earlier pass', () => {
    const before: CaseReport = { 'E2E-EXEC-002': { status: 'passed', tests: { card: one('passed') } } };
    const now: CaseReport = { 'E2E-EXEC-002': { status: 'skipped', tests: { card: one('skipped', 'completed by an earlier execute run') } } };
    expect(mergeReports(before, now)['E2E-EXEC-002']).toEqual({ status: 'passed', tests: { card: one('passed') } });
  });

  test('a case is the worst of its tests across invocations, and a new non-skip replaces the old outcome', () => {
    const before: CaseReport = { 'E2E-PAYOUT-001': { status: 'failed', tests: { cash: one('failed', 'boom'), card: one('passed') } } };
    const now: CaseReport = { 'E2E-PAYOUT-001': { status: 'passed', tests: { cash: one('passed') } } };
    expect(mergeReports(before, now)['E2E-PAYOUT-001'].status).toBe('passed');
    const later: CaseReport = { 'E2E-PAYOUT-001': { status: 'failed', tests: { card: one('failed', 'late') } } };
    expect(mergeReports(mergeReports(before, now), later)['E2E-PAYOUT-001']).toEqual({ status: 'failed', tests: { cash: one('passed'), card: one('failed', 'late') } });
  });

  test('cases only an earlier invocation touched are kept, and a finding outranks a skip', () => {
    const before: CaseReport = { 'E2E-BOOK-001': { status: 'passed', tests: { a: one('passed') } } };
    const now: CaseReport = { 'E2E-AUTH-051': { status: 'finding', tests: { b: one('finding', 'triage') } } };
    const merged = mergeReports(before, now);
    expect(Object.keys(merged).sort()).toEqual(['E2E-AUTH-051', 'E2E-BOOK-001']);
    const skipAndFinding: CaseReport = { 'E2E-AUTH-051': { status: 'skipped', tests: { c: one('skipped') } } };
    expect(mergeReports(merged, skipAndFinding)['E2E-AUTH-051'].status).toBe('finding');
  });

  test('a case one test proved reads passed when its twin skipped, and skipped only when every test skipped', () => {
    // The API half pressed Close now, so the web half's Close now test skipped.
    const now: CaseReport = { 'E2E-PAYOUT-016': { status: 'skipped', tests: { api: one('passed'), web: one('skipped', 'already closed') } } };
    expect(mergeReports({}, now)['E2E-PAYOUT-016'].status).toBe('passed');
    const none: CaseReport = { 'E2E-PAYOUT-017': { status: 'passed', tests: { api: one('skipped'), web: one('skipped') } } };
    expect(mergeReports({}, none)['E2E-PAYOUT-017'].status).toBe('skipped');
  });

  test('a case that passed with a skipped twin prints the skip reason, and an all-skipped case prints its own reason once', () => {
    const half = { status: 'passed', tests: { web: one('passed'), api: one('skipped', 'can be started from 07:45') } };
    expect(caseNote(half)).toBe('(1 skipped: can be started from 07:45)');
    expect(caseNote({ status: 'skipped', tests: { api: one('skipped', 'can be started from 07:45') } })).toBe('can be started from 07:45');
    expect(caseNote({ status: 'passed', tests: { api: one('passed') } })).toBe('');
  });

  test('a report in the old shape is read as no history', () => {
    const old = { 'E2E-BOOK-001': { status: 'skipped', tests: ['a'], notes: [] } } as unknown as CaseReport;
    expect(mergeReports(old, { 'E2E-BOOK-001': { status: 'passed', tests: { a: one('passed') } } })['E2E-BOOK-001'].status).toBe('passed');
  });
});

test.describe('run state flags', () => {
  test('a gate one file set survives another file saving the state it loaded before', () => {
    const runId = `logic-${process.pid}`;
    mkdirSync(runDir(runId), { recursive: true });
    try {
      const fresh = (): RunState => ({ runId, password: '-', orders: {} });
      saveRun(fresh());
      const tenFile = fresh();
      const twentyFile = fresh();
      setFlag(tenFile, 'f1.previewed', new Date('2026-11-18T07:58:00Z'));
      expect(flag(twentyFile, 'f1.previewed')).toBe('2026-11-18T07:58:00.000Z');
      twentyFile.serviceId = 'svc';
      saveRun(twentyFile);
      expect(flag(fresh(), 'f1.previewed')).toBe('2026-11-18T07:58:00.000Z');
      expect(flag(fresh(), 'never.set')).toBeUndefined();
    } finally {
      rmSync(runDir(runId), { recursive: true, force: true });
    }
  });
});
