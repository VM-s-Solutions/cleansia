import { readFileSync } from 'fs';
import { join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(
  __dirname,
  '../../../../../../apps/cleansia-partner.app/src/assets/i18n'
);
const TEMPLATE = join(
  __dirname,
  '../components/profile-job-radius/profile-job-radius.component.html'
);

const SECTION_KEYS = [
  'job_radius',
  'job_radius_explainer',
  'job_radius_limit',
  'job_radius_off_hint',
  'job_radius_distance',
  'job_radius_distance_hint',
] as const;

const MESSAGE_KEYS = ['job_radius_saved', 'job_radius_load_error'] as const;

/**
 * The sentence each locale uses to say the board is unchanged. The radius narrows the digest and
 * nothing else, so a translation that drops this promise is the defect — and a list that only
 * covered the four translations would pass while the English source drifted.
 */
const BOARD_UNCHANGED_STEMS: Record<string, { explainer: string; off: string }> =
  {
    en: { explainer: 'still lists every job', off: 'every new job' },
    cs: { explainer: 'všechny zakázky', off: 'každou novou zakázku' },
    sk: { explainer: 'všetky zákazky', off: 'každú novú zákazku' },
    uk: { explainer: 'всі замовлення', off: 'кожне нове замовлення' },
    ru: { explainer: 'все заказы', off: 'каждом новом заказе' },
  };

const bundleFor = (locale: string): Record<string, never> =>
  JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

const sectionCopy = (locale: string): Record<string, string> =>
  (bundleFor(locale)['pages'] as Record<string, Record<string, string>>)[
    'profile'
  ];

const messageCopy = (locale: string): Record<string, string> =>
  (
    (bundleFor(locale)['global'] as Record<string, never>)['messages'] as Record<
      string,
      Record<string, string>
    >
  )['profile'];

describe('job radius copy', () => {
  it.each(LOCALES)('%s carries every key, non-empty', (locale) => {
    const section = sectionCopy(locale);
    const messages = messageCopy(locale);

    for (const key of SECTION_KEYS) {
      expect(section[key]).toBeTruthy();
    }
    for (const key of MESSAGE_KEYS) {
      expect(messages[key]).toBeTruthy();
    }
  });

  it.each(LOCALES)(
    '%s promises the board is unchanged, because the radius narrows only the digest',
    (locale) => {
      const section = sectionCopy(locale);
      const stems = BOARD_UNCHANGED_STEMS[locale];

      expect(section['job_radius_explainer'].toLowerCase()).toContain(
        stems.explainer.toLowerCase()
      );
      expect(section['job_radius_off_hint'].toLowerCase()).toContain(
        stems.off.toLowerCase()
      );
    }
  );

  it.each(LOCALES)(
    '%s states the bounds through placeholders, never as literal numbers',
    (locale) => {
      const section = sectionCopy(locale);

      expect(section['job_radius_distance_hint']).toContain('{{min}}');
      expect(section['job_radius_distance_hint']).toContain('{{max}}');

      for (const key of SECTION_KEYS) {
        const withoutPlaceholders = section[key].replace(/\{\{\s*\w+\s*\}\}/g, '');
        expect(withoutPlaceholders).not.toMatch(/\d/);
      }
    }
  );

  it('renders the explainer on the section itself, not only in the bundle', () => {
    const template = readFileSync(TEMPLATE, 'utf8');

    expect(template).toContain('pages.profile.job_radius_explainer');
    expect(template).toContain('pages.profile.job_radius_off_hint');
  });

  it('offers no distance box while the limit is off, so "no limit" is a choice and not an end-stop', () => {
    const template = readFileSync(TEMPLATE, 'utf8');

    expect(template).toContain('@if (facade.limitEnabled())');
    expect(template).toContain('formControlName="limitEnabled"');
  });
});
