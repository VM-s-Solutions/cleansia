import { readdirSync, readFileSync } from 'fs';
import { join } from 'path';

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
