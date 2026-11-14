import { expect, test } from '@playwright/test';
import { CaseReport, mergeReports } from './support/case-reporter';

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

  test('a report in the old shape is read as no history', () => {
    const old = { 'E2E-BOOK-001': { status: 'skipped', tests: ['a'], notes: [] } } as unknown as CaseReport;
    expect(mergeReports(old, { 'E2E-BOOK-001': { status: 'passed', tests: { a: one('passed') } } })['E2E-BOOK-001'].status).toBe('passed');
  });
});
