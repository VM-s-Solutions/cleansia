import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The partner pages print a status, a date and an amount the way the admin pages do: a status is
 * `<cleansia-status-badge>` on the five shared tones; a date prints through `formatDate` in the
 * session's language; an amount prints through `formatMoney` in the row's currency. Each rule
 * pins a bypass that used to live on one page.
 */

function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const APP_DIR = join(findSolutionDir(), 'Cleansia.App');
const FEATURES_DIR = join(APP_DIR, 'libs/cleansia-partner-features');
const PARTNER_PAGES_DIR = join(APP_DIR, 'libs/shared/assets/src/styles/pages/cleansia-partner');

// The one date still printed through DatePipe: the work-contract version line's effective day,
// "21.09.2026" in every language where the admin dialog prints "21. 9. 2026". It reads
// formatDate(…, 'utcDate') once its dialog ticket lands; until then it is named here so the rule
// holds everywhere else, and the rule reads it back so the entry cannot outlive the bypass.
const DATE_PIPE_TOLERATED = [
  'libs/cleansia-partner-features/orders/src/lib/components/work-contract-dialog/work-contract-dialog.component.html',
  'libs/cleansia-partner-features/orders/src/lib/components/work-contract-dialog/work-contract-dialog.component.ts',
];

function walk(dir: string, pattern: RegExp, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, pattern, out);
    else if (pattern.test(name)) out.push(path);
  }
  return out;
}

const rel = (file: string) => relative(APP_DIR, file).replace(/\\/g, '/');
const read = (file: string) => readFileSync(file, 'utf8');

const templates = walk(FEATURES_DIR, /\.html$/);
const sources = walk(FEATURES_DIR, /(?<!\.spec)\.ts$/);
const pageStylesheets = readdirSync(PARTNER_PAGES_DIR)
  .filter((name) => name.endsWith('.scss'))
  .map((name) => join(PARTNER_PAGES_DIR, name));

function offenders(pattern: RegExp, files: string[]): string[] {
  return files
    .filter((file) => pattern.test(read(file)))
    .map(rel)
    .sort();
}

describe('partner status badge', () => {
  it('is the shared component on the shared tones — no page-local badge class or class resolver', () => {
    expect(offenders(/class="[^"]*\b\w+-status-badge\b|StatusClass\(|<p-tag\b|<p-badge\b/, templates)).toEqual([]);
    expect(offenders(/\bget\w*StatusClass\b|['"`][\w-]+-status-badge['"`]/, sources)).toEqual([]);
  });

  it('is coloured by no partner page stylesheet', () => {
    expect(offenders(/\.status-badge\b|-status-badge\b/, pageStylesheets).map((file) => file.split('/').pop())).toEqual([]);
  });
});

describe('partner dates', () => {
  it('print through formatDate in the session language — no toLocale call, no DatePipe pattern', () => {
    expect(offenders(/\.toLocale(Date)?String\(/, [...sources, ...templates])).toEqual([]);
    expect(offenders(/\bDatePipe\b|\|\s*date\s*:/, [...sources, ...templates])).toEqual(DATE_PIPE_TOLERATED);
  });
});

describe('partner money', () => {
  it('print through formatMoney in the row currency — no toFixed on an amount, no en-GB pin', () => {
    expect(offenders(/\.toFixed\(2\)/, [...sources, ...templates])).toEqual([]);
    expect(offenders(/Intl\.NumberFormat\(\s*['"]en-GB['"]/, sources)).toEqual([]);
  });
});
