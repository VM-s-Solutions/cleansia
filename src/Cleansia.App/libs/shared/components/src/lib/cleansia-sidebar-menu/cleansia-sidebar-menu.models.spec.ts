import { readFileSync } from 'fs';
import { join } from 'path';
import {
  isMobileViewport,
  MOBILE_VIEWPORT_MAX_PX,
} from './cleansia-sidebar-menu.models';

describe('isMobileViewport', () => {
  it.each([
    [767, true],
    [768, true],
    [769, false],
  ])('at %i px the shell is mobile: %s', (width, mobile) => {
    expect(isMobileViewport(width)).toBe(mobile);
  });

  /**
   * The stylesheet and the script decide the same question independently, and the day they
   * disagreed by one pixel the signed-in shell rendered neither the rail nor the toolbar at that
   * width. The stylesheet lives in another lib, so it is declared an input of this project's test
   * target — a spec that reads a file Nx cannot see replays green from cache over changed bytes.
   */
  it('matches every viewport media query in the sidebar stylesheet', () => {
    const scss = readFileSync(
      join(
        __dirname,
        '../../../../assets/src/styles/components/cleansia-sidebar-menu.component.scss'
      ),
      'utf-8'
    );
    const queries = [...scss.matchAll(/@media\s*\(max-width:\s*(\d+)px\)/g)].map(
      (m) => Number(m[1])
    );

    expect(queries.length).toBeGreaterThan(0);
    expect(new Set(queries)).toEqual(new Set([MOBILE_VIEWPORT_MAX_PX]));
  });
});
