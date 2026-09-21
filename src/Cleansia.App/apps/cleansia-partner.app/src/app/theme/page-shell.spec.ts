import { existsSync, readdirSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

/**
 * The page shell is declared once, in `common/page-wrapper.scss`; a partner page stylesheet that
 * re-declares the card width is the drift this pins. A media query names a viewport, not a card.
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

function partnerPageStylesheets(): { name: string; scss: string }[] {
  return readdirSync(PARTNER_PAGES_DIR)
    .filter((name) => name.endsWith('.scss') && name !== 'index.scss')
    .map((name) => ({ name, scss: readFileSync(join(PARTNER_PAGES_DIR, name), 'utf8') }));
}

function withoutMediaQueries(scss: string): string {
  return scss.replace(/@media[^{]*\{/g, '{');
}

describe('partner page shell', () => {
  it('declares the card width once, in the shared page wrapper', () => {
    const offenders = partnerPageStylesheets()
      .filter(({ scss }) => /max-width:\s*1[0-9]{3}px/.test(withoutMediaQueries(scss)))
      .map(({ name }) => name);

    expect(offenders).toEqual([]);
  });
});
