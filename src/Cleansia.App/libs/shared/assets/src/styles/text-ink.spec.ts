import { join } from 'path';
import { compile } from 'sass';

// A text link or a text button is blue TEXT on a light ground, so it takes the text ink, Sky700
// (5.9 on white), never the brand Sky600 (4.1) or Sky500 (2.8); under the pointer it goes a step
// darker, Sky800 (7.6). Fills, borders and standalone icons keep the brand blue.
describe('the shared and partner stylesheets — text links and text buttons', () => {
  const compiled = (entry: string): string =>
    compile(join(__dirname, entry), {
      quietDeps: true,
      loadPaths: [join(__dirname, '../../../../../node_modules')],
    }).css;
  const partner = compiled('cleansia-partner.scss');
  const customer = compiled('cleansia-customer.scss');
  const admin = compiled('cleansia-admin.scss');

  /** The colour every rule for exactly this selector declares. */
  const inks = (css: string, selector: string): string[] => {
    const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    const rules = css.matchAll(new RegExp(`(?:^[ \\t]*|,\\s*)${escaped}\\s*(?:,[^{]*)?\\{([^}]*)\\}`, 'gm'));
    return Array.from(rules, ([, body]) => body.match(/(?:^|;)\s*color:\s*([^;]+)/)?.[1].trim() ?? '')
      .filter(Boolean);
  };

  it.each([
    ['the sign-in card links (partner and admin)', '.cleansia-login a'],
    ['the partner register page links', '.cleansia-register a'],
    ['the partner forgot-password page links', '.cleansia-forgot-password a'],
    ['the forgot-password "sign in" link', '.cleansia-forgot-password__link a'],
    ['the detail breadcrumb link', '.cleansia-order-details__breadcrumb-link'],
    ['the partner profile consent link', '.cleansia-profile__consent-text a'],
    ['the cookie notice link', '.cleansia-cookie-consent__link'],
    ['the help card "show help" button', '.cleansia-help-card__restore-btn'],
  ])('inks %s with Sky700, and Sky800 under the pointer', (_, selector) => {
    expect(inks(partner, selector)).toEqual(['var(--cleansia-primary-700)']);
    expect(inks(partner, `${selector}:hover`)).toEqual(['var(--cleansia-primary-800)']);
  });

  // Blue text that is read, not pressed, takes the same ink: the order's package price sat on the
  // brand Sky600 (4.1) beside a Sky700 name.
  it("inks the partner order's package price with Sky700", () => {
    expect(inks(partner, '.cleansia-order-details__package-header .package-price')).toEqual([
      'var(--cleansia-primary-700)',
    ]);
  });

  // The referral code dialog's "checking…" line sat on the brand Sky600 (4.1 on white, less on its
  // tint; 3.4 after dark) and the admin's optional-price badge on Sky500 (2.8).
  it('inks the code dialog\'s "checking" line with Sky700, and Sky300 after dark', () => {
    expect(inks(customer, '.cleansia-code-input-dialog__status--neutral')).toEqual([
      'var(--cleansia-primary-700)',
    ]);
    expect(inks(customer, ':root.dark-mode .cleansia-code-input-dialog__status--neutral')).toEqual([
      '#7dd3fc',
    ]);
  });

  // The applied and invalid lines sat on green-600 and red-600: 2.9 and 4.1 on their tints, 3.8 and
  // 2.9 after dark. Green-700 is 4.4 on its tint, so the applied line takes green-800.
  it("inks the code dialog's applied and invalid lines to read on their tints, after dark too", () => {
    expect(inks(customer, '.cleansia-code-input-dialog__status--success')).toEqual([
      'var(--cleansia-success-800)',
    ]);
    expect(inks(customer, '.cleansia-code-input-dialog__status--error')).toEqual(['var(--cleansia-error-700)']);
    expect(inks(customer, ':root.dark-mode .cleansia-code-input-dialog__status--success')).toEqual(['#86efac']);
    expect(inks(customer, ':root.dark-mode .cleansia-code-input-dialog__status--error')).toEqual(['#fca5a5']);
  });

  // The partner dashboard's green figures were emerald-500 #10b981: 2.1 on the order summary's blue
  // wash, 2.4 on the earnings summary. Green-700 is 4.1 on the wash; green-800 reads 5.8.
  it.each([
    ['a success label (the completion rate, a positive growth)', '.cleansia-label--success'],
    ["the order chart's success value", '.summary-item__value--success'],
    ["the earnings chart's positive value", '.summary-item__value--positive'],
  ])('inks %s with green-800', (_, selector) => {
    expect(inks(partner, selector)).toEqual(['var(--cleansia-success-800)']);
  });

  it("inks the admin price form's optional badge with Sky700", () => {
    expect(inks(admin, '.currency-price-block__badge--optional')).toEqual(['var(--cleansia-primary-700)']);
  });

  it("gives a filter chip's remove button the chip's own ink", () => {
    expect(inks(partner, '.cleansia-filter-chips__chip')).toEqual(['var(--cleansia-primary-700)']);
    expect(inks(partner, '.cleansia-filter-chips__remove')).toEqual(['var(--cleansia-primary-700)']);
  });

  it('inks a sortable column header and its arrow with Sky700 under the pointer', () => {
    expect(inks(partner, '.cleansia-table .table__head tr th.sortable:hover')).toEqual([
      'var(--cleansia-primary-700)',
    ]);
    expect(inks(partner, '.cleansia-table .table__head tr th.sortable:hover .sort-icon')).toEqual([
      'var(--cleansia-primary-700)',
    ]);
  });

  // PrimeNG has no hover ink for these two variants: the preset inks them Sky700 and the hovered
  // button carries Sky800 itself. Light only — after dark the preset's light blue stays.
  it('takes text and outlined buttons to Sky800 under the pointer, in the light theme only', () => {
    for (const css of [partner, customer]) {
      expect(css).toMatch(
        /:root:not\(\.dark-mode\) \.p-button:is\(\.p-button-text, \.p-button-outlined\):not\(:disabled\):is\(:hover, :active\) \{\s*--p-button-text-primary-color: var\(--p-primary-800\);\s*--p-button-outlined-primary-color: var\(--p-primary-800\);/,
      );
    }
  });

  // The rejected row is pink, where the text ink reads 4.4; the raised button gets a white face.
  it("puts the registration lock's contact button on a face of its own", () => {
    expect(partner).toMatch(/\.cleansia-registration-lock__category-action \.p-button \{\s*background: var\(--cleansia-white\);/);
  });

  // The schedule form's add-address link went to the accent, Sky600, under the pointer: lighter
  // than its Sky700 rest, 4.1. After dark the rest is Sky300 and the accent, Sky400, is the step.
  it("takes the schedule form's add-address link a step darker under the pointer", () => {
    expect(inks(customer, '.cl-rec__add-address')).toEqual(['var(--cl-heading)']);
    expect(inks(customer, '.cl-rec__add-address:hover')).toEqual(['#075985']);
    expect(inks(customer, ':root.dark-mode .cl-rec__add-address:hover')).toEqual(['var(--cl-accent)']);
  });

  // The notice's OK is a filled button: white on the brand blue, Sky600 at its lightest (it started
  // at Sky500, 2.77), a step darker under the pointer. Light theme; after dark it keeps its own.
  it("paints the cookie notice's OK button from Sky600, a step darker under the pointer", () => {
    const background = (css: string, selector: string): string[] => {
      const escaped = selector.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
      return Array.from(
        css.matchAll(new RegExp(`(?:^|\\n)${escaped}\\s*\\{([^}]*)\\}`, 'g')),
        ([, body]) => body.match(/background:\s*([^;]+)/)?.[1].trim() ?? ''
      ).filter(Boolean);
    };
    for (const css of [partner, customer]) {
      expect(background(css, '.cleansia-cookie-consent__btn--accept')).toEqual([
        'linear-gradient(135deg, var(--cleansia-primary-600) 0%, var(--cleansia-primary-700) 100%)',
      ]);
      expect(background(css, '.cleansia-cookie-consent__btn--accept:hover')).toEqual([
        'linear-gradient(135deg, var(--cleansia-primary-700) 0%, var(--cleansia-primary-800) 100%)',
      ]);
    }
  });

  it('keeps the cookie notice link a light blue after dark', () => {
    expect(inks(customer, ':root.dark-mode .cleansia-cookie-consent__link')).toEqual(['#7dd3fc']);
    expect(inks(customer, ':root.dark-mode .cleansia-cookie-consent__link:hover')).toEqual(['#bae6fd']);
  });
});
