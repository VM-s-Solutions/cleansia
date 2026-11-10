import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { EmailType, LoyaltyEarnSource } from '@cleansia/admin-services';
import { toSnakeCase } from '@cleansia/utils';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    dir = dirname(dir);
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

function lookup(locale: string, key: string): unknown {
  const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
  return key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], bundle);
}

const emailTypeKeys = Object.values(EmailType)
  .filter((v): v is string => typeof v === 'string')
  .map((name) => `enums.email_type.${toSnakeCase(name)}`);

const loyaltySourceKeys = Object.values(LoyaltyEarnSource)
  .filter((v): v is string => typeof v === 'string')
  .map((name) => `pages.loyalty_user_detail.activity.source.${name}`);

const REQUIRED = [
  ...emailTypeKeys,
  ...loyaltySourceKeys,
  'page_titles.admin.pay_configs',
  'page_titles.admin.pay_config_create',
  'page_titles.admin.pay_config_edit',
  'pages.loyalty_tiers.no_tiers',
  'pages.data_protection.requests.actor.self',
  'pages.data_protection.requests.actor.system',
];

describe('admin raw-code copy', () => {
  it('covers all ten e-mail types', () => expect(emailTypeKeys).toHaveLength(10));

  for (const locale of LOCALES) {
    it(`${locale} carries every key`, () => {
      const missing = REQUIRED.filter((key) => typeof lookup(locale, key) !== 'string' || lookup(locale, key) === '');
      expect(missing).toEqual([]);
    });

    it(`${locale} no longer carries the unused global.languages block`, () => {
      expect(lookup(locale, 'global.languages')).toBeUndefined();
    });
  }
});
