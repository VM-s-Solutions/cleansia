#!/usr/bin/env node
/**
 * The cross-stack booking-policy parity check.
 *
 * `BookingPolicy` decides what a cancellation costs, how late a booking is accepted and what an
 * express slot adds. Four surfaces then STATE those numbers to a customer — the web copy, the web's
 * shared constants, Android's string resources and iOS's string catalog — and every one of them
 * holds its own literal. Nothing compiled them together, so nothing noticed when they stopped
 * agreeing. `LegalDocumentVersions` is pinned the same way (§4): the version the server stamps on a
 * consent is the one the legal pages show, in every locale.
 *
 * They did stop agreeing. Both mobile apps told customers a cancellation cost "50% charge" between
 * 4 and 24 hours and "100% charge" under 4, against a policy of 25% and 50%: double the real fee, in
 * five locales, on two platforms, in the direction that talks a customer out of booking. The home
 * page separately claimed the earliest bookable slot was 4 hours when the floor is 2 — and said so
 * next to the express surcharge that exists precisely for the 2–4 hour band it denied.
 *
 * WHY A PLAIN NODE SCRIPT, NOT A TEST — the same reasoning `check-available-status-parity.mjs`
 * records for the offerability rule, and it applies here unchanged:
 *   - `frontend-ci.yml` runs `nx affected`; a C#/Kotlin/Swift-only diff selects ZERO Nx projects, so
 *     a Jest spec would not run. Nx inputs also cannot reference paths above `src/Cleansia.App`, so
 *     `BookingPolicy.cs` is not a declared input and a spec would replay a CACHED PASS over drift.
 *   - `backend-ci.yml` excludes `src/cleansia_android/**` and `src/cleansia_ios/**`.
 *   - `android-ci` and `ios-ci` are Gradle and Xcode; neither reads C#.
 * No single existing job can see all four trees. This one is dependency-free, runs on
 * ubuntu-latest in seconds, and has its own repo-root workflow triggering on every tree it reads.
 *
 * WHAT IT DELIBERATELY DOES NOT DO. It does not serve these numbers from the API. They change at
 * release cadence, a client that cannot reach the network still has to state them, and the copy
 * would still need a placeholder per locale — the drift would simply move into the interpolation.
 * Freezing the comparison is cheaper than plumbing the value, and it fails LOUDLY at the moment the
 * two disagree, which is the only property that actually mattered here.
 */
import { readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * `--root=<dir>` points the tool at a fixture tree instead of the repository, which is how the
 * self-test exercises it without ever touching the working tree.
 */
const rootArg = process.argv.find((a) => a.startsWith('--root='));
const REPO = rootArg
  ? rootArg.slice('--root='.length)
  : join(dirname(fileURLToPath(import.meta.url)), '..', '..');

const POLICY_CS = 'src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs';
const LEGAL_VERSIONS_CS = 'src/Cleansia.Core.Domain/Legal/LegalDocumentVersions.cs';
const WEB_MODEL = 'src/Cleansia.App/libs/shared/models/src/lib/models/booking-window.models.ts';
const WEB_I18N = 'src/Cleansia.App/apps/cleansia.app/src/assets/i18n';
const ANDROID_RES = 'src/cleansia_android/customer-app/src/main/res';
const IOS_CATALOG = 'src/cleansia_ios/CleansiaCustomer/Resources/Localizable.xcstrings';

const LOCALES = ['en', 'cs', 'sk', 'ru', 'uk'];
/** res/values is the default (en); the rest carry a locale suffix. */
const ANDROID_DIRS = { en: 'values', cs: 'values-cs', sk: 'values-sk', ru: 'values-ru', uk: 'values-uk' };

const findings = [];
const note = (where, what) => findings.push(`${where} — ${what}`);

const read = (rel) => readFileSync(join(REPO, rel), 'utf8');

/** `public const decimal X = 0.25m;` / `public const int X = 24;` → number. */
export function readCsConst(source, name) {
  const match = new RegExp(
    `public\\s+const\\s+\\w+\\s+${name}\\s*=\\s*(-?[0-9]+(?:\\.[0-9]+)?)[mMdDfF]?\\s*;`,
  ).exec(source);
  return match ? Number(match[1]) : null;
}

/** `public const string X = "2026-09-draft";` → string. */
export function readCsStringConst(source, name) {
  const match = new RegExp(`public\\s+const\\s+string\\s+${name}\\s*=\\s*"([^"]*)"\\s*;`).exec(source);
  return match ? match[1] : null;
}

/** `export const X = 0.2;` → number. */
export function readTsConst(source, name) {
  const match = new RegExp(
    `export\\s+const\\s+${name}\\s*=\\s*(-?[0-9]+(?:\\.[0-9]+)?)\\s*;`,
  ).exec(source);
  return match ? Number(match[1]) : null;
}

/**
 * Every percentage a string states, as numbers.
 *
 * Locale-blind on purpose: "25% charge", "25 % poplatok" and "25 % штраф" all reduce to [25], and
 * the hours in "Between 4 and 24 hours it is 25%, under 4 hours 50%" are not followed by a percent
 * sign so they never enter the set. Comparing the SET rather than a position also means a
 * translator may reorder the sentence without tripping the check.
 */
export function percentagesIn(text) {
  return [...String(text).matchAll(/(\d+(?:[.,]\d+)?)\s*%/g)].map((m) =>
    Number(m[1].replace(',', '.')),
  );
}

/** Every bare integer in a sentence, for copy that quotes an AMOUNT rather than a percentage. */
export function amountsIn(text) {
  return [...String(text).matchAll(/(\d+)/g)].map((m) => Number(m[1]));
}

/** The first integer in a short claim like "in 2 hours" / "za 2 hodiny" / "через 2 часа". */
export function firstNumberIn(text) {
  const match = /(\d+)/.exec(String(text));
  return match ? Number(match[1]) : null;
}

function androidString(dir, key) {
  const path = join(REPO, ANDROID_RES, dir, 'strings.xml');
  if (!existsSync(path)) return null;
  const match = new RegExp(`<string name="${key}">(.*?)</string>`, 's').exec(
    readFileSync(path, 'utf8'),
  );
  return match ? match[1] : null;
}

function iosString(catalog, key, locale) {
  return catalog.strings?.[key]?.localizations?.[locale]?.stringUnit?.value ?? null;
}

// ─── The authority ──────────────────────────────────────────────────────────
const policySource = read(POLICY_CS);
const policy = {
  FreeCancellationHours: readCsConst(policySource, 'FreeCancellationHours'),
  PartialCancellationHours: readCsConst(policySource, 'PartialCancellationHours'),
  PartialCancellationFeeRate: readCsConst(policySource, 'PartialCancellationFeeRate'),
  LastMinuteCancellationFeeRate: readCsConst(policySource, 'LastMinuteCancellationFeeRate'),
  ExpressLeadTimeHours: readCsConst(policySource, 'ExpressLeadTimeHours'),
  StandardLeadTimeHours: readCsConst(policySource, 'StandardLeadTimeHours'),
  ExpressSurchargeRate: readCsConst(policySource, 'ExpressSurchargeRate'),
  FirstWindowHour: readCsConst(policySource, 'FirstWindowHour'),
  LastWindowHour: readCsConst(policySource, 'LastWindowHour'),
};

for (const [name, value] of Object.entries(policy)) {
  if (value === null) note(POLICY_CS, `could not read \`${name}\` — the parser needs updating`);
}

const partialPct = Math.round(policy.PartialCancellationFeeRate * 100);
const lastMinutePct = Math.round(policy.LastMinuteCancellationFeeRate * 100);
const expressPct = Math.round(policy.ExpressSurchargeRate * 100);

// ─── 1. The web's shared mirror ─────────────────────────────────────────────
// It carries a "Mirrors BookingPolicy" comment and, until this checker, nothing that held it to it.
const webModel = read(WEB_MODEL);
for (const [tsName, csName] of [
  ['FIRST_WINDOW_HOUR', 'FirstWindowHour'],
  ['LAST_WINDOW_HOUR', 'LastWindowHour'],
  ['EXPRESS_LEAD_TIME_HOURS', 'ExpressLeadTimeHours'],
  ['STANDARD_LEAD_TIME_HOURS', 'StandardLeadTimeHours'],
  ['EXPRESS_SURCHARGE_RATE', 'ExpressSurchargeRate'],
]) {
  const actual = readTsConst(webModel, tsName);
  if (actual === null) {
    note(WEB_MODEL, `\`${tsName}\` not found`);
  } else if (actual !== policy[csName]) {
    note(WEB_MODEL, `\`${tsName}\` is ${actual}, BookingPolicy.${csName} is ${policy[csName]}`);
  }
}

// ─── 2. What the three clients TELL a customer a cancellation costs ─────────
const iosCatalog = JSON.parse(read(IOS_CATALOG));

for (const locale of LOCALES) {
  const web = JSON.parse(read(join(WEB_I18N, `${locale}.json`)));

  // The booking wizard's policy tiers.
  const wizard = web.pages?.order ?? {};
  for (const [key, expected] of [
    ['cancel_policy_tier3_value', partialPct],
    ['cancel_policy_tier4_value', lastMinutePct],
  ]) {
    const value = wizard[key];
    if (value === undefined) {
      note(`web/${locale}`, `pages.order.${key} is missing`);
    } else if (!percentagesIn(value).includes(expected)) {
      note(`web/${locale}`, `pages.order.${key} = "${value}" does not state ${expected}%`);
    }
  }

  // The home page's rules band states both rates in one sentence.
  const rules = web.pages?.home?.rules ?? {};
  const stated = percentagesIn(rules.cancel_desc ?? '');
  for (const expected of [partialPct, lastMinutePct]) {
    if (!stated.includes(expected)) {
      note(
        `web/${locale}`,
        `pages.home.rules.cancel_desc = "${rules.cancel_desc}" does not state ${expected}%`,
      );
    }
  }
  // …and the two lead-time claims beside it. "Earliest slot" is the EXPRESS floor: 4 was stated
  // here once, which denied the very band the surcharge chip next to it charges for.
  const earliest = firstNumberIn(rules.lead_value ?? '');
  if (earliest !== policy.ExpressLeadTimeHours) {
    note(
      `web/${locale}`,
      `pages.home.rules.lead_value = "${rules.lead_value}" says ${earliest} h; the floor is ` +
        `${policy.ExpressLeadTimeHours} h (BookingPolicy.ExpressLeadTimeHours)`,
    );
  }
  if (!percentagesIn(rules.express_value ?? '').includes(expressPct)) {
    note(
      `web/${locale}`,
      `pages.home.rules.express_value = "${rules.express_value}" does not state ${expressPct}%`,
    );
  }

  // The calculator's own date hint states the surcharge band, and it sat OUTSIDE this check while
  // its neighbours were inside it — so for as long as anyone can tell, five locales told a customer
  // booking two days out that they would pay 20% more. The band is
  // [ExpressLeadTimeHours, StandardLeadTimeHours): 2–4 hours. The hint names the upper edge, because
  // "less than 4 hours ahead" is the sentence a customer can act on; under the lower edge the
  // booking is refused outright rather than surcharged.
  const dateHint = web.pages?.home?.quote?.date_hint ?? '';
  const hintHours = firstNumberIn(dateHint);
  if (dateHint && hintHours !== policy.StandardLeadTimeHours) {
    note(
      `web/${locale}`,
      `pages.home.quote.date_hint = "${dateHint}" says ${hintHours} h; the express band ends at ` +
        `${policy.StandardLeadTimeHours} h (BookingPolicy.StandardLeadTimeHours)`,
    );
  }

  // Android.
  for (const [key, expected] of [
    ['booking_cancel_tier2_value', partialPct],
    ['booking_cancel_tier3_value', lastMinutePct],
  ]) {
    const value = androidString(ANDROID_DIRS[locale], key);
    if (value === null) {
      note(`android/${ANDROID_DIRS[locale]}`, `${key} is missing`);
    } else if (!percentagesIn(value).includes(expected)) {
      note(`android/${ANDROID_DIRS[locale]}`, `${key} = "${value}" does not state ${expected}%`);
    }
  }

  // iOS.
  for (const [key, expected] of [
    ['booking_cancel_tier2_value', partialPct],
    ['booking_cancel_tier3_value', lastMinutePct],
  ]) {
    const value = iosString(iosCatalog, key, locale);
    if (value === null) {
      note(`ios/${locale}`, `${key} is missing`);
    } else if (!percentagesIn(value).includes(expected)) {
      note(`ios/${locale}`, `${key} = "${value}" does not state ${expected}%`);
    }
  }
}

// ─── 3. Money figures in copy come from the MARKET, never from the translation (ADR-0060) ────
// The no-show credit is `Currency.NoShowCredit`, the insurance ceiling is
// `CountryConfiguration.InsuranceCoverageAmount`, and the currency a legal page names is the chosen
// market's — all data, none of it a constant this checker can read. What it CAN pin is the shape of
// the copy: the placeholder is present where a figure is rendered, no integer is baked in beside it,
// and no currency word rides along (the client formats the amount with its unit). A literal creeping
// back into any of these keys is the drift this section catches.

/** Every integer in a sentence once the loc-arg / interpolation slots are removed. */
export function bakedAmountsIn(text) {
  return amountsIn(String(text).replace(/\{\{\s*\w+\s*\}\}|%\d+\$[@sd]|%[@sd]/g, ''));
}

const CURRENCY_WORDS = /\bCZK\b|K\u010d|\bEUR\b|\u20ac|\bPLN\b|z\u0142|\bGBP\b|\u00a3|\bUSD\b|\$(?!\S)/;

function pinPlaceholderCopy(where, key, value, { placeholder, optional = false } = {}) {
  if (value === null || value === undefined) {
    if (!optional) note(where, `${key} is missing`);
    return;
  }
  if (placeholder && !value.includes(placeholder)) {
    note(where, `${key} = "${value}" does not carry the ${placeholder} placeholder`);
  }
  const baked = bakedAmountsIn(value);
  if (baked.length) {
    note(where, `${key} = "${value}" bakes a figure in (${baked.join(', ')}) — the amount comes from the market`);
  }
  if (CURRENCY_WORDS.test(value)) {
    note(where, `${key} = "${value}" names a currency — the client formats the unit`);
  }
}

for (const locale of LOCALES) {
  const web = JSON.parse(read(join(WEB_I18N, `${locale}.json`)));
  const rules = web.pages?.home?.rules ?? {};
  // The VALUE line renders the market's credit; its sibling is the variant for a market with none.
  pinPlaceholderCopy(`web/${locale}`, 'pages.home.rules.we_cancel_value', rules.we_cancel_value, { placeholder: '{{amount}}' });
  pinPlaceholderCopy(`web/${locale}`, 'pages.home.rules.we_cancel_value_refund_only', rules.we_cancel_value_refund_only);
  pinPlaceholderCopy(`web/${locale}`, 'terms_page.section3_text', web.terms_page?.section3_text, { placeholder: '{{currency}}' });
  pinPlaceholderCopy(`web/${locale}`, 'terms_page.section3_text_no_market', web.terms_page?.section3_text_no_market);
}

for (const [locale, dir] of Object.entries(ANDROID_DIRS)) {
  // The push carries the credit as its second loc-arg, formatted by the server in the credit's
  // own currency (owner ruling 2026-09-13; ADR-0025 D3 widened for this one event).
  pinPlaceholderCopy(`android/${locale}`, 'notification_order_no_cleaner_refunded_body', androidString(dir, 'notification_order_no_cleaner_refunded_body'), { placeholder: '%2$s' });
  pinPlaceholderCopy(`android/${locale}`, 'booking_trust_insured', androidString(dir, 'booking_trust_insured'), { placeholder: '%1$s' });
  pinPlaceholderCopy(`android/${locale}`, 'booking_trust_insured_no_figure', androidString(dir, 'booking_trust_insured_no_figure'));
  pinPlaceholderCopy(`android/${locale}`, 'help_faq_a3', androidString(dir, 'help_faq_a3'), { placeholder: '%1$s' });
  pinPlaceholderCopy(`android/${locale}`, 'help_faq_a3_no_figure', androidString(dir, 'help_faq_a3_no_figure'));
  if (androidString(dir, 'home_seasonal_subtitle') !== null) {
    note(`android/${locale}`, 'home_seasonal_subtitle is back — the seasonal card was deleted (ADR-0060 D2)');
  }
}

for (const locale of LOCALES) {
  pinPlaceholderCopy(`ios/${locale}`, 'push.order.no_cleaner_refunded.body', iosString(iosCatalog, 'push.order.no_cleaner_refunded.body', locale), { placeholder: '%2$@' });
  pinPlaceholderCopy(`ios/${locale}`, 'booking_trust_insured', iosString(iosCatalog, 'booking_trust_insured', locale), { placeholder: '%1$@' });
  pinPlaceholderCopy(`ios/${locale}`, 'booking_trust_insured_no_figure', iosString(iosCatalog, 'booking_trust_insured_no_figure', locale));
  pinPlaceholderCopy(`ios/${locale}`, 'help_faq_a3', iosString(iosCatalog, 'help_faq_a3', locale), { placeholder: '%1$@' });
  pinPlaceholderCopy(`ios/${locale}`, 'help_faq_a3_no_figure', iosString(iosCatalog, 'help_faq_a3_no_figure', locale));
  if (iosString(iosCatalog, 'home_seasonal_subtitle', locale) !== null) {
    note(`ios/${locale}`, 'home_seasonal_subtitle is back — the seasonal card was deleted (ADR-0060 D2)');
  }
}

// ─── 4. The legal text version the server stamps on an acceptance (ADR-0062 D4) ────────
// `LegalDocumentVersions` is what a consent row and a customer audit row carry; the legal pages
// render `terms_page.version` / `privacy_page.version` so the reader can see which text they agreed
// to. A legal-text edit bumps the constant AND the key in the same change — this is what notices when
// only one of them moved. Mobile catalogues are not pinned: they deep-link the web page.
const legalSource = read(LEGAL_VERSIONS_CS);
const legalVersions = {
  CustomerTerms: readCsStringConst(legalSource, 'CustomerTerms'),
  CustomerPrivacy: readCsStringConst(legalSource, 'CustomerPrivacy'),
};
for (const [name, value] of Object.entries(legalVersions)) {
  if (value === null) note(LEGAL_VERSIONS_CS, `could not read \`${name}\` — the parser needs updating`);
}

for (const locale of LOCALES) {
  const web = JSON.parse(read(join(WEB_I18N, `${locale}.json`)));
  for (const [key, csName] of [
    ['terms_page.version', 'CustomerTerms'],
    ['privacy_page.version', 'CustomerPrivacy'],
  ]) {
    const [page, member] = key.split('.');
    const value = web[page]?.[member];
    if (value === undefined) {
      note(`web/${locale}`, `${key} is missing`);
    } else if (value !== legalVersions[csName]) {
      note(`web/${locale}`, `${key} = "${value}", LegalDocumentVersions.${csName} is "${legalVersions[csName]}"`);
    }
  }
}

// ─── Report ─────────────────────────────────────────────────────────────────
if (findings.length) {
  console.log('booking-policy-parity violations:');
  for (const f of findings) console.log(`  ${f}`);
  console.log(
    '\nBookingPolicy and LegalDocumentVersions are the authority. Change the COPY to match them — or,' +
      '\nif the policy or the legal text itself is moving, change the constant first and let this check' +
      '\ntell you every surface that quotes it.',
  );
} else {
  console.log(
    `booking-policy-parity: ${LOCALES.length} locale(s) × web + android + ios agree with ` +
      `BookingPolicy — cancellation ${partialPct}%/${lastMinutePct}%, express +${expressPct}% ` +
      `from ${policy.ExpressLeadTimeHours} h, window ${policy.FirstWindowHour}:00–${policy.LastWindowHour}:00; ` +
      `money figures in copy come from the market; legal texts at ${legalVersions.CustomerTerms} / ` +
      legalVersions.CustomerPrivacy,
  );
}

process.exit(findings.length ? 1 : 0);
