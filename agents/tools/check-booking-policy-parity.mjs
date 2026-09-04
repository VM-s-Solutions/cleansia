#!/usr/bin/env node
/**
 * The cross-stack booking-policy parity check.
 *
 * `BookingPolicy` decides what a cancellation costs, how late a booking is accepted and what an
 * express slot adds. Four surfaces then STATE those numbers to a customer — the web copy, the web's
 * shared constants, Android's string resources and iOS's string catalog — and every one of them
 * holds its own literal. Nothing compiled them together, so nothing noticed when they stopped
 * agreeing.
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

// ─── Report ─────────────────────────────────────────────────────────────────
if (findings.length) {
  console.log('booking-policy-parity violations:');
  for (const f of findings) console.log(`  ${f}`);
  console.log(
    '\nBookingPolicy is the authority. Change the COPY to match it — or, if the policy itself is' +
      '\nmoving, change BookingPolicy first and let this check tell you every surface that quotes it.',
  );
} else {
  console.log(
    `booking-policy-parity: ${LOCALES.length} locale(s) × web + android + ios agree with ` +
      `BookingPolicy — cancellation ${partialPct}%/${lastMinutePct}%, express +${expressPct}% ` +
      `from ${policy.ExpressLeadTimeHours} h, window ${policy.FirstWindowHour}:00–${policy.LastWindowHour}:00`,
  );
}

process.exit(findings.length ? 1 : 0);
