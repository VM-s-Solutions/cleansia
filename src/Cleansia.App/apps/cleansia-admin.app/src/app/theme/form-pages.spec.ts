import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The admin create and edit forms share one shape: the shared page header with an h1 title,
 * fields on the one twelve-column form grid declared in `_form-page.scss`, a hint or a
 * cross-field message on a full-width line under its row rather than under one field, and a
 * footer that puts the secondary action before the primary. Each rule pins a drift that used to
 * live on one form.
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

const FORM_TEMPLATES = [
  'admin-profile/src/lib/admin-profile/admin-profile.component.html',
  'admin-user-management/src/lib/admin-user-form/admin-user-form.component.html',
  'company-management/src/lib/company-info-form/company-info-form.component.html',
  'country-management/src/lib/country-form/country-form.component.html',
  'currency-management/src/lib/currency-form/currency-form.component.html',
  'extra-management/src/lib/extra-form/extra-form.component.html',
  'language-management/src/lib/language-form/language-form.component.html',
  'loyalty-promo-codes/src/lib/promo-code-form/promo-code-form.component.html',
  'marketing/src/lib/sitewide-push-form/sitewide-push-form.component.html',
  'membership-plan-management/src/lib/membership-plan-form/membership-plan-form.component.html',
  'package-management/src/lib/package-form/package-form.component.html',
  'pay-config-management/src/lib/pay-config-form/pay-config-form.component.html',
  'service-management/src/lib/service-form/service-form.component.html',
];

// The two forms that are not reached from a list: the profile and the marketing push.
const FORMS_WITHOUT_BACK = new Set([
  'admin-profile/src/lib/admin-profile/admin-profile.component.html',
  'marketing/src/lib/sitewide-push-form/sitewide-push-form.component.html',
]);

const formTemplate = (path: string) => readFileSync(join(FEATURES_DIR, path), 'utf8');

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, out);
    else if (name.endsWith('.html')) out.push(path);
  }
  return out;
}

const rel = (file: string) => relative(APP_DIR, file).replace(/\\/g, '/');

describe('admin form pages', () => {
  it('open with the shared page header and an h1 title, the back control beside it', () => {
    const offenders = FORM_TEMPLATES.filter((path) => {
      const html = formTemplate(path);
      const backBesideTitle = FORMS_WITHOUT_BACK.has(path) || /class="cleansia-detail-title"/.test(html);
      return !/class="cleansia-page-header"/.test(html) || !/\[level\]="1"/.test(html) || !backBesideTitle;
    });

    expect(offenders).toEqual([]);
  });

  it('lay their fields on the shared form grid inside one form column', () => {
    const offenders = FORM_TEMPLATES.filter((path) => {
      const html = formTemplate(path);
      return !/class="cleansia-form"/.test(html) || !/class="form-grid"/.test(html);
    });

    expect(offenders).toEqual([]);
  });

  it('write a hint or a cross-field message under the row, never under one field', () => {
    const offenders = FORM_TEMPLATES.filter((path) =>
      /class="(field-help|field-error|form-field__hint|form-label)"/.test(formTemplate(path))
    );

    expect(offenders).toEqual([]);
  });

  it('put the cancel action before the primary in one footer', () => {
    const offenders = FORM_TEMPLATES.filter((path) => {
      const html = formTemplate(path);
      const footer = html.indexOf('class="form-actions"');
      if (footer < 0) return true;
      const cancel = html.indexOf("'global.actions.cancel' | translate", footer);
      const submit = html.indexOf('type="submit"', footer);
      return cancel >= 0 && submit >= 0 && submit < cancel;
    });

    expect(offenders).toEqual([]);
  });

  it('declare the form grid, its spans and the footer once, in the shared form partial', () => {
    const partial = readFileSync(join(ADMIN_PAGES_DIR, '_form-page.scss'), 'utf8');
    expect(partial).toMatch(/\.form-grid\s*\{[^}]*grid-template-columns:\s*repeat\(12,/);
    expect(partial).toMatch(/\.form-actions\s*\{/);

    const offenders = readdirSync(ADMIN_PAGES_DIR)
      .filter((name) => name.endsWith('.component.scss'))
      .filter((name) => {
        const scss = readFileSync(join(ADMIN_PAGES_DIR, name), 'utf8');
        return /\.form-grid\b|\.form-field\b|\.form-actions\b|\.form-hint\b|min-height:\s*100vh/.test(scss);
      });

    expect(offenders).toEqual([]);
  });

  it('size a form column by its span, never by a pixel track', () => {
    const offenders = readdirSync(ADMIN_PAGES_DIR)
      .filter((name) => /-form\.component\.scss$/.test(name))
      .filter((name) => /grid-template-columns:[^;]*(\d+px|repeat\(\d)/.test(readFileSync(join(ADMIN_PAGES_DIR, name), 'utf8')));

    expect(offenders).toEqual([]);
  });

  it('use the built-in control flow: no structural *ngIf is left in an admin template', () => {
    const offenders = walk(FEATURES_DIR)
      .filter((file) => /\*ngIf=/.test(readFileSync(file, 'utf8')))
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
  });
});
