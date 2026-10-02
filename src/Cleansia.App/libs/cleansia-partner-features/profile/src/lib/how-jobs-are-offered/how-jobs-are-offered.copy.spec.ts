import { readFileSync } from 'fs';
import { join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(
  __dirname,
  '../../../../../../apps/cleansia-partner.app/src/assets/i18n'
);
const PAGE_TEMPLATE = join(__dirname, 'how-jobs-are-offered.component.html');
const PROFILE_TEMPLATE = join(__dirname, '../profile/profile.component.html');

const REVIEW_CONTACT = 'support@cleansia.cz';

/** Board order, the favourite-cleaner hold, the push radius, the sweeps, no score, approval, review. */
const SECTIONS = [
  'board',
  'preferred',
  'radius',
  'automatic',
  'no_score',
  'approval',
  'review',
] as const;

/** The one automatic step that watches the cleaner: an unstarted job is reported to the administrators. */
const NOT_STARTED_ALERT: Record<(typeof LOCALES)[number], readonly string[]> = {
  en: ['not been started', 'administrators'],
  cs: ['zahájena', 'administrátoři'],
  sk: ['zahájená', 'administrátori'],
  uk: ['не розпочато', 'адміністратори'],
  ru: ['не начат', 'администраторы'],
};

const PAGE_KEYS = [
  'title',
  'description',
  ...SECTIONS.flatMap((section) => [`${section}_title`, `${section}_text`]),
];

type Bundle = Record<string, Record<string, Record<string, unknown>>>;

const bundleFor = (locale: string): Bundle =>
  JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

const pageCopy = (locale: string): Record<string, string> =>
  bundleFor(locale)['pages']['how_jobs_are_offered'] as Record<string, string>;

const profileCopy = (locale: string): Record<string, string> =>
  bundleFor(locale)['pages']['profile'] as Record<string, string>;

describe('how jobs are offered', () => {
  it.each(LOCALES)('%s carries every section of the page, non-empty', (locale) => {
    const page = pageCopy(locale);

    for (const key of PAGE_KEYS) {
      expect(page[key]).toBeTruthy();
    }
  });

  it.each(LOCALES)('%s names the page in the shell and the tab title', (locale) => {
    const bundle = bundleFor(locale);

    expect(bundle['sidebar']['how_jobs_are_offered']).toBeTruthy();
    expect(
      (bundle['page_titles']['partner'] as Record<string, string>)['how_jobs_are_offered']
    ).toBeTruthy();
  });

  it.each(LOCALES)('%s names the human contact for a review', (locale) => {
    expect(pageCopy(locale)['review_text']).toContain(REVIEW_CONTACT);
  });

  it.each(LOCALES)(
    '%s discloses that the administrators are told when a taken job is not started',
    (locale) => {
      const automatic = pageCopy(locale)['automatic_text'];

      for (const phrase of NOT_STARTED_ALERT[locale]) {
        expect(automatic).toContain(phrase);
      }
    }
  );

  it.each(LOCALES)(
    '%s states no policy figure, so the page cannot drift from the rules it describes',
    (locale) => {
      const page = pageCopy(locale);

      for (const key of PAGE_KEYS) {
        expect(page[key].replace(REVIEW_CONTACT, '')).not.toMatch(/\d/);
      }
    }
  );

  it('renders every section, title and text', () => {
    const template = readFileSync(PAGE_TEMPLATE, 'utf8');

    for (const key of PAGE_KEYS) {
      expect(template).toContain(`'pages.how_jobs_are_offered.${key}'`);
    }
  });
});

describe('the weekly job limit on the profile', () => {
  it.each(LOCALES)('%s states the cap and its reason through placeholders', (locale) => {
    const profile = profileCopy(locale);

    expect(profile['weekly_limit']).toBeTruthy();
    expect(profile['weekly_limit_text']).toContain('{{count}}');
    expect(profile['weekly_limit_reason']).toContain('{{reason}}');
  });

  it('fills both placeholders from the profile read', () => {
    const template = readFileSync(PROFILE_TEMPLATE, 'utf8');

    expect(template).toContain(
      "'pages.profile.weekly_limit_text' | translate: { count: facade.weeklyOrderLimit() }"
    );
    expect(template).toContain(
      "'pages.profile.weekly_limit_reason' | translate: { reason: facade.weeklyOrderLimitReason() }"
    );
  });
});
