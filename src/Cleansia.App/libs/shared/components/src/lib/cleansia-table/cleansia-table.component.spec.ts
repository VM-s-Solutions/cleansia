import { readFileSync } from 'fs';
import { join } from 'path';

/**
 * The row actions and the pager are 2rem glyph boxes; a thumb needs 44px. The hit area is the
 * shared ring grown past each box, so the boxes keep their size and the rows their rhythm. jsdom
 * lays nothing out, so the pins read the stylesheet, which is declared an input of this project's
 * test target.
 */
describe('CleansiaTableComponent — control touch floor', () => {
  const scss = readFileSync(
    join(__dirname, '../../../../assets/src/styles/components/cleansia-table.component.scss'),
    'utf-8'
  );

  it('gives each row action the shared 44px hit area around its 2rem box', () => {
    expect(scss).toMatch(
      /\.action-btn\s*\{\s*width:\s*2rem;\s*height:\s*2rem;\s*padding:\s*0;\s*position:\s*relative;\s*@include touch-target;/
    );
  });

  it('gives each pager button the shared 44px hit area around its 2rem box', () => {
    expect(scss).toMatch(
      /&__btn\s*\{\s*min-width:\s*2rem;\s*height:\s*2rem;\s*padding:\s*0 0\.5rem;\s*position:\s*relative;\s*@include touch-target;/
    );
  });
});
