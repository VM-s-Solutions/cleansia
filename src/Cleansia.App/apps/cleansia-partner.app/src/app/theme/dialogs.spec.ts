import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { dirname, join, relative } from 'path';

/**
 * The partner dialogs take the admin dialog shape: every dynamic open carries the shared
 * `cleansia-dialog` skin on a named panel width, the fields sit on the form grid inside one
 * dialog body, the footer puts the outlined cancel or close before one primary that is never
 * green, orange or info-blue, and no dialog draws a header, title or footer of its own. The
 * partner bundle reads the panel and grid rules from the same partials the admin bundle does.
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
const PARTNER_PAGES_INDEX = join(PARTNER_PAGES_DIR, 'index.scss');

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

const dialogTemplates = walk(FEATURES_DIR, /-dialog\.component\.html$/);
const sources = walk(FEATURES_DIR, /(?<!\.spec)\.ts$/);

const blocks = (text: string, open: RegExp, close: string): string[] => {
  const found: string[] = [];
  let match: RegExpExecArray | null;
  const re = new RegExp(open.source, 'g');
  while ((match = re.exec(text)) !== null) {
    const end = text.indexOf(close, match.index);
    found.push(text.slice(match.index, end < 0 ? text.length : end + close.length));
  }
  return found;
};

describe('partner dialogs', () => {
  it('open every dynamic dialog on the shared skin and a named panel width, never an inline width', () => {
    const offenders = sources
      .filter((file) => {
        const source = read(file);
        return blocks(source, /\.dialogService\.open\(/, '});').some(
          (call) => !/styleClass: 'cleansia-dialog dialog-panel/.test(call) || /\bwidth:/.test(call) || /header: undefined/.test(call)
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

  it('let the control print its own errors and write a hint under the row', () => {
    const offenders = dialogTemplates
      .filter((file) => /\[showErrors\]="false"|<small class="[^"]*__(error|hint)"/.test(read(file)))
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('draw no header, title or footer of their own', () => {
    // The work-contract dialog loads its title with the document and shares its block with the admin one.
    const offenders = dialogTemplates
      .filter((file) => !/work-contract-dialog/.test(file))
      .filter((file) => /-dialog__(header|title|subtitle|footer)"/.test(read(file)))
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
    expect(walk(FEATURES_DIR, /-dialog\.component\.scss$/).map(rel)).toEqual([]);
  });

  it('fill no dialog button green, orange or info-blue', () => {
    const offenders = dialogTemplates
      .filter((file) => /severity\]?="'?(success|warn|info)'?"/.test(read(file)))
      .map(rel)
      .sort();

    expect(offenders).toEqual([]);
  });

  it('open every footer with the outlined cancel or close, the primary after it', () => {
    const offenders = dialogTemplates
      .filter((file) => {
        const html = read(file);
        const footers = blocks(html, /<div class="(dialog-actions|work-contract-dialog__footer)">/, '</div>');
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

  it('read the panel widths, the body, the footer and the form grid from the shared partials', () => {
    const index = read(PARTNER_PAGES_INDEX);
    expect(index).toMatch(/@use '[^']*\/dialog';/);
    expect(index).toMatch(/@use '[^']*\/form-page';/);
  });

  it('collapse the completion summary to one column on a phone, as the detail grid it sits on does', () => {
    // A page rule that re-declares the grid's columns on two classes outweighs the shared
    // breakpoints, so it must restate the phone collapse or the three columns hold at 390px.
    const scss = read(join(PARTNER_PAGES_DIR, 'orders.component.scss'));
    const start = scss.indexOf('.complete-order-dialog__times.detail-grid {');
    expect(start).toBeGreaterThanOrEqual(0);
    let depth = 0;
    let end = start;
    for (; end < scss.length; end++) {
      if (scss[end] === '{') depth++;
      if (scss[end] === '}' && --depth === 0) break;
    }
    const rule = scss.slice(start, end + 1);

    expect(rule).toMatch(/@media \(max-width: 767px\) \{\s*grid-template-columns: minmax\(0, 1fr\);/);
  });
});
