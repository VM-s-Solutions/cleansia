import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * One confirmation dialog: the shell's, opened through the shared DialogService. A feature that
 * provides its own ConfirmationService and draws its own `<p-confirmDialog>` shows a second dialog
 * the shell's styling never reaches. The admin and partner apps pin the same in
 * `theme/feedback-idioms.spec.ts`.
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
const FEATURE_DIR = join(APP_DIR, 'libs/cleansia-customer-features');

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) walk(path, out);
    else if (/\.(ts|html)$/.test(name) && !name.endsWith('.spec.ts')) out.push(path);
  }
  return out;
}

const featureFiles = walk(FEATURE_DIR);

function offenders(pattern: RegExp): string[] {
  return featureFiles
    .filter((file) => pattern.test(readFileSync(file, 'utf8')))
    .map((file) => relative(APP_DIR, file).replace(/\\/g, '/'))
    .sort();
}

describe('confirmation dialog', () => {
  it('is rendered by the shell alone, styled as the shared dialog', () => {
    expect(offenders(/<p-confirmDialog/i)).toEqual([]);
    const shell = readFileSync(join(APP_DIR, 'apps/cleansia.app/src/app/app.html'), 'utf8');
    expect(shell).toMatch(/<p-confirmDialog styleClass="cleansia-dialog">/);
  });

  it("is opened through DialogService — no feature imports PrimeNG's ConfirmationService or its dialog", () => {
    expect(offenders(/\bConfirmationService\b|from 'primeng\/confirmdialog'/)).toEqual([]);
  });
});
