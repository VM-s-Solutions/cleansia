import { readFileSync } from 'fs';
import { join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(
  __dirname,
  '../../../../../../apps/cleansia.app/src/assets/i18n'
);

const KEYS = [
  'title',
  'explainer',
  'placeholder',
  'loading',
  'unavailable',
  'section_title',
  'awaiting_title',
  'awaiting_body',
  'accepted_title',
  'closed_title',
  'closed_body',
  'choose_another',
  'choose_another_hint',
  'choose_submit',
  'choose_success',
  'list_error',
  'list_empty',
] as const;

/**
 * A reservation is a real, disclosed offer and nothing more: the cleaner is asked, and the job
 * releases to everyone on a decline or on silence. No locale may tell the customer their cleaner
 * IS coming, so no locale may use the word for it.
 */
const ASSIGNMENT_WORDS: Record<string, readonly string[]> = {
  en: ['assign'],
  cs: ['přiřaz', 'prirazen'],
  sk: ['priraden', 'pridelen'],
  uk: ['признач'],
  ru: ['назнач'],
};

/**
 * The closure covers a decline and a silent lapse with one sentence, because the customer is never
 * told which. Stems for both "refused" and "did not answer", per locale — the same lists the iOS
 * push catalog pins the same event's copy against.
 */
const OUTCOME_WORDS: Record<string, readonly string[]> = {
  en: ['declin', 'refus', 'reject', 'turned down', 'answer', 'respond', 'repl', 'ignor'],
  cs: ['odmít', 'odmit', 'odpověd', 'neozval', 'reagoval'],
  sk: ['odmiet', 'odmietn', 'odpoved', 'neozval', 'reagoval'],
  uk: ['відмов', 'відповід', 'відповів', 'зреагував'],
  ru: ['отказ', 'отклон', 'ответ', 'отклик', 'отреагиров'],
};

/**
 * Nothing in a pull board can keep a statement about when a cleaner will be on the job, so no
 * surface may make one (ADR-0045 D4.2). `awaiting_body` carries an instant, and that instant is the
 * end of the reservation — not a time-to-assignment.
 */
const DURATION_PROMISE = /\b(within|do|за|через|behem|během)\s+\d+\s*(hour|hod|min|час|хвил|годин)/i;

/**
 * The closure headline stands under `section_title` — "Your favourite cleaner" — so a form that
 * agrees with a PERSON reads as a statement about the cleaner rather than about the booking. ru
 * shipped the masculine short adjective «Открыт», which agrees with «заказ», a noun the headline
 * does not name; the other four are impersonal.
 */
// `\b` is defined over ASCII word characters only, so it never matches after `é` or a Cyrillic
// letter — the lookahead is what makes the boundary real in four of these five.
const IMPERSONAL_CLOSURE: Record<string, RegExp> = {
  en: /^Open(?=\s|$)/,
  cs: /^Otevřeno(?=\s|$)/,
  sk: /^Otvorené(?=\s|$)/,
  uk: /^Відкрито(?=\s|$)/,
  ru: /^Открыто(?=\s|$)/,
};

function copyFor(locale: string): Record<string, string> {
  const bundle = JSON.parse(
    readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')
  ) as { preferred_cleaner?: Record<string, string> };

  if (!bundle.preferred_cleaner) {
    throw new Error(`preferred_cleaner missing from ${locale}.json`);
  }

  return bundle.preferred_cleaner;
}

describe('preferred-cleaner copy', () => {
  describe.each(LOCALES)('%s', (locale) => {
    const copy = copyFor(locale);

    it('ships every key', () => {
      for (const key of KEYS) {
        expect(copy[key]).toBeTruthy();
      }
    });

    it('never tells the customer a cleaner is assigned to them', () => {
      for (const key of KEYS) {
        for (const word of ASSIGNMENT_WORDS[locale]) {
          expect(copy[key].toLowerCase()).not.toContain(word);
        }
      }
    });

    it('names neither outcome when the offer closes', () => {
      for (const key of ['closed_title', 'closed_body'] as const) {
        for (const word of OUTCOME_WORDS[locale]) {
          expect(copy[key].toLowerCase()).not.toContain(word);
        }
      }
    });

    it('states no time-to-assignment anywhere', () => {
      for (const key of KEYS) {
        expect(copy[key]).not.toMatch(DURATION_PROMISE);
      }
    });

    it('holds the pending state open — it names the person asked, and the deadline of the ask', () => {
      expect(copy['awaiting_title']).toContain('{{name}}');
      expect(copy['awaiting_body']).toContain('{{time}}');
      expect(copy['awaiting_body']).not.toContain('{{name}}');
    });

    it('names nobody in the closure', () => {
      expect(copy['closed_title']).not.toContain('{{');
      expect(copy['closed_body']).not.toContain('{{');
    });

    it('opens the closure headline impersonally, so it reads about the booking', () => {
      expect(copy['closed_title']).toMatch(IMPERSONAL_CLOSURE[locale]);
    });
  });
});
