import { existsSync, readdirSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

/**
 * The page shell is declared once, in `common/page-wrapper.scss`; a partner page stylesheet that
 * re-declares the card width is the drift this pins. A media query names a viewport, not a card,
 * and a phone media query may stack a row and let its buttons fill it; the desktop rules may not.
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

const PARTNER_PAGES_DIR = join(findSolutionDir(), 'Cleansia.App/libs/shared/assets/src/styles/pages/cleansia-partner');

interface Block {
  selector: string;
  body: string;
}

function partnerPageStylesheets(): { name: string; scss: string }[] {
  return readdirSync(PARTNER_PAGES_DIR)
    .filter((name) => name.endsWith('.scss') && name !== 'index.scss')
    .map((name) => ({ name, scss: readFileSync(join(PARTNER_PAGES_DIR, name), 'utf8') }));
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

// The viewport header is not a card width; the rules inside the query still are.
function withoutMediaQueryHeaders(scss: string): string {
  return scss.replace(/@media[^{]*\{/g, '{');
}

function withoutMediaQueryBlocks(scss: string): string {
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

describe('partner page shell', () => {
  it('declares the card width once, in the shared page wrapper', () => {
    const offenders = partnerPageStylesheets()
      .filter(({ scss }) => /max-width:\s*1[0-9]{3}px/.test(withoutMediaQueryHeaders(scss)))
      .map(({ name }) => name);

    expect(offenders).toEqual([]);
  });

  it('lets no page stretch a button to the row outside a phone media query', () => {
    const offenders = partnerPageStylesheets()
      .filter(({ scss }) =>
        blocks(withoutMediaQueryBlocks(scss), /cleansia-button|\.cleansia-button/).some(({ body }) => /(min-)?width:\s*100%/.test(body))
      )
      .map(({ name }) => name);

    expect(offenders).toEqual([]);
  });
});
