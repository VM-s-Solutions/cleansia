import type { FullResult, Reporter, TestCase, TestResult } from '@playwright/test/reporter';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { findRun, runDir } from './run';

type TestOutcome = { status: string; note: string };
export type CaseReport = Record<string, { status: string; tests: Record<string, TestOutcome> }>;
// 'finding': the test passed on an answer the catalogue calls wrong, annotated for the triager.
// A skip ranks lowest: a case another test proved is not unproven because its twin skipped (the web half's Close now
// skips once the API half has closed the period), and a case reads skipped only when every test of it skipped.
const RANK: Record<string, number> = { skipped: 0, passed: 1, finding: 2, interrupted: 3, timedOut: 4, failed: 5 };
const worst = (tests: Record<string, TestOutcome>) =>
  Object.values(tests).reduce((s, t) => ((RANK[t.status] ?? 5) > (RANK[s] ?? 5) ? t.status : s), 'skipped');

/**
 * Folds one invocation into the stage's report so far. Per test, a skip never replaces an earlier outcome that was
 * not a skip (a job an earlier run finished is skipped now); anything else replaces it. A case is its worst test,
 * a skip counting only when every test skipped.
 */
export function mergeReports(previous: CaseReport, current: CaseReport): CaseReport {
  const out: CaseReport = { ...previous };
  for (const [id, c] of Object.entries(current)) {
    const before = previous[id]?.tests;
    const tests = before && !Array.isArray(before) ? { ...before } : {};
    for (const [title, t] of Object.entries(c.tests)) {
      if (t.status !== 'skipped' || !tests[title] || tests[title].status === 'skipped') tests[title] = t;
    }
    out[id] = { status: worst(tests), tests };
  }
  return out;
}

/** The printed note: the reason of a test with the case's outcome, plus why any test of a case that did not skip skipped. */
export function caseNote(c: CaseReport[string]): string {
  const tests = Object.values(c.tests);
  const note = tests.find((t) => t.status === c.status && t.note)?.note ?? '';
  const skipped = c.status === 'skipped' ? [] : tests.filter((t) => t.status === 'skipped');
  if (!skipped.length) return note;
  const reasons = skipped.map((t) => t.note).filter(Boolean).join('; ');
  return `${note ? `${note} ` : ''}(${skipped.length} skipped${reasons ? `: ${reasons}` : ''})`;
}

/**
 * Reports each catalogue case id (a test tag such as @E2E-BOOK-007) with the worst outcome of the tests tagged with
 * it, merged into the run folder's report-<stage>.json so a re-run of a stage keeps what earlier invocations proved.
 */
export default class CaseReporter implements Reporter {
  private readonly cases: CaseReport = {};
  private stage = '';

  onTestEnd(test: TestCase, result: TestResult) {
    this.stage = test.parent.project()?.name ?? '';
    const finding = result.annotations.find((a) => a.type === 'finding');
    const status = result.status === 'passed' && finding ? 'finding' : result.status;
    const note =
      result.annotations.find((a) => a.type === 'skip' && a.description)?.description ?? finding?.description ?? result.error?.message?.split('\n')[0] ?? '';
    for (const id of test.tags.filter((t) => t.startsWith('@E2E-')).map((t) => t.slice(1))) {
      const c = (this.cases[id] ??= { status: 'passed', tests: {} });
      c.tests[test.title] = { status, note };
      c.status = worst(c.tests);
    }
  }

  onEnd(result: FullResult) {
    if (!Object.keys(this.cases).length) return;
    const run = findRun();
    const file = run ? join(runDir(run.runId), `report-${this.stage}.json`) : '';
    let previous: CaseReport = {};
    try {
      if (file && existsSync(file)) previous = JSON.parse(readFileSync(file, 'utf8')) as CaseReport;
    } catch {
      previous = {};
    }
    const merged = mergeReports(previous, this.cases);
    console.log(`\nCases (${this.stage}, this invocation ${result.status}; merged with earlier invocations of run ${run?.runId ?? '-'}):`);
    for (const id of Object.keys(merged).sort()) {
      console.log(`  ${id.padEnd(16)} ${merged[id].status.padEnd(8)} ${caseNote(merged[id])}`);
    }
    if (file) writeFileSync(file, JSON.stringify(merged, null, 2));
  }
}
