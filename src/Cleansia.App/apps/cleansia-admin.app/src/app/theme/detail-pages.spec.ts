import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The admin detail pages share one shape: the page header with the back control beside an h1
 * title and the page's secondary actions on the right, label/value pairs on the shared detail
 * grid with no trailing colon, and an entity's actions as one row of content-sized buttons with
 * no success, warn or info fill. Each rule pins a drift that used to live on one page.
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

const DETAIL_TEMPLATES = [
  'audit-log/src/lib/audit-entry/audit-entry.component.html',
  'audit-log/src/lib/customer-audit-entry/customer-audit-entry.component.html',
  'audit-log/src/lib/resource-history/resource-history.component.html',
  'disputes-management/src/lib/dispute-detail/dispute-detail.component.html',
  'employee-management/src/lib/employee-detail/employee-detail.component.html',
  'invoice-management/src/lib/invoice-detail/invoice-detail.component.html',
  'loyalty-promo-codes/src/lib/promo-code-detail/promo-code-detail.component.html',
  'loyalty-user-detail/src/lib/user-loyalty-detail/user-loyalty-detail.component.html',
  'order-management/src/lib/order-detail/order-detail.component.html',
  'pay-periods/src/lib/pay-period-detail/pay-period-detail.component.html',
  'template-management/src/lib/email-type-detail/email-type-detail.component.html',
];

// The dialogs keep their own footer shape; every other admin template is a page or a section.
const DIALOG_TEMPLATE = /-dialog\.component\.html$/;

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, out);
    else if (name.endsWith('.html')) out.push(path);
  }
  return out;
}

const templates = walk(FEATURES_DIR);
const rel = (file: string) => relative(APP_DIR, file).replace(/\\/g, '/');

function offenders(pattern: RegExp, files: string[] = templates): string[] {
  return files
    .filter((file) => pattern.test(readFileSync(file, 'utf8')))
    .map(rel)
    .sort();
}

describe('admin detail pages', () => {
  it('open with the shared page header, the back control beside an h1 title', () => {
    const missing = DETAIL_TEMPLATES.filter((path) => {
      const html = readFileSync(join(FEATURES_DIR, path), 'utf8');
      return (
        !/class="cleansia-page-header"/.test(html) ||
        !/class="cleansia-detail-title"/.test(html) ||
        !/\[level\]="1"/.test(html)
      );
    });

    expect(missing).toEqual([]);
  });

  it('write no label with a trailing colon on the detail grid', () => {
    expect(offenders(/detail-grid__label"[^>]*>[^<]*:\s*<\//)).toEqual([]);
  });

  it('carry no inline style attribute', () => {
    expect(offenders(/\sstyle="/)).toEqual([]);
  });

  it('fill no button green, orange or info-blue outside a dialog', () => {
    const pages = templates.filter((file) => !DIALOG_TEMPLATE.test(file));
    expect(offenders(/severity="(success|warn|info)"/, pages)).toEqual([]);
  });
});
