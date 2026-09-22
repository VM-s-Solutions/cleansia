import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The admin dialogs share one shape: the shared `cleansia-dialog` skin on one of three named
 * panel widths, a body that reads lede → fields on the form grid → summary, and a footer that
 * puts the outlined cancel before one primary — red only when the act is destructive, never
 * green, orange or info-blue. Each rule pins a drift that used to live on one dialog.
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

// A template that mounts a dialog inline, or a component a facade opens as one.
const dialogTemplates = templates.filter((file) => /<p-dialog\b/.test(read(file)) || /-dialog\.component\.html$/.test(file));

function offenders(pattern: RegExp, files: string[]): string[] {
  return files
    .filter((file) => pattern.test(read(file)))
    .map(rel)
    .sort();
}

const blocks = (html: string, open: RegExp, close: string): string[] => {
  const found: string[] = [];
  let match: RegExpExecArray | null;
  const re = new RegExp(open.source, 'g');
  while ((match = re.exec(html)) !== null) {
    const end = html.indexOf(close, match.index);
    found.push(html.slice(match.index, end < 0 ? html.length : end + close.length));
  }
  return found;
};

describe('admin dialogs', () => {
  it('mount every inline dialog on the shared skin and a named panel width, never an inline width', () => {
    const offenders = dialogTemplates
      .filter((file) => {
        const html = read(file);
        return blocks(html, /<p-dialog\b/, '>').some(
          (tag) => !/styleClass="cleansia-dialog dialog-panel/.test(tag) || /\[style\]=/.test(tag) || !/\[draggable\]="false"/.test(tag)
        );
      })
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('open every dynamic dialog on the shared skin and a named panel width, never an inline width', () => {
    const offenders = sources
      .filter((file) => {
        const source = read(file);
        return blocks(source, /\.dialogService\.open\(/, '});').some(
          (call) => !/styleClass: 'cleansia-dialog dialog-panel/.test(call) || /\bwidth:/.test(call)
        );
      })
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('lay their fields on the shared form grid inside one dialog body', () => {
    const offenders = dialogTemplates
      .filter((file) => {
        const html = read(file);
        return /<cleansia-(text-input|textarea|select|calendar)\b/.test(html) && (!/class="dialog-body"/.test(html) || !/class="form-grid"/.test(html));
      })
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('write a hint or a message under the row, never in a dialog-local class', () => {
    expect(offenders(/class="(field-help|field-error|field-warning|static-label|static-value|dialog-description)"/, dialogTemplates)).toEqual([]);
  });

  it('fill no dialog button green, orange or info-blue', () => {
    expect(offenders(/severity="(success|warn|info)"/, dialogTemplates)).toEqual([]);
  });

  it('open every footer with the outlined cancel or close, the primary after it', () => {
    const offenders = dialogTemplates
      .filter((file) => {
        const html = read(file);
        // The skin's footer template, the in-content footer, or the shared work-contract dialog's own.
        const footers = [
          ...blocks(html, /<ng-template pTemplate="footer">/, '</ng-template>'),
          ...blocks(html, /<div class="(dialog-actions|work-contract-dialog__footer)">/, '</div>'),
        ];
        if (footers.length === 0) return true;
        return footers.some((footer) => {
          const buttons = blocks(footer, /<cleansia-button/, '/>');
          return buttons.length === 0 || !/'global\.actions\.(cancel|close)' \| translate/.test(buttons[0]) || !/\[outlined\]="true"/.test(buttons[0]);
        });
      })
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('declare the panel widths, the body and the in-content footer once, in the shared dialog partial', () => {
    const partial = read(join(ADMIN_PAGES_DIR, '_dialog.scss'));
    expect(partial).toMatch(/\.cleansia-dialog\.dialog-panel\s*\{/);
    expect(partial).toMatch(/\.dialog-body\s*\{/);
    expect(partial).toMatch(/\.cleansia-dialog \.dialog-actions\s*\{/);

    const offenders = readdirSync(ADMIN_PAGES_DIR)
      .filter((name) => name.endsWith('.component.scss'))
      .filter((name) => /-dialog\b|__dialog\b|\.dialog-|__create-form|__city-form|override-form/.test(read(join(ADMIN_PAGES_DIR, name))));

    expect(offenders).toEqual([]);
  });
});
