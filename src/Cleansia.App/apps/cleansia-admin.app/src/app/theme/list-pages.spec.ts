import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The admin list pages share one shape: the page header with an h1 title and the filter drawer
 * before the create action, a table whose numeric, money and date columns are right-aligned, and
 * a page stylesheet that carries only what is page-specific. Each rule pins a drift that used to
 * live on one page.
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
const ADMIN_PAGES_DIR = join(APP_DIR, 'libs/shared/assets/src/styles/pages/cleansia-admin');

const LIST_TEMPLATES = [
  'admin-user-management/src/lib/admin-user-management/admin-user-management.component.html',
  'audit-log/src/lib/audit-log/audit-log.component.html',
  'audit-log/src/lib/customer-audit-list/customer-audit-list.component.html',
  'company-lifecycle/src/lib/company-lifecycle/company-lifecycle.component.html',
  'company-management/src/lib/company-info-list/company-info-list.component.html',
  'company-settings/src/lib/company-settings/company-settings.component.html',
  'country-management/src/lib/country-management/country-management.component.html',
  'country-management/src/lib/service-area-management/service-area-management.component.html',
  'currency-management/src/lib/currency-management/currency-management.component.html',
  'data-protection/src/lib/data-protection/data-protection.component.html',
  'disputes-management/src/lib/disputes-management/disputes-management.component.html',
  'employee-document-config/src/lib/deletion-requests/deletion-requests.component.html',
  'employee-document-config/src/lib/document-requirements/document-requirements.component.html',
  'employee-management/src/lib/employee-management/employee-management.component.html',
  'extra-management/src/lib/extra-management/extra-management.component.html',
  'fiscal-failures/src/lib/fiscal-failures-list/fiscal-failures-list.component.html',
  'invoice-management/src/lib/invoice-management/invoice-management.component.html',
  'language-management/src/lib/language-management/language-management.component.html',
  'legal-documents/src/lib/legal-documents/legal-documents.component.html',
  'loyalty-promo-codes/src/lib/promo-codes-list/promo-codes-list.component.html',
  'loyalty-referrals/src/lib/referrals-list/referrals-list.component.html',
  'loyalty-tier-configs/src/lib/tier-configs/tier-configs.component.html',
  'membership-plan-management/src/lib/membership-plan-list/membership-plan-list.component.html',
  'notifications/src/lib/notifications/notifications.component.html',
  'order-management/src/lib/order-management/order-management.component.html',
  'package-management/src/lib/package-management/package-management.component.html',
  'pay-config-management/src/lib/pay-config-management/pay-config-management.component.html',
  'pay-periods/src/lib/pay-period-management/pay-period-management.component.html',
  'reports/src/lib/reports/reports.component.html',
  'service-management/src/lib/service-management/service-management.component.html',
  'template-management/src/lib/template-management.component.html',
];

// The page stylesheets of the list pages; one that no longer exists carries nothing page-specific.
const LIST_STYLESHEETS = [
  'admin-user-management.component.scss',
  'audit-log.component.scss',
  'company-lifecycle.component.scss',
  'company-management.component.scss',
  'company-settings.component.scss',
  'country-management.component.scss',
  'currency-management.component.scss',
  'data-protection.component.scss',
  'disputes-management.component.scss',
  'employee-management.component.scss',
  'extra-management.component.scss',
  'invoice-management.component.scss',
  'language-management.component.scss',
  'legal-documents.component.scss',
  'membership-plan-list.component.scss',
  'notifications.component.scss',
  'order-management.component.scss',
  'package-management.component.scss',
  'pay-config-management.component.scss',
  'pay-period-management.component.scss',
  'reports.component.scss',
  'service-area-management.component.scss',
  'service-management.component.scss',
  'template-list.component.scss',
  'template-management.component.scss',
];

// A column whose id names a figure, an amount or a count, or ends in a stamp's suffix.
const FIGURE_COLUMN_ID =
  /(price|amount|total|count|rating|discount|threshold|percentage|hours|days|points|awarded|pay|revenue|orders?|range|tender|credit|card|limit|employees|perks)$|^(perUser|validity|lastModified)$|^duration/i;
const STAMP_COLUMN_ID = /(On|At|Date|DateTime|From)$/;
// Columns whose id names no figure but whose cell prints one; `value` cannot join the pattern
// because the company-settings column of that id prints a setting's text.
const NAMED_FIGURE_COLUMNS = new Set([
  'libs/cleansia-admin-features/company-lifecycle/src/lib/company-lifecycle/company-lifecycle.models.ts → value',
]);
const isNumericColumn = (file: string, id: string): boolean =>
  FIGURE_COLUMN_ID.test(id) || STAMP_COLUMN_ID.test(id) || NAMED_FIGURE_COLUMNS.has(`${file} → ${id}`);

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, out);
    else out.push(path);
  }
  return out;
}

interface ColumnLiteral {
  file: string;
  id: string;
  body: string;
}

// Every `{ id: '…', … }` object literal in a file, with its body up to the matching brace; a table
// column is the one that also names its field and header.
function columnLiterals(file: string): ColumnLiteral[] {
  const source = readFileSync(file, 'utf8');
  const found: ColumnLiteral[] = [];
  const open = /\{\s*id:\s*'([^']+)'/g;
  let match: RegExpExecArray | null;
  while ((match = open.exec(source)) !== null) {
    let depth = 1;
    let end = match.index + 1;
    while (depth > 0 && end < source.length) {
      if (source[end] === '{') depth++;
      else if (source[end] === '}') depth--;
      end++;
    }
    found.push({
      file: relative(APP_DIR, file).replace(/\\/g, '/'),
      id: match[1],
      body: source.slice(match.index, end),
    });
  }
  return found;
}

const listTemplate = (path: string) => readFileSync(join(FEATURES_DIR, path), 'utf8');

describe('admin list pages', () => {
  it('right-align every numeric, money and date column', () => {
    const files = walk(FEATURES_DIR).filter(
      (file) => /\.(models|component)\.ts$/.test(file) && !file.endsWith('.spec.ts') && !/detail|timeline/.test(file)
    );
    const offenders = files
      .flatMap(columnLiterals)
      .filter(({ body }) => /\bfield:/.test(body) && /\bheader:/.test(body))
      .filter(({ file, id, body }) => isNumericColumn(file, id) && !/numeric:\s*true/.test(body))
      .map(({ file, id }) => `${file} → ${id}`)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('open with the shared page header and an h1 title', () => {
    const offenders = LIST_TEMPLATES.filter((path) => {
      const html = listTemplate(path);
      return !/class="cleansia-page-header"/.test(html) || !/\[level\]="1"/.test(html);
    });

    expect(offenders).toEqual([]);
  });

  it('put the filter drawer before the create action in the header', () => {
    const offenders = LIST_TEMPLATES.filter((path) => {
      const html = listTemplate(path);
      const drawer = html.indexOf('<cleansia-filter-drawer');
      const create = html.search(/<cleansia-button[^>]*icon="pi pi-plus"/);
      return drawer >= 0 && create >= 0 && create < drawer;
    });

    expect(offenders).toEqual([]);
  });

  it('keep the list stylesheets to what is page-specific: no filter panel, no button restyle, no shell copy', () => {
    const offenders = LIST_STYLESHEETS.filter((name) => existsSync(join(ADMIN_PAGES_DIR, name))).filter((name) => {
      const scss = readFileSync(join(ADMIN_PAGES_DIR, name), 'utf8');
      return /\.filter-panel|\.p-button\b|min-height:\s*100vh/.test(scss);
    });

    expect(offenders).toEqual([]);
  });
});
