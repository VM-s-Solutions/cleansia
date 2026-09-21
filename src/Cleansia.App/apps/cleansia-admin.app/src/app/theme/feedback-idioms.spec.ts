import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * One confirmation dialog, one toast path, one loading state and one empty state, shared by the
 * admin and partner web apps. Each rule below pins a variant that used to live in a feature.
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
const FEATURE_DIRS = ['libs/cleansia-admin-features', 'libs/cleansia-partner-features'].map((d) => join(APP_DIR, d));
const SHELLS = ['cleansia-admin.app', 'cleansia-partner.app'];
const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'];

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, out);
    else if (/\.(ts|html)$/.test(name) && !name.endsWith('.spec.ts')) out.push(path);
  }
  return out;
}

const featureFiles = FEATURE_DIRS.flatMap((d) => walk(d));

function offenders(pattern: RegExp, files: string[] = featureFiles): string[] {
  return files
    .filter((file) => pattern.test(readFileSync(file, 'utf8')))
    .map((file) => relative(APP_DIR, file).replace(/\\/g, '/'))
    .sort();
}

describe('confirmation dialog', () => {
  it('is rendered by the shells alone, styled as the shared dialog', () => {
    expect(offenders(/<p-confirmDialog/i)).toEqual([]);
    for (const app of SHELLS) {
      const shell = readFileSync(join(APP_DIR, `apps/${app}/src/app/app.component.html`), 'utf8');
      expect(shell).toMatch(/<p-confirmDialog styleClass="cleansia-dialog" \/>/);
    }
  });

  it('is opened through DialogService, never through a feature-scoped ConfirmationService', () => {
    expect(offenders(/providers:\s*\[[^\]]*\bConfirmationService\b/)).toEqual([]);
    expect(offenders(/confirmationService\.confirm\(/)).toEqual([]);
  });

  it('reads its Yes and No from the bundle, never from a hardcoded PrimeNG block', () => {
    for (const app of SHELLS) {
      const config = readFileSync(join(APP_DIR, `apps/${app}/src/app/app.config.ts`), 'utf8');
      expect(config).toMatch(/providePrimeNgTranslation\(\)/);
      expect(config).not.toMatch(/translation:\s*\{/);
      for (const locale of LOCALES) {
        const bundle = JSON.parse(readFileSync(join(APP_DIR, `apps/${app}/src/assets/i18n/${locale}.json`), 'utf8'));
        expect(typeof bundle.primeng?.accept).toBe('string');
        expect(Object.keys(bundle.primeng.day_names)).toHaveLength(7);
        expect(Object.keys(bundle.primeng.month_names)).toHaveLength(12);
      }
    }
  });
});

describe('toasts', () => {
  it('leave the error toast to the interceptor: no per-feature error-key map or resolver', () => {
    expect(offenders(/_ERROR_KEY_MAP\b/)).toEqual([]);
    expect(offenders(/\bresolve(?!Api)\w+ErrorKey\(/)).toEqual([]);
  });

  it('name a key and let the snackbar translate it', () => {
    expect(offenders(/\.show(Success|Error)\(\s*this\.translate\.instant\(/)).toEqual([]);
  });
});

describe('loading state', () => {
  it('is the in-place loader — never a fixed overlay, never a layout-specific skeleton', () => {
    const loader = readFileSync(join(APP_DIR, 'libs/shared/assets/src/styles/components/cleansia-loader.component.scss'), 'utf8');
    expect(loader).not.toMatch(/position:\s*fixed/);
    expect(existsSync(join(APP_DIR, 'libs/shared/components/src/lib/cleansia-skeleton'))).toBe(false);
    expect(offenders(/<cleansia-(detail|form|dashboard|table)-skeleton/)).toEqual([]);
  });
});

describe('empty state', () => {
  it('is the shared class, not a page-local one', () => {
    expect(offenders(/class="(report-empty|refund-empty|order-photos__empty|currency-price-block__empty|cleansia-dispute-detail__empty)"/)).toEqual([]);
    const common = readFileSync(join(APP_DIR, 'libs/shared/assets/src/styles/common/index.scss'), 'utf8');
    expect(common).toMatch(/empty-state\.scss/);
    expect(common).toMatch(/not-found-state\.scss/);
  });
});

describe('cancel', () => {
  it('is global.actions.cancel in the admin bundle; the common.* namespace is gone', () => {
    for (const locale of LOCALES) {
      const bundle = JSON.parse(readFileSync(join(APP_DIR, `apps/cleansia-admin.app/src/assets/i18n/${locale}.json`), 'utf8'));
      expect(bundle.common).toBeUndefined();
      expect(typeof bundle.global.actions.cancel).toBe('string');
    }
    expect(offenders(/'common\.(save|cancel|edit|delete|confirm)'/)).toEqual([]);
    expect(offenders(/cancel_button' \| translate/)).toEqual([]);
  });
});
