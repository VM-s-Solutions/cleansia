#!/usr/bin/env node
/**
 * The cross-stack booking-policy parity check.
 *
 * `BookingPolicy` decides what a cancellation costs, how late a booking is accepted and what an
 * express slot adds. Four surfaces then STATE those numbers to a customer — the web copy, the web's
 * shared constants, Android's string resources and iOS's string catalog — and every one of them
 * holds its own literal. Nothing compiled them together, so nothing noticed when they stopped
 * agreeing. The legal seed is held to the same shape (§3): the market's figures reach a legal text
 * through a placeholder the server fills, never as a literal in one language's file.
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
import { readFileSync, existsSync, readdirSync } from 'node:fs';
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
/** The platform's own cancellation reasons — keys the three customer clients turn into a sentence. */
const REASONS_CS = 'src/Cleansia.Core.Domain/Orders/OrderCancellationReasons.cs';
const WEB_REASON_MAP = 'src/Cleansia.App/libs/cleansia-customer-features/orders/src/lib/order-detail/order-detail.component.ts';
const ANDROID_REASON_MAP = 'src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt';
const IOS_REASON_MAP = 'src/cleansia_ios/CleansiaCustomer/Sources/Features/Orders/CancellationReasonCopy.swift';
/** The customer legal texts, one dated folder per version, one markdown file per language. */
const LEGAL_SEED = 'src/Cleansia.Infra.Database/Seed/Legal/customer';
const LEGAL_SEED_TYPES = ['terms-of-service', 'privacy-policy'];
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
}

// The legal texts are stored documents seeded from markdown, one folder per effective date, and the
// server fills `{{currency}}` with the market's code before rendering. Only the NEWEST version is
// read: a version already in force is immutable, so a finding against it could never be fixed in
// place — the fix is always the next dated folder, which is where this looks.
//
// What this does NOT read: a figure in a paragraph with no placeholder beside it. A legal text
// states hours, percentages and a phone number on purpose, so the baked-figure check runs only
// where a `{{…}}` slot says a market value is rendered; a bare "1 000 000" elsewhere is caught only
// if a currency word rides along with it.

/** The greatest `yyyy-MM-dd` folder under `<type>/any/`, or null when there is none. */
export function newestSeedVersion(root, type) {
  const dir = join(root, LEGAL_SEED, type, 'any');
  if (!existsSync(dir)) return null;
  const versions = readdirSync(dir).filter((name) => /^\d{4}-\d{2}-\d{2}$/.test(name)).sort();
  return versions.length ? versions[versions.length - 1] : null;
}

/** The markdown paragraphs after the front-matter block. */
export function seedParagraphs(markdown) {
  const text = markdown.replace(/\r\n/g, '\n').replace(/^\uFEFF/, '');
  const body = text.startsWith('---\n') ? text.slice(text.indexOf('\n---\n', 4) + 5) : text;
  return body.split(/\n\s*\n/).map((p) => p.trim()).filter(Boolean);
}

function pinSeedText(where, markdown, { placeholder } = {}) {
  const paragraphs = seedParagraphs(markdown);
  const text = paragraphs.join('\n');
  if (placeholder && !text.includes(placeholder)) {
    note(where, `does not carry the ${placeholder} placeholder — the currency is the market's`);
  }
  for (const paragraph of paragraphs) {
    if (!/\{\{\s*\w+\s*\}\}/.test(paragraph)) continue;
    const baked = bakedAmountsIn(paragraph);
    if (baked.length) {
      note(where, `"${paragraph}" bakes a figure in (${baked.join(', ')}) — the amount comes from the market`);
    }
  }
  if (CURRENCY_WORDS.test(text)) {
    note(where, 'names a currency — the server fills the market\'s unit into the placeholder');
  }
}

for (const type of LEGAL_SEED_TYPES) {
  const version = newestSeedVersion(REPO, type);
  if (version === null) {
    note(`${LEGAL_SEED}/${type}/any`, 'has no dated version folder — the customer legal text has no seed');
    continue;
  }
  for (const locale of LOCALES) {
    const rel = `${LEGAL_SEED}/${type}/any/${version}/${locale}.md`;
    if (!existsSync(join(REPO, rel))) {
      note(rel, 'is missing');
      continue;
    }
    pinSeedText(rel, read(rel), type === 'terms-of-service' ? { placeholder: '{{currency}}' } : {});
  }
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

// ─── 4. Every platform cancellation reason renders as a sentence in the three customer clients ────
// `OrderCancellationReasons` is a cross-assembly contract: the sweeps write the key and each client
// maps it to copy in five locales. A key added on the server without its three maps reaches the
// customer as silence (the clients render nothing for an unknown key, by design), which no compiler
// sees. The maps are read as source and the copy as the locale files.

/** `public const string X = "order.cancelled.y";` → ["order.cancelled.y", …]. */
export function readCancellationReasons(source) {
  return [...source.matchAll(/public\s+const\s+string\s+\w+\s*=\s*"(order\.cancelled\.[a-z_]+)"\s*;/g)].map(
    (m) => m[1],
  );
}

/**
 * Keys the server writes that no client renders yet. A gap named here is a finding for the owner,
 * not a pass: the list exists so the gate fails on the NEXT key while a known one is reported.
 * Empty today — every declared reason renders on all three clients.
 */
const REASONS_NOT_YET_RENDERED = new Set();

const reasons = existsSync(join(REPO, REASONS_CS)) ? readCancellationReasons(read(REASONS_CS)) : [];
if (reasons.length === 0) note(REASONS_CS, 'declares no cancellation reason — the parser needs updating');

const webReasonMap = existsSync(join(REPO, WEB_REASON_MAP)) ? read(WEB_REASON_MAP) : '';
const androidReasonMap = existsSync(join(REPO, ANDROID_REASON_MAP)) ? read(ANDROID_REASON_MAP) : '';
const iosReasonMap = existsSync(join(REPO, IOS_REASON_MAP)) ? read(IOS_REASON_MAP) : '';

for (const reason of reasons) {
  if (REASONS_NOT_YET_RENDERED.has(reason)) continue;
  const suffix = reason.slice('order.cancelled.'.length);

  const webKey = `pages.order_detail.cancellation_reason.${suffix}`;
  if (!webReasonMap.includes(`'${reason}'`) || !webReasonMap.includes(`'${webKey}'`)) {
    note(WEB_REASON_MAP, `does not map ${reason} to ${webKey}`);
  }
  const androidResource = `order_cancelled_reason_${suffix}`;
  if (!androidReasonMap.includes(`"${reason}"`) || !androidReasonMap.includes(`R.string.${androidResource}`)) {
    note(ANDROID_REASON_MAP, `does not map ${reason} to R.string.${androidResource}`);
  }
  if (!iosReasonMap.includes(`"${reason}": "${androidResource}"`)) {
    note(IOS_REASON_MAP, `does not map ${reason} to ${androidResource}`);
  }

  for (const locale of LOCALES) {
    const web = JSON.parse(read(join(WEB_I18N, `${locale}.json`)));
    const sentence = web.pages?.order_detail?.cancellation_reason?.[suffix];
    if (!sentence) note(`web/${locale}`, `${webKey} is missing`);
    if (androidString(ANDROID_DIRS[locale], androidResource) === null) {
      note(`android/${ANDROID_DIRS[locale]}`, `${androidResource} is missing`);
    }
    if (iosString(iosCatalog, androidResource, locale) === null) {
      note(`ios/${locale}`, `${androidResource} is missing`);
    }
  }
}

for (const known of REASONS_NOT_YET_RENDERED) {
  if (!reasons.includes(known)) {
    note(REASONS_CS, `${known} is on the not-yet-rendered list but no longer declared — drop it from the list`);
  }
}

// ─── Report ─────────────────────────────────────────────────────────────────
if (findings.length) {
  console.log('booking-policy-parity violations:');
  for (const f of findings) console.log(`  ${f}`);
  console.log(
    '\nBookingPolicy is the authority and a money figure belongs to the market. Change the COPY to' +
      '\nmatch — or, if the policy itself is moving, change the constant first and let this check tell' +
      '\nyou every surface that quotes it. A legal text is fixed by its next dated seed folder.',
  );
} else {
  const seedVersions = LEGAL_SEED_TYPES.map((type) => newestSeedVersion(REPO, type)).join(' / ');
  console.log(
    `booking-policy-parity: ${LOCALES.length} locale(s) × web + android + ios agree with ` +
      `BookingPolicy — cancellation ${partialPct}%/${lastMinutePct}%, express +${expressPct}% ` +
      `from ${policy.ExpressLeadTimeHours} h, window ${policy.FirstWindowHour}:00–${policy.LastWindowHour}:00; ` +
      `money figures in copy come from the market; legal seed ${seedVersions} carries the placeholders; ` +
      `${reasons.length - REASONS_NOT_YET_RENDERED.size} cancellation reason(s) render on every client`,
  );
}

process.exit(findings.length ? 1 : 0);
