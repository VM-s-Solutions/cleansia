#!/usr/bin/env node
/**
 * Self-test for the booking-policy parity gate (`check-booking-policy-parity.mjs`).
 *
 * It never touches the working tree: every scenario materialises a fixture repository under a
 * throwaway directory — a BookingPolicy.cs, the web's shared model, five web locale files, five
 * Android string files and an iOS catalog — and runs the tool against it with `--root=`.
 *
 * WHAT THIS HAS TO PROVE, in order of what it cost to learn:
 *
 *   1. THE GATE CAN STILL FAIL. Stub the tool to `process.exit(0)` and the drift scenarios go green.
 *      A defanged gate reads exactly like a clean tree, and this one exists precisely because four
 *      trees drifted for months with every CI job passing.
 *   2. IT CATCHES THE TWO REAL DEFECTS. Both mobile apps stating "100% charge" under 4 hours against
 *      a 50% policy, and the home page stating a 4-hour floor against a 2-hour one. Those are the
 *      bugs that motivated it; a gate that would not have caught them is decoration.
 *   3. BOOKINGPOLICY IS THE AUTHORITY, NOT THE COPY. Moving the C# constant must fail every surface
 *      still quoting the old number — otherwise the check freezes today's copy rather than tracking
 *      the rule, and the first real policy change would be reported backwards.
 *   4. IT DOES NOT CRY WOLF. Czech and Russian put a space before the percent sign, Russian and
 *      Ukrainian use a comma decimal, and translators reorder sentences. Each of those, reported
 *      once, turns a gate into noise nobody reads — which is worse than no gate, because it hides
 *      the true positives too.
 */
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { tmpdir } from 'node:os';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const TOOL = join(HERE, 'check-booking-policy-parity.mjs');

const LOCALES = ['en', 'cs', 'sk', 'ru', 'uk'];
const ANDROID_DIRS = { en: 'values', cs: 'values-cs', sk: 'values-sk', ru: 'values-ru', uk: 'values-uk' };

let passed = 0;
const failures = [];

function write(root, rel, contents) {
  const path = join(root, rel);
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, contents, 'utf8');
}

/** A fixture repository whose four trees all agree with its BookingPolicy. */
function buildFixture(overrides = {}) {
  const root = mkdtempSync(join(tmpdir(), 'booking-policy-parity-'));
  const o = {
    partialRate: '0.25m',
    lastMinuteRate: '0.50m',
    expressRate: '0.20m',
    expressLead: '2',
    standardLead: '4',
    firstHour: '8',
    lastHour: '20',
    tsExpressRate: '0.2',
    tsExpressLead: '2',
    webTier3: '25% charge',
    webTier4: '50% charge',
    webCancelDesc: 'Between 4 and 24 hours it is 25%, under 4 hours 50%.',
    webLeadValue: 'in 2 hours',
    webExpressValue: '+20%',
    webDateHint: 'Optional. Booking less than 4 hours ahead adds an express surcharge.',
    androidTier2: '25% charge',
    androidTier3: '50% charge',
    iosTier2: '25% charge',
    iosTier3: '50% charge',
    noShowCredit: '250m',
    webWeCancelValue: 'Everything back + 250 CZK credit',
    androidNoShowBody: 'Nobody could take booking #%1$s, so we refunded it and added 250 Kč credit.',
    iosNoShowBody: 'Nobody could take booking #%1$@, so we refunded it and added 250 Kč credit.',
    ...overrides,
  };

  write(root, 'src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs', `
public static class BookingPolicy
{
    public const int StandardLeadTimeHours = ${o.standardLead};
    public const int ExpressLeadTimeHours = ${o.expressLead};
    public const decimal ExpressSurchargeRate = ${o.expressRate};
    public const int FirstWindowHour = ${o.firstHour};
    public const int LastWindowHour = ${o.lastHour};
    public const int FreeCancellationHours = 24;
    public const decimal PartialCancellationFeeRate = ${o.partialRate};
    public const decimal LastMinuteCancellationFeeRate = ${o.lastMinuteRate};
    public const int PartialCancellationHours = 4;
    public const decimal NoShowCreditCzk = ${o.noShowCredit};
}
`);

  write(root, 'src/Cleansia.App/libs/shared/models/src/lib/models/booking-window.models.ts', `
export const FIRST_WINDOW_HOUR = ${o.firstHour};
export const LAST_WINDOW_HOUR = ${o.lastHour};
export const EXPRESS_LEAD_TIME_HOURS = ${o.tsExpressLead};
export const STANDARD_LEAD_TIME_HOURS = ${o.standardLead};
export const EXPRESS_SURCHARGE_RATE = ${o.tsExpressRate};
`);

  for (const locale of LOCALES) {
    write(
      root,
      `src/Cleansia.App/apps/cleansia.app/src/assets/i18n/${locale}.json`,
      JSON.stringify({
        pages: {
          order: {
            cancel_policy_tier3_value: o.webTier3,
            cancel_policy_tier4_value: o.webTier4,
          },
          home: {
            rules: {
              cancel_desc: o.webCancelDesc,
              lead_value: o.webLeadValue,
              express_value: o.webExpressValue,
              we_cancel_value: o.webWeCancelValue,
            },
            quote: {
              date_hint: o.webDateHint,
            },
          },
        },
      }, null, 2),
    );

    write(
      root,
      `src/cleansia_android/customer-app/src/main/res/${ANDROID_DIRS[locale]}/strings.xml`,
      `<resources>
    <string name="booking_cancel_tier2_value">${o.androidTier2}</string>
    <string name="booking_cancel_tier3_value">${o.androidTier3}</string>
    <string name="notification_order_no_cleaner_refunded_body">${o.androidNoShowBody}</string>
</resources>`,
    );
  }

  const localizations = (value) =>
    Object.fromEntries(LOCALES.map((l) => [l, { stringUnit: { value } }]));
  write(
    root,
    'src/cleansia_ios/CleansiaCustomer/Resources/Localizable.xcstrings',
    JSON.stringify({
      strings: {
        booking_cancel_tier2_value: { localizations: localizations(o.iosTier2) },
        booking_cancel_tier3_value: { localizations: localizations(o.iosTier3) },
        'push.order.no_cleaner_refunded.body': { localizations: localizations(o.iosNoShowBody) },
      },
    }, null, 2),
  );

  return root;
}

/** Run the tool (or a stubbed copy of it) against a fixture. */
function run(root, { defanged = false } = {}) {
  let tool = TOOL;
  if (defanged) {
    tool = join(root, 'defanged.mjs');
    writeFileSync(tool, 'process.exit(0);\n', 'utf8');
  }
  try {
    const stdout = execFileSync(process.execPath, [tool, `--root=${root}`], { encoding: 'utf8' });
    return { code: 0, stdout };
  } catch (error) {
    return { code: error.status ?? 1, stdout: `${error.stdout ?? ''}${error.stderr ?? ''}` };
  }
}

function scenario(name, overrides, expect) {
  const root = buildFixture(overrides);
  try {
    const result = run(root);
    const problems = [];
    if (result.code !== expect.code) {
      problems.push(`exit ${result.code}, wanted ${expect.code}`);
    }
    for (const needle of expect.mentions ?? []) {
      if (!result.stdout.includes(needle)) problems.push(`output never mentioned "${needle}"`);
    }
    for (const needle of expect.silentAbout ?? []) {
      if (result.stdout.includes(needle)) problems.push(`output should not mention "${needle}"`);
    }
    if (problems.length) {
      failures.push(`${name}\n      ${problems.join('\n      ')}\n      --- output ---\n${result.stdout.replace(/^/gm, '      ')}`);
    } else {
      passed++;
    }
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

// ─── 1. A tree that agrees passes ───────────────────────────────────────────
scenario('a tree whose four surfaces agree passes', {}, { code: 0 });

// ─── 2. The gate can still fail ─────────────────────────────────────────────
{
  const root = buildFixture({ androidTier3: '100% charge' });
  try {
    const real = run(root);
    const stub = run(root, { defanged: true });
    if (real.code === 0) {
      failures.push('the gate did not fail on injected drift — it cannot catch anything');
    } else if (stub.code !== 0) {
      failures.push('the defanged control did not pass — the harness proves nothing');
    } else {
      passed++;
    }
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

// ─── 2b. The no-show apology, which is quoted as an AMOUNT rather than a percentage ─────
// Added the day the push started stating the figure. The 250 cannot be a loc arg — the lock-screen
// allowlist is a closed {orderNumber, count} set — so it is written into fifteen strings by hand,
// and this is the half of the gate that holds them to the constant.
scenario(
  'catches a home page still quoting the old apology amount',
  { noShowCredit: '300m' },
  { code: 1, mentions: ['web/en', 'does not state 300'] },
);
scenario(
  'catches an Android push still quoting the old apology amount',
  { androidNoShowBody: 'We refunded it and added 500 Kč credit.' },
  { code: 1, mentions: ['android/en', 'does not state 250'] },
);
scenario(
  'catches an iOS push still quoting the old apology amount',
  { iosNoShowBody: 'We refunded it and added 500 Kč credit.' },
  { code: 1, mentions: ['ios/en', 'does not state 250'] },
);
// The desc line beside the value explains WHO qualifies and carries no number on purpose; asserting
// on it would have made the gate cry wolf on honest copy, which the header calls the worse failure.
scenario(
  'says nothing about the we_cancel_desc line, which quotes no amount',
  {},
  { code: 0, silentAbout: ['we_cancel_desc'] },
);

// ─── 3. The two defects that motivated this gate ────────────────────────────
scenario(
  'catches the mobile "100% charge" defect on Android',
  { androidTier3: '100% charge' },
  { code: 1, mentions: ['android/values', 'does not state 50%'] },
);
scenario(
  'catches the mobile "100% charge" defect on iOS',
  { iosTier3: '100% charge' },
  { code: 1, mentions: ['ios/en', 'does not state 50%'] },
);
scenario(
  'catches the mobile "50% charge" mid-tier defect',
  { androidTier2: '50% charge', iosTier2: '50% charge' },
  { code: 1, mentions: ['booking_cancel_tier2_value', 'does not state 25%'] },
);
scenario(
  'catches the home page claiming a 4-hour floor',
  { webLeadValue: 'in 4 hours' },
  { code: 1, mentions: ['lead_value', 'the floor is 2 h'] },
);
// The calculator's date hint shipped saying "within 48 hours" in all five locales while the band is
// 2-4 h — a customer booking two days out was told they would pay 20% more. It sat just outside this
// gate while both its neighbours were inside it, which is the whole reason it drifted.
scenario(
  'catches the date hint overstating the express band',
  { webDateHint: 'Optional. Booking within 48 hours adds an express surcharge.' },
  { code: 1, mentions: ['date_hint', 'says 48 h', 'ends at 4 h'] },
);
scenario(
  'reads the band from a localised hint wherever the number sits',
  { webDateHint: 'Nepovinné. Objednávka méně než 4 hodiny předem má expresní příplatek.' },
  { code: 0 },
);
scenario(
  'catches the unguarded web mirror drifting from BookingPolicy',
  { tsExpressRate: '0.15' },
  { code: 1, mentions: ['EXPRESS_SURCHARGE_RATE', 'is 0.15'] },
);

// ─── 4. BookingPolicy is the authority, not the copy ────────────────────────
scenario(
  'moving the C# rate fails every surface still quoting the old one',
  { partialRate: '0.30m' },
  {
    code: 1,
    mentions: ['does not state 30%', 'android/values', 'ios/en'],
  },
);
scenario(
  'moving the C# lead time fails the home page still quoting the old one',
  { expressLead: '3', tsExpressLead: '3' },
  { code: 1, mentions: ['the floor is 3 h'] },
);

// ─── 5. It does not cry wolf ────────────────────────────────────────────────
scenario(
  'a space before the percent sign is the same number',
  { androidTier2: '25 % poplatok', iosTier2: '25 % штраф', webTier3: '25 % poplatok' },
  { code: 0 },
);
scenario(
  'a reordered sentence still states both rates',
  { webCancelDesc: 'Under 4 hours it is 50%; between 4 and 24 hours, 25%.' },
  { code: 0 },
);
scenario(
  'the hours in the sentence are never mistaken for percentages',
  { webCancelDesc: 'Mezi 4 a 24 hodinami 25 %, pod 4 hodiny 50 %.' },
  { code: 0 },
);
scenario(
  'a localised lead-time claim reads its number wherever it sits',
  { webLeadValue: 'через 2 часа' },
  { code: 0 },
);

// ─── 6. A missing key is a finding, not a silent pass ───────────────────────
{
  const root = buildFixture();
  try {
    const catalogPath = join(
      root, 'src/cleansia_ios/CleansiaCustomer/Resources/Localizable.xcstrings');
    const catalog = JSON.parse(readFileSync(catalogPath, 'utf8'));
    delete catalog.strings.booking_cancel_tier3_value;
    writeFileSync(catalogPath, JSON.stringify(catalog, null, 2), 'utf8');
    const result = run(root);
    if (result.code === 1 && result.stdout.includes('is missing')) passed++;
    else failures.push(`a deleted iOS key passed silently\n${result.stdout}`);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

// ─── Report ─────────────────────────────────────────────────────────────────
if (failures.length) {
  console.log(`booking-policy-parity self-test: ${failures.length} scenario(s) FAILED\n`);
  for (const f of failures) console.log(`  - ${f}\n`);
  process.exit(1);
}
console.log(`booking-policy-parity self-test: ${passed} scenario(s) pass`);
