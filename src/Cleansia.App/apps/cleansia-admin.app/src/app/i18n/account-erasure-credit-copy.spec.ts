import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

/** Both confirmations that run the erasure: erase (and fulfil, which reuses it) and retry. */
const ERASURE_CONFIRMATIONS = [
  'pages.data_protection.erase.confirm_message',
  'pages.data_protection.requests.retry_confirm_message',
];

const EXPIRE_LEDE = 'pages.loyalty_user_detail.credit.expire_dialog.lede';

/**
 * A completed erasure writes off the subject's unused credit without payout, and positive credit no
 * longer refuses it. The admin is told the first, and no longer told the second.
 */
const COPY: Record<Locale, { writtenOff: RegExp; notPaidOut: RegExp; refused: RegExp }> = {
  en: { writtenOff: /written off/i, notPaidOut: /not paid out|no payout/i, refused: /refused/i },
  cs: { writtenOff: /odepíše/i, notPaidOut: /nevyplácí|bez výplaty/i, refused: /odmít/i },
  sk: { writtenOff: /odpíše/i, notPaidOut: /nevypláca|bez výplaty/i, refused: /odmiet/i },
  uk: { writtenOff: /спис/i, notPaidOut: /не виплачуються|без виплати/i, refused: /відхиля/i },
  ru: { writtenOff: /спис/i, notPaidOut: /не выплачиваются|без выплаты/i, refused: /отклоня/i },
};

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<string, unknown>;
}

function resolveKey(tree: Record<string, unknown>, dotted: string): string {
  const value = dotted.split('.').reduce<unknown>((node, segment) => {
    if (node && typeof node === 'object') return (node as Record<string, unknown>)[segment];
    return undefined;
  }, tree);
  return typeof value === 'string' ? value : '';
}

describe('the admin erasure copy states that unused credit is written off', () => {
  it.each(LOCALES)('warns on every erasure confirmation, in %s', (locale) => {
    const bundle = readLocale(locale);
    const { writtenOff, notPaidOut } = COPY[locale];

    for (const key of ERASURE_CONFIRMATIONS) {
      const value = resolveKey(bundle, key);
      expect({ key, writtenOff: writtenOff.test(value), notPaidOut: notPaidOut.test(value) }).toEqual({
        key,
        writtenOff: true,
        notPaidOut: true,
      });
    }
  });

  it.each(LOCALES)('no longer says outstanding credit refuses an erasure, in %s', (locale) => {
    const value = resolveKey(readLocale(locale), EXPIRE_LEDE);

    expect(value.trim().length).toBeGreaterThan(0);
    expect({ locale, refused: COPY[locale].refused.test(value) }).toEqual({ locale, refused: false });
  });
});
