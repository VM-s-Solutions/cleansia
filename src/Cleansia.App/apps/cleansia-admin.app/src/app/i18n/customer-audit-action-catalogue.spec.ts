import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const ACTION_LABEL_ROOT = 'pages.audit_log.actions';

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

const SOLUTION_DIR = findSolutionDir();
const APP_SERVICES_DIR = join(SOLUTION_DIR, 'Cleansia.Core.AppServices');
const TIMELINE_QUERY_PATH = join(APP_SERVICES_DIR, 'Features/Auditing/GetActionTimeline.cs');
const I18N_DIR = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

// Read off disk rather than imported: the audit-log lib is lazy-loaded by the app shell, and a static
// import from the app is the boundary violation `check-module-boundaries.mjs` records.
const CATALOGUE_PATH = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-admin-features/audit-log/src/lib/customer-audit-actions.ts'
);

// `[AuditAction("customer.order.cancel", Audience = AuditAudience.Customer, ...)]` — the label is the
// first argument and the audience a named one, in any order after it.
const CUSTOMER_MARKER = /\[AuditAction\(\s*"([^"]+)"[^\]]*Audience\s*=\s*AuditAudience\.Customer/g;

// `EmployeeAuditAction.X => "employee.order.x"` inside GetActionTimeline.EmployeeActionLabel.
const EMPLOYEE_LABEL = /EmployeeAuditAction\.\w+\s*=>\s*"([^"]+)"/g;

function walkCsFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (name === 'bin' || name === 'obj') continue;
      walkCsFiles(full, out);
    } else if (name.endsWith('.cs')) {
      out.push(full);
    }
  }
  return out;
}

function backendCustomerRoster(): Set<string> {
  const labels = new Set<string>();
  for (const file of walkCsFiles(APP_SERVICES_DIR)) {
    const source = readFileSync(file, 'utf8');
    let match: RegExpExecArray | null;
    CUSTOMER_MARKER.lastIndex = 0;
    while ((match = CUSTOMER_MARKER.exec(source)) !== null) {
      labels.add(match[1]);
    }
  }
  return labels;
}

function backendEmployeeLabels(): Set<string> {
  const source = readFileSync(TIMELINE_QUERY_PATH, 'utf8');
  const labels = new Set<string>();
  let match: RegExpExecArray | null;
  EMPLOYEE_LABEL.lastIndex = 0;
  while ((match = EMPLOYEE_LABEL.exec(source)) !== null) {
    labels.add(match[1]);
  }
  return labels;
}

function catalogueList(name: 'CUSTOMER_AUDIT_ACTIONS' | 'EMPLOYEE_AUDIT_ACTIONS'): string[] {
  const source = readFileSync(CATALOGUE_PATH, 'utf8');
  const block = new RegExp(`export const ${name} = \\[([^\\]]*)\\] as const;`).exec(source);
  if (!block) throw new Error(`${name} not found in ${CATALOGUE_PATH}`);
  return [...block[1].matchAll(/'([^']+)'/g)].map((m) => m[1]);
}

const CUSTOMER_AUDIT_ACTIONS = catalogueList('CUSTOMER_AUDIT_ACTIONS');
const EMPLOYEE_AUDIT_ACTIONS = catalogueList('EMPLOYEE_AUDIT_ACTIONS');

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function resolve(tree: Record<string, unknown>, dotted: string): unknown {
  return dotted.split('.').reduce<unknown>((node, segment) => {
    if (node && typeof node === 'object') {
      return (node as Record<string, unknown>)[segment];
    }
    return undefined;
  }, tree);
}

function leafKeys(node: unknown, prefix = ''): string[] {
  if (!node || typeof node !== 'object') return [prefix];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafKeys(value, prefix ? `${prefix}.${key}` : key)
  );
}

describe('customer audit action catalogue', () => {
  const catalogue = [...CUSTOMER_AUDIT_ACTIONS, ...EMPLOYEE_AUDIT_ACTIONS];

  it('reads a sixteen-label customer catalogue and a two-label employee one off the lib', () => {
    expect(new Set(CUSTOMER_AUDIT_ACTIONS).size).toBe(16);
    expect(new Set(EMPLOYEE_AUDIT_ACTIONS).size).toBe(2);
  });

  it('covers every customer marker the backend carries, so no recorded act reaches the screen unlabelled', () => {
    const roster = backendCustomerRoster();

    expect(roster.size).toBeGreaterThanOrEqual(3);
    for (const label of roster) {
      expect(CUSTOMER_AUDIT_ACTIONS).toContain(label);
    }
  });

  it('carries exactly the employee labels the timeline query emits', () => {
    expect([...backendEmployeeLabels()].sort()).toEqual([...EMPLOYEE_AUDIT_ACTIONS].sort());
  });

  it.each(LOCALES)('%s translates every catalogued label under pages.audit_log.actions', (locale) => {
    const tree = readLocale(locale);
    for (const label of catalogue) {
      const value = resolve(tree, `${ACTION_LABEL_ROOT}.${label}`);
      expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
    }
  });

  it('carries no label the five locales translate but the catalogue does not know', () => {
    const catalogued = new Set<string>(catalogue);
    for (const locale of LOCALES) {
      const labels = leafKeys(resolve(readLocale(locale), ACTION_LABEL_ROOT));
      expect(labels.filter((label) => !catalogued.has(label))).toEqual([]);
    }
  });

  it.each([
    'pages.audit_log.segments',
    'pages.audit_log.customers',
    'pages.audit_log.timeline',
    'pages.customer_detail',
  ])('%s carries an identical, non-empty key set in all five locales', (root) => {
    const reference = leafKeys(resolve(readLocale('en'), root)).sort();
    expect(reference.length).toBeGreaterThan(0);
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      expect(leafKeys(resolve(tree, root)).sort()).toEqual(reference);
      for (const key of reference) {
        const value = resolve(tree, `${root}.${key}`);
        expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
      }
    }
  });
});
