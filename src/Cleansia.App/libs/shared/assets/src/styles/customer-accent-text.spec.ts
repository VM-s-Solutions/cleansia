import { join } from 'path';
import { compile } from 'sass';

// The brand primary, Sky600 #0284c7, measures 4.1 on white against the 4.5 text needs, and the
// fixed SCSS value stays 4.2 on the dark card. Blue TEXT takes --cl-accent-text instead: Sky700 in
// light mode (5.9), Sky300 after dark (10.4). Fills, borders, icons and buttons keep the primary.
describe('the customer stylesheet — blue text', () => {
  const css = compile(join(__dirname, 'cleansia-customer.scss'), {
    quietDeps: true,
    loadPaths: [join(__dirname, '../../../../../node_modules')],
  }).css;

  /**
   * The colour each rule for exactly this selector declares. A rule for it inside another selector
   * (a featured card, the dark theme, the reader's own tier) is a variant with its own ground.
   */
  const inks = (selector: string): string[] => {
    const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const rules = css.matchAll(new RegExp(`(?:^[ \\t]*|,\\s*)${escaped}\\s*(?:,[^{]*)?\\{([^}]*)\\}`, 'gm'));
    return Array.from(rules, ([, body]) => body.match(/(?:^|;)\s*color:\s*([^;]+)/)?.[1].trim() ?? '')
      .filter(Boolean);
  };

  it.each([
    ['the booking package card price', '.cl-wiz__pack-price'],
    ['the booking summary total', '.cl-wiz__total'],
    ['the home services card "from" price', '.cl-services__price'],
    ['the home quick quote amount', '.cl-quote__price strong'],
    ['the home quick quote amount, on its own', '.cl-quote__amount'],
    ['the services catalogue package price', '.cl-cat-pkg__price'],
    ['the Plus page plan price', '.cl-plusp__price'],
    ['the order detail total', '.order-detail__total-amount'],
    ['the schedule form price', '.cl-rec__summary-price'],
    ['a rewards tier discount', '.cl-rwd__tier-discount'],
    ['the track page total', '.cl-trk__total'],
    ['a legal heading number', '.cl-lgl__content h2 .cl-lgl__num'],
    ['the navbar active link', '.customer-navbar__link--active'],
    ['the navbar user role', '.customer-navbar__user-role'],
    ['the navbar sign-in link on hover', '.customer-navbar__signin:hover'],
  ])('inks %s with the text-safe blue', (_, selector) => {
    const declared = inks(selector);

    expect(declared.length).toBeGreaterThan(0);
    expect(declared.every((ink) => ink.startsWith('var(--cl-accent-text'))).toBe(true);
  });

  it('defines the text-safe blue for both themes', () => {
    expect(css).toMatch(/:root \{[^}]*--cl-accent-text: #0369a1;/);
    expect(css).toMatch(/:root\.dark-mode \{[^}]*--cl-accent-text: #7dd3fc;/);
  });
});
