import { readFileSync } from 'fs';
import { join } from 'path';
import { checkIfPasswordsValid } from './forgot-password.models';

const PARTNER_LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(
  __dirname,
  '../../../../../../apps/cleansia-partner.app/src/assets/i18n'
);
const TEMPLATE = join(__dirname, 'forgot-password.component.html');

function resolveKey(bundle: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object'
          ? (node as Record<string, unknown>)[segment]
          : undefined,
      bundle
    );
}

describe('checkIfPasswordsValid (partner forgot-password)', () => {
  // The backend rule is ValidationExtensions.ValidatePassword: >= 8 chars, at
  // least one letter and one digit. The checklist must advertise exactly that.
  it('accepts the shortest backend-valid password', () => {
    const result = checkIfPasswordsValid('Passw0rd');

    expect(result.hasLetter).toBe(true);
    expect(result.hasNumber).toBe(true);
    expect(result.hasMinLength).toBe(true);
  });

  it('rejects seven characters', () => {
    expect(checkIfPasswordsValid('Passw0r').hasMinLength).toBe(false);
  });

  it('requires a digit', () => {
    expect(checkIfPasswordsValid('Passwords').hasNumber).toBe(false);
  });

  it('requires a letter', () => {
    expect(checkIfPasswordsValid('12345678').hasLetter).toBe(false);
  });

  it('does not demand case mixing or a special character', () => {
    const result = checkIfPasswordsValid('passw0rd');

    expect(result.hasLetter).toBe(true);
    expect(result.hasNumber).toBe(true);
    expect(result.hasMinLength).toBe(true);
    expect(Object.keys(result).sort()).toEqual([
      'arePasswordsEqual',
      'hasLetter',
      'hasMinLength',
      'hasNumber',
    ]);
  });

  it('compares the confirmation against the original', () => {
    expect(checkIfPasswordsValid('Passw0rd', 'Passw0rd').arePasswordsEqual).toBe(
      true
    );
    expect(checkIfPasswordsValid('Passw0rd', 'Passw0rx').arePasswordsEqual).toBe(
      false
    );
  });
});

describe('forgot-password template translation keys', () => {
  // Nothing in the build fails on a missing translation — ngx-translate renders
  // the key path verbatim. This screen shipped three keys that existed only in
  // the customer app's bundles, so the partner saw "auth.register.validation.
  // password_lowercase" printed on screen. Guard every key, in every locale.
  const referenced = Array.from(
    new Set(
      Array.from(
        readFileSync(TEMPLATE, 'utf8').matchAll(
          /'([a-zA-Z0-9_.]+)'\s*\|\s*translate/g
        )
      ).map((match) => match[1])
    )
  );

  it('finds keys to check', () => {
    expect(referenced.length).toBeGreaterThan(0);
  });

  it.each(PARTNER_LOCALES)('resolves every key in %s.json', (locale) => {
    const bundle = JSON.parse(
      readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')
    );

    const missing = referenced.filter(
      (key) => typeof resolveKey(bundle, key) !== 'string'
    );

    expect(missing).toEqual([]);
  });
});
