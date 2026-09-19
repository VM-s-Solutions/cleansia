import { join } from 'path';
import { compile } from 'sass';

/**
 * A control a thumb has to hit needs a 44px box. jsdom lays nothing out, so the floors are read
 * from the compiled stylesheets, which is what the browser receives; each file is declared an
 * input of this project's test target so the pin cannot replay from cache.
 */
const STYLES_DIR = join(__dirname, '../../../../assets/src/styles/components');

function compiled(stylesheet: string): string {
  return compile(join(STYLES_DIR, stylesheet), { quietDeps: true }).css;
}

function block(css: string, selector: string): string {
  const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = css.match(new RegExp(`${escaped}\\s*\\{([^}]*)\\}`));
  if (!match) {
    throw new Error(`no rule for ${selector}`);
  }
  return match[1];
}

describe('touch-target floors in the shared stylesheets', () => {
  it('gives the default button size a 44px box at any root font size', () => {
    const css = compiled('cleansia-button.component.scss');

    expect(block(css, '.cleansia-button--medium button')).toMatch(/min-height:\s*max\(2\.75rem,\s*44px\)/);
    expect(block(css, '.cleansia-button--medium button.p-button-icon-only')).toMatch(
      /width:\s*max\(2\.75rem,\s*44px\)[^}]*height:\s*max\(2\.75rem,\s*44px\)/
    );
  });

  it('keeps the icon-only default button on the floor below 768px, where it shrinks', () => {
    const css = compiled('cleansia-button.component.scss');
    const narrow = css.slice(css.indexOf('@media (max-width: 768px)'));

    expect(block(narrow, '.cleansia-button--medium button.p-button-icon-only')).toMatch(
      /width:\s*max\(2\.5rem,\s*44px\)[^}]*height:\s*max\(2\.5rem,\s*44px\)/
    );
  });

  it('gives the shared text field and its password eye a 44px box', () => {
    const css = compiled('cleansia-text-input.component.scss');

    expect(block(css, 'cleansia-text-input input.p-inputtext')).toMatch(/min-height:\s*44px/);
    expect(block(css, 'button.cleansia-text-input__eye')).toMatch(/width:\s*44px;[^}]*height:\s*44px/);
  });
});
