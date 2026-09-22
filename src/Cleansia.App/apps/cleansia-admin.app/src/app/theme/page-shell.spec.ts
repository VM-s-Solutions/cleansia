import { existsSync, readdirSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

/**
 * The page shell is declared once, in `common/page-wrapper.scss`; a page stylesheet that
 * re-declares a card width, a card radius or a centred header is the drift this pins.
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

const STYLES_DIR = join(findSolutionDir(), 'Cleansia.App/libs/shared/assets/src/styles');
const ADMIN_PAGES_DIR = join(STYLES_DIR, 'pages/cleansia-admin');

// The one full-screen screen with no page wrapper: the unauthorized splash.
const NOT_SHELL_PAGES = new Set(['index.scss', 'unauthorized.component.scss']);

interface Block {
  selector: string;
  body: string;
}

function adminPageStylesheets(): { name: string; scss: string }[] {
  return readdirSync(ADMIN_PAGES_DIR)
    .filter((name) => name.endsWith('.scss') && !NOT_SHELL_PAGES.has(name))
    .map((name) => ({ name, scss: readFileSync(join(ADMIN_PAGES_DIR, name), 'utf8') }));
}

function blocks(scss: string, selector: RegExp): Block[] {
  const found: Block[] = [];
  const open = new RegExp(`(${selector.source})\\s*\\{`, 'g');
  let match: RegExpExecArray | null;
  while ((match = open.exec(scss)) !== null) {
    let depth = 1;
    let end = match.index + match[0].length;
    while (depth > 0 && end < scss.length) {
      if (scss[end] === '{') depth++;
      else if (scss[end] === '}') depth--;
      end++;
    }
    found.push({ selector: match[1], body: scss.slice(match.index + match[0].length, end - 1) });
  }
  return found;
}

// A phone media query may stack a row and let its buttons fill it; the desktop rules may not.
function withoutMediaQueries(scss: string): string {
  let out = scss;
  let match: RegExpExecArray | null;
  while ((match = /@media[^{]*\{/.exec(out)) !== null) {
    let depth = 1;
    let end = match.index + match[0].length;
    while (depth > 0 && end < out.length) {
      if (out[end] === '{') depth++;
      else if (out[end] === '}') depth--;
      end++;
    }
    out = out.slice(0, match.index) + out.slice(end);
  }
  return out;
}

describe('admin page shell', () => {
  it('declares the card width once, in the shared page wrapper', () => {
    const offenders = adminPageStylesheets()
      .filter(({ scss }) => /max-width:\s*1[0-9]{3}px/.test(scss))
      .map(({ name }) => name);

    expect(offenders).toEqual([]);
  });

  it('declares no card radius or background inside a page container', () => {
    const offenders = adminPageStylesheets()
      .filter(({ scss }) =>
        blocks(scss, /&__container/).some(({ body }) => /border-radius|background:\s*var\(--cleansia-white\)/.test(body))
      )
      .map(({ name }) => name);

    expect(offenders).toEqual([]);
  });

  it('centres no page header', () => {
    const offenders = adminPageStylesheets()
      .filter(({ scss }) => blocks(scss, /&__header/).some(({ body }) => /text-align:\s*center/.test(body)))
      .map(({ name }) => name);

    expect(offenders).toEqual([]);
  });

  it('keeps the shared page wrapper as the one card', () => {
    const wrapper = readFileSync(join(STYLES_DIR, 'common/page-wrapper.scss'), 'utf8');

    expect(wrapper).toMatch(/\.cleansia-page\s*\{/);
    expect(wrapper).toMatch(/\.page-wrapper\s*\{[^}]*max-width:\s*1400px/);
    expect(wrapper).toMatch(/&--narrow\s*\{[^}]*max-width:\s*1200px/);
  });

  it('sizes a button to its label unless asked otherwise', () => {
    const button = readFileSync(join(STYLES_DIR, 'components/cleansia-button.component.scss'), 'utf8');

    expect(button).not.toMatch(/min-width:\s*100%/);
  });

  it('lets no page stretch a button to the row outside a phone media query', () => {
    const offenders = adminPageStylesheets()
      .filter(({ scss }) =>
        blocks(withoutMediaQueries(scss), /cleansia-button|\.cleansia-button/).some(({ body }) => /(min-)?width:\s*100%/.test(body))
      )
      .map(({ name }) => name);

    expect(offenders).toEqual([]);
  });
});
