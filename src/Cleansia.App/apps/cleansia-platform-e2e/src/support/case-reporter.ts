import type { FullResult, Reporter, TestCase, TestResult } from '@playwright/test/reporter';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { findRun, runDir } from './run';

type Outcome = { status: string; tests: string[]; notes: string[] };
const RANK: Record<string, number> = { passed: 0, skipped: 1, interrupted: 2, timedOut: 3, failed: 4 };

/** Reports each catalogue case id (a test tag such as @E2E-BOOK-007) with the worst outcome of the tests tagged with it. */
export default class CaseReporter implements Reporter {
  private readonly cases = new Map<string, Outcome>();
  private stage = '';

  onTestEnd(test: TestCase, result: TestResult) {
    this.stage = test.parent.project()?.name ?? '';
    const notes = result.annotations.filter((a) => a.type === 'skip' && a.description).map((a) => a.description ?? '');
    if (result.error?.message) notes.push(result.error.message.split('\n')[0]);
    for (const id of test.tags.filter((t) => t.startsWith('@E2E-')).map((t) => t.slice(1))) {
      const seen = this.cases.get(id) ?? { status: 'passed', tests: [], notes: [] };
      if (RANK[result.status] > RANK[seen.status]) seen.status = result.status;
      seen.tests.push(test.title);
      seen.notes.push(...notes);
      this.cases.set(id, seen);
    }
  }

  onEnd(result: FullResult) {
    if (!this.cases.size) return;
    const ids = [...this.cases.keys()].sort();
    console.log(`\nCases (${this.stage}, ${result.status}):`);
    for (const id of ids) {
      const c = this.cases.get(id) as Outcome;
      console.log(`  ${id.padEnd(16)} ${c.status.padEnd(8)} ${c.notes[0] ?? ''}`);
    }
    const run = findRun();
    if (run) writeFileSync(join(runDir(run.runId), `report-${this.stage}.json`), JSON.stringify(Object.fromEntries(this.cases), null, 2));
  }
}
