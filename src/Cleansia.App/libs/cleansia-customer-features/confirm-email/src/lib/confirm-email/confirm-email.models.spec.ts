import { readFileSync } from 'fs';
import { join } from 'path';
import { checkIfPasswordsValid } from './confirm-email.models';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(__dirname, '../../../../../../apps/cleansia.app/src/assets/i18n');
const TEMPLATE = join(__dirname, 'confirm-email.component.html');

function resolveKey(bundle: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
}

describe('checkIfPasswordsValid (customer confirm-email)', () => {
  it('accepts the shortest password the server takes', () => {
    expect(checkIfPasswordsValid('Passw0rd')).toEqual({
      hasLetter: true,
      hasNumber: true,
      hasMinLength: true,
      arePasswordsEqual: false,
    });
  });

  it('wants eight characters, a letter and a digit, and nothing else', () => {
    expect(checkIfPasswordsValid('Passw0r').hasMinLength).toBe(false);
    expect(checkIfPasswordsValid('Passwords').hasNumber).toBe(false);
    expect(checkIfPasswordsValid('12345678').hasLetter).toBe(false);
    expect(checkIfPasswordsValid('passw0rd').hasLetter).toBe(true);
  });

  it('counts a letter and a digit of any script, as the server does', () => {
    expect(checkIfPasswordsValid('Пароль2024').hasLetter).toBe(true);
    expect(checkIfPasswordsValid('Heslo١٢٣').hasNumber).toBe(true);
  });

  it('compares the second password against the first', () => {
    expect(checkIfPasswordsValid('Passw0rd', 'Passw0rd').arePasswordsEqual).toBe(true);
    expect(checkIfPasswordsValid('Passw0rd', 'Passw0rx').arePasswordsEqual).toBe(false);
  });
});

describe('confirm-email template translation keys (customer)', () => {
  const referenced = Array.from(
    new Set(
      Array.from(readFileSync(TEMPLATE, 'utf8').matchAll(/'([a-zA-Z0-9_.]+)'\s*\|\s*translate/g)).map(
        (match) => match[1]
      )
    )
  );

  it('asks for the password with its rules', () => {
    expect(referenced).toEqual(
      expect.arrayContaining([
        'pages.email_confirm.password_lead',
        'auth.register.password',
        'auth.register.confirm_password',
        'auth.register.validation.password_min_length',
        'auth.register.validation.password_letter',
        'auth.register.validation.password_number',
        'auth.register.validation.passwords_match',
      ])
    );
  });

  it.each(LOCALES)('resolves every key the screen and its facade use in %s.json', (locale) => {
    const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

    const missing = [...referenced, 'pages.email_confirm.password_invalid'].filter(
      (key) => typeof resolveKey(bundle, key) !== 'string'
    );

    expect(missing).toEqual([]);
  });
});
