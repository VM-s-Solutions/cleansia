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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-partner.app/src/assets/i18n');

/**
 * Deleting a cleaner's account files a request and changes nothing until the cooperation is ended in
 * person, so the section describing it may not promise an immediate, irreversible deletion.
 */
const COPY: Record<Locale, { request: RegExp; immediate: RegExp }> = {
  en: { request: /request/i, immediate: /permanently|cannot be undone/i },
  cs: { request: /žádost/i, immediate: /trvale|nelze vrátit/i },
  sk: { request: /žiadosť/i, immediate: /natrvalo|nemožno vrátiť/i },
  uk: { request: /запит/i, immediate: /назавжди|не можна скасувати/i },
  ru: { request: /запрос/i, immediate: /навсегда|нельзя отменить/i },
};

function readLocale(locale: Locale): { pages: { gdpr: Record<string, string> } } {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

describe('the partner GDPR page describes a deletion request', () => {
  it.each(LOCALES)('the %s delete description files a request and promises no instant deletion', (locale) => {
    const description = readLocale(locale).pages.gdpr['delete_description'];

    expect(description).toMatch(COPY[locale].request);
    expect(description).not.toMatch(COPY[locale].immediate);
  });
});
