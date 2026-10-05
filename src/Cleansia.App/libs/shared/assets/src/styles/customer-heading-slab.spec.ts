import { readdirSync, readFileSync } from 'fs';
import { join } from 'path';
import { compile } from 'sass';

// --cl-heading is Sky700 in light mode and Sky300 (#7dd3fc) in dark, so a literal white ink on a slab
// painted with it reads 5.9 light and 1.7 dark. The ink that reads on both is the card ground,
// --cl-surface, which flips with the slab: white on Sky700, #0f1b2d on Sky300 (10.4).
describe('the customer stylesheets — ink on the --cl-heading slab', () => {
  const stylesheets = [
    ...readdirSync(join(__dirname, 'pages/cleansia-customer'))
      .filter((name) => name.endsWith('.scss'))
      .map((name) => join(__dirname, 'pages/cleansia-customer', name)),
    ...readdirSync(join(__dirname, 'components'))
      .filter((name) => name.startsWith('cleansia-customer') && name.endsWith('.scss'))
      .map((name) => join(__dirname, 'components', name)),
  ];

  /** Each rule painting the slab, with the ink it declares beside it, if any. */
  const slabs = stylesheets.flatMap((path) => {
    const lines = readFileSync(path, 'utf8').split('\n');
    return lines.flatMap((line, at) => {
      if (!/^\s*background(-color)?:\s*var\(--cl-heading\b/.test(line)) return [];
      // The declarations at this rule's own depth: back to its `{`, on to its `}`.
      let start = at;
      for (let depth = 0; start > 0; start--) {
        depth += (lines[start - 1].match(/\}/g) ?? []).length;
        depth -= (lines[start - 1].match(/\{/g) ?? []).length;
        if (depth < 0) break;
      }
      let end = at;
      for (let depth = 0; end < lines.length - 1; end++) {
        depth += (lines[end + 1].match(/\{/g) ?? []).length;
        depth -= (lines[end + 1].match(/\}/g) ?? []).length;
        if (depth < 0) break;
      }
      let depth = 0;
      const ink = lines.slice(start, end + 1).find((own) => {
        const atOwnDepth = depth === 0;
        depth += (own.match(/\{/g) ?? []).length - (own.match(/\}/g) ?? []).length;
        return atOwnDepth && /^\s*color:/.test(own);
      });
      return ink ? [{ where: `${path.split('/styles/')[1]}:${at + 1}`, ink: ink.trim() }] : [];
    });
  });

  it('finds the slabs it guards', () => {
    expect(slabs.map((slab) => slab.where)).toEqual(
      expect.arrayContaining([
        expect.stringMatching(/^pages\/cleansia-customer\/_disputes\.scss:/),
        expect.stringMatching(/^pages\/cleansia-customer\/_orders\.scss:/),
        expect.stringMatching(/^pages\/cleansia-customer\/_rewards\.scss:/),
        expect.stringMatching(/^pages\/cleansia-customer\/_order-detail\.scss:/),
        expect.stringMatching(/^pages\/cleansia-customer\/_profile\.scss:/),
        expect.stringMatching(/^components\/cleansia-customer-navbar\.component\.scss:/),
      ]),
    );
  });

  it('inks every one with the card ground, never a literal white', () => {
    const misinked = slabs.filter((slab) => !/^color: var\(--cl-surface\b/.test(slab.ink));

    expect(misinked).toEqual([]);
  });
});

// A picked chip is the slab; its base's `:hover` (0,2,0) outranks its `--on` (0,1,0), so a hover rule
// that does not skip the picked one swaps the picked border for the pale hover tint.
describe('the customer stylesheet — a picked slab under the pointer', () => {
  const css = compile(join(__dirname, 'cleansia-customer.scss'), {
    quietDeps: true,
    loadPaths: [join(__dirname, '../../../../../node_modules')],
  }).css;
  const rules = Array.from(css.matchAll(/([^{}]+)\{([^{}]*)\}/g), ([, selector, body]) => ({
    selectors: selector.split(',').map((one) => one.trim()),
    body,
  }));

  /** Each `.x--on` painted with the slab, with its base `.x`. */
  const picked = rules
    .filter(({ body }) => /background(-color)?:\s*var\(--cl-heading\b/.test(body))
    .flatMap(({ selectors }) => selectors)
    .flatMap((selector) => {
      const match = selector.match(/^(\.[\w-]+)--on$/);
      return match ? [{ base: match[1], on: selector }] : [];
    });

  it('finds the picked chips it guards', () => {
    expect(picked.map(({ on }) => on)).toEqual(
      expect.arrayContaining([
        '.cl-dsp__reason--on',
        '.cl-rwd__chip--on',
        '.customer-orders__chip--on',
        '.cl-rec__chip--on',
        '.cl-rec__pick--on',
      ]),
    );
  });

  it('skips the picked one in every hover rule that changes the border', () => {
    const paling = picked.flatMap(({ base, on }) =>
      rules
        .filter(({ body }) => /border(-color)?:/.test(body))
        .flatMap(({ selectors }) => selectors)
        .filter((selector) => selector.startsWith(`${base}:hover`) && !selector.includes(`:not(${on})`)),
    );

    expect(paling).toEqual([]);
  });
});

// White on the accent: the plan flag read 4.1 light and 2.1 after dark (Sky400); the account menu's
// avatar, on a Sky600→Sky500 gradient, 4.1 to 2.8 (2.1 after dark). Both now take the slab.
describe('the customer stylesheet — the plan flag and the account menu avatar', () => {
  const css = compile(join(__dirname, 'cleansia-customer.scss'), {
    quietDeps: true,
    loadPaths: [join(__dirname, '../../../../../node_modules')],
  }).css;
  const bodies = (selector: string): string[] =>
    Array.from(css.matchAll(/([^{}]+)\{([^{}]*)\}/g))
      .filter(([, selectors]) => selectors.split(',').some((one) => one.trim() === selector))
      .map(([, , body]) => body);

  it.each([
    ['the membership "your plan" flag', '.cl-mbr__option-flag'],
    ['the account menu avatar', '.customer-navbar__user-avatar'],
  ])('paints %s with the slab and inks it with the card ground, in both themes', (_, selector) => {
    const own = bodies(selector).join('');

    expect(own).toMatch(/background: var\(--cl-heading\b/);
    expect(own).toMatch(/(?:^|;)\s*color: var\(--cl-surface\b/);
    expect(bodies(`:root.dark-mode ${selector}`)).toEqual([]);
  });
});
