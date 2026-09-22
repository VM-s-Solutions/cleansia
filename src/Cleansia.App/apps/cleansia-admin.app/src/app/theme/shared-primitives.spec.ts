import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * One status badge, one date format and one money format on the admin pages. A status is
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
const FEATURES_DIR = join(APP_DIR, 'libs/cleansia-admin-features');
const STYLES_DIR = join(APP_DIR, 'libs/shared/assets/src/styles');
const ADMIN_PAGES_DIR = join(STYLES_DIR, 'pages/cleansia-admin');

// The one amount still printed with toFixed: the membership plan price, "199.00 CZK" where the
// ledger prints "199,00 Kč". It reads formatMoney once its list ticket lands; until then it is
// named here so the rule holds everywhere else.
const AMOUNT_BYPASS_TOLERATED = new Set([
  'libs/cleansia-admin-features/membership-plan-management/src/lib/membership-plan-list/membership-plan-list.models.ts',
]);

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
const pageStylesheets = readdirSync(ADMIN_PAGES_DIR)
  .filter((name) => name.endsWith('.scss'))
  .map((name) => join(ADMIN_PAGES_DIR, name));

function offenders(pattern: RegExp, files: string[]): string[] {
  return files
    .filter((file) => pattern.test(read(file)))
    .map(rel)
    .sort();
}

describe('admin status badge', () => {
  it('is the shared component on the shared tones — no page-local badge class or class resolver', () => {
    expect(offenders(/class="[^"]*\b\w+-status-badge\b|StatusClass\(|<p-tag\b|<p-badge\b/, templates)).toEqual([]);
    expect(offenders(/\bget\w*StatusClass\b|['"`][\w-]+-status-badge['"`]/, sources)).toEqual([]);
  });

  it('is drawn once, in the shared partial, and no page stylesheet colours a status of its own', () => {
    const partial = read(join(STYLES_DIR, 'common/status-badge.scss'));
    for (const tone of ['neutral', 'info', 'success', 'warning', 'danger']) {
      expect(partial).toMatch(new RegExp(`&--${tone}\\s*\\{`));
    }
    expect(partial).not.toMatch(/:hover|transform:/);

    expect(offenders(/\.status-badge\b|-status-badge\b/, pageStylesheets).map((file) => file.split('/').pop())).toEqual([]);
  });
});

describe('admin dates', () => {
  it('print through formatDate in the session language — no toLocale call, no DatePipe pattern', () => {
    expect(offenders(/\.toLocale(Date)?String\(/, [...sources, ...templates])).toEqual([]);
    expect(offenders(/\bDatePipe\b/, sources)).toEqual([]);
    expect(offenders(/\|\s*date\s*:/, templates)).toEqual([]);
  });
});

describe('admin money', () => {
  it('print through formatMoney in the row currency — no toFixed on an amount, no en-GB pin', () => {
    const amounts = [...sources, ...templates].filter((file) => !AMOUNT_BYPASS_TOLERATED.has(rel(file)));
    expect(offenders(/\.toFixed\(2\)/, amounts)).toEqual([]);
    expect(offenders(/Intl\.NumberFormat\(\s*['"]en-GB['"]/, sources)).toEqual([]);
    expect([...AMOUNT_BYPASS_TOLERATED].filter((path) => !existsSync(join(APP_DIR, path)))).toEqual([]);
  });
});
