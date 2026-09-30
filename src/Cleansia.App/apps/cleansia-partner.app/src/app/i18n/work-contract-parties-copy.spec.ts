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
 * The contract for work and the cleaner documents bind the market's operating company, not the
 * customer, and the contract prices the work at the cleaner's reward for their spot.
 */
const COPY: Record<Locale, { company: RegExp; client: RegExp; contractor: RegExp; reward: RegExp }> = {
  en: { company: /operating company/i, client: /client/i, contractor: /contractor/i, reward: /reward/i },
  cs: { company: /provozní společnost/i, client: /objednatel/i, contractor: /zhotovitel/i, reward: /odměn/i },
  sk: { company: /prevádzkov\S* spoločnos/i, client: /objednávateľ/i, contractor: /zhotoviteľ/i, reward: /odmen/i },
  uk: { company: /операційн\S* компан/i, client: /замовник/i, contractor: /підрядник/i, reward: /винагород/i },
  ru: { company: /операционн\S* компан/i, client: /заказчик/i, contractor: /подрядчик/i, reward: /вознагражд/i },
};

interface PartnerCopy {
  pages: {
    profile: Record<string, string>;
    orders: { work_contract: { parties: string; facts: Record<string, string> } };
  };
}

function readLocale(locale: Locale): PartnerCopy {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

describe('the partner contract copy names the operating company', () => {
  it.each(LOCALES)('the %s contract for work is between the company as client and the partner as contractor', (locale) => {
    const parties = readLocale(locale).pages.orders.work_contract.parties;

    expect(parties).toMatch(COPY[locale].company);
    expect(parties).toMatch(COPY[locale].client);
    expect(parties).toMatch(COPY[locale].contractor);
  });

  it.each(LOCALES)('the %s contract for work prices the job at the reward for the spot', (locale) => {
    expect(readLocale(locale).pages.orders.work_contract.facts['reward']).toMatch(COPY[locale].reward);
  });

  it.each(LOCALES)('the %s contract documents are introduced as the work for the operating company', (locale) => {
    expect(readLocale(locale).pages.profile['legal_documents_intro']).toMatch(COPY[locale].company);
  });
});
