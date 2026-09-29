#!/usr/bin/env node
/**
 * Self-test for the booking-policy parity gate (`check-booking-policy-parity.mjs`).
 *
 * It never touches the working tree: every scenario materialises a fixture repository under a
 * throwaway directory — a BookingPolicy.cs, the web's shared model, five web locale files, five
 * Android string files, an iOS catalog and the legal seed's markdown files — and runs the tool
 * against it with `--root=`.
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
    /** `null` leaves the constant out of the fixture's BookingPolicy. */
    increasedDirtinessRate: '0.30m',
    heavyDirtinessRate: '0.60m',
    expressLead: '2',
    standardLead: '4',
    firstHour: '8',
    lastHour: '20',
    tsExpressRate: '0.2',
    tsExpressLead: '2',
    tsIncreasedDirtinessRate: '0.3',
    tsHeavyDirtinessRate: '0.6',
    webDirtinessSurcharge: '+{{rate}}%',
    androidIncreasedPrice: '+30%',
    androidHeavyPrice: '+60%',
    androidSurchargeIncreased: 'Increased dirtiness surcharge (+30%)',
    androidSurchargeHeavy: 'Heavy dirtiness surcharge (+60%)',
    iosIncreasedRate: '+30%',
    iosHeavyRate: '+60%',
    iosSurchargeIncreased: 'Increased dirtiness surcharge (+30%)',
    iosSurchargeHeavy: 'Heavy dirtiness surcharge (+60%)',
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
    graceStandard: '15',
    /** `null` leaves the constant out of the fixture's BookingPolicy. */
    graceFirstBooking: '60',
    gracePlus: '60',
    webGraceWithout: '15 minutes (60 on your first booking)',
    webGraceWith: '60 minutes',
    webPlusPerkGrace: '60 minutes after booking to cancel free, instead of 15 minutes',
    webRethinkDesc: 'Once someone has, you still have 15 minutes from when you booked — 60 minutes on your first booking or with Cleansia Plus.',
    webHomePlusPerkGrace: 'Cancel free within 60 minutes of every booking, instead of 15 minutes',
    webCancelPolicyNote: 'Free cancellation up to 24 hours ahead. Within 15 minutes of booking you pay nothing — within 60 minutes on your first booking or with Cleansia Plus.',
    webMembershipPerkGrace: '60 minutes after every booking to cancel free, instead of 15 minutes',
    webPlusPerkCancelBody: 'Without membership the line is 24 hours. And after every booking you have 60 minutes to cancel free of charge, instead of 15 minutes.',
    androidGraceNote: 'Within 15 minutes of booking you pay nothing — within 60 minutes on your first booking or with Cleansia Plus.',
    androidPerkGrace: '60 minutes after booking to cancel free, instead of 15 minutes',
    androidFaqA1: 'Cancellations free of charge up to 24 hours before. Within 15 minutes of booking you pay nothing — within 60 minutes on your first booking or with Cleansia Plus.',
    iosGraceNote: 'Within 15 minutes of booking you pay nothing — within 60 minutes on your first booking or with Cleansia Plus.',
    iosPerkGrace: '60 minutes after booking to cancel free, instead of 15 minutes',
    iosFaqA1: 'Cancellations are free up to 24 hours before. Within 15 minutes of booking you pay nothing — within 60 minutes on your first booking or with Cleansia Plus.',
    webWeCancelValue: 'Everything back + {{amount}} credit',
    webWeCancelRefundOnly: 'Everything back',
    /**
     * The legal seed: the newest version's five language files per document. The terms carry the
     * policy's own figures and a phone number on purpose — integers a legal text legitimately
     * states, which the gate must not mistake for a baked amount.
     */
    seedVersion: '2026-09-14',
    seedTerms:
      'Please read these terms carefully.\n\n' +
      '## Ordering & Payment\n\n' +
      'Orders can be placed through our website. Prices are displayed in {{currency}} and are the final amount payable.\n\n' +
      '## Cancellation Policy\n\n' +
      'Once accepted: free 24+ hours before start, 25% fee 4-24 hours before, 50% fee under 4 hours before start.\n\n' +
      '## Contact\n\n' +
      'Write to info@cleansia.cz or +420 739 788 108.',
    seedPrivacy: 'Your privacy matters.\n\n## Data We Collect\n\nName, email, phone number and address.',
    /** The contract for work (ADR-0068): the price is a term of the order's snapshot, never a figure in the text. */
    seedWorkContract: 'The contract for work.\n\n## Price\n\nThe price of the work is the price shown at booking, in {{currency}}.',
    /** Per-file body overrides keyed `<type>/<lang>`, for the "one translator edited one file" case. */
    seedByFile: {},
    /** The languages the newest version carries; a missing file is a finding. */
    seedLanguages: LOCALES,
    /** Other dated versions, `{ '<yyyy-MM-dd>': { '<type>': '<body>' } }` — only the newest is read. */
    seedOtherVersions: {},
    /** `false` leaves the seed tree out entirely. */
    seedPresent: true,
    androidNoShowBody: 'Nobody could take booking #%1$s, so we refunded it and added %2$s credit towards your next clean.',
    androidInsured: 'Insured up to %1$s',
    androidInsuredNoFigure: 'Insured',
    androidFaq: 'Covered by insurance up to %1$s per booking.',
    androidFaqNoFigure: 'Covered by insurance.',
    androidSeasonal: null,
    iosNoShowBody: 'Nobody could take booking #%1$@, so we refunded it and added %2$@ credit towards your next clean.',
    iosInsured: 'Insured up to %1$@',
    iosInsuredNoFigure: 'Insured',
    iosFaq: 'Covered by insurance up to %1$@ per booking.',
    iosFaqNoFigure: 'Covered by insurance.',
    iosSeasonal: null,
    // The platform's cancellation reasons: every declared key is mapped and localised on the three
    // clients unless the scenario drops one surface.
    reasons: ['payment_not_completed', 'company_wind_down', 'no_cleaner_available'],
    reasonMissingOn: null,
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
    public const int OopsWindowMinutesStandard = ${o.graceStandard};
${o.graceFirstBooking === null ? '' : `    public const int OopsWindowMinutesFirstBooking = ${o.graceFirstBooking};`}
    public const int OopsWindowMinutesPlus = ${o.gracePlus};
${o.increasedDirtinessRate === null ? '' : `    public const decimal IncreasedDirtinessSurchargeRate = ${o.increasedDirtinessRate};`}
${o.heavyDirtinessRate === null ? '' : `    public const decimal HeavyDirtinessSurchargeRate = ${o.heavyDirtinessRate};`}
}
`);

  if (o.seedPresent) {
    const seedFile = (type, version, lang, body) =>
      write(
        root,
        `src/Cleansia.Infra.Database/Seed/Legal/customer/${type}/any/${version}/${lang}.md`,
        `---\ntitle: ${type} ${lang}\n---\n\n${body}\n`,
      );
    for (const lang of o.seedLanguages) {
      seedFile('terms-of-service', o.seedVersion, lang, o.seedByFile[`terms-of-service/${lang}`] ?? o.seedTerms);
      seedFile('privacy-policy', o.seedVersion, lang, o.seedByFile[`privacy-policy/${lang}`] ?? o.seedPrivacy);
      seedFile('work-contract', o.seedVersion, lang, o.seedByFile[`work-contract/${lang}`] ?? o.seedWorkContract);
    }
    for (const [version, byType] of Object.entries(o.seedOtherVersions)) {
      for (const [type, body] of Object.entries(byType)) {
        for (const lang of LOCALES) seedFile(type, version, lang, body);
      }
    }
  }

  const reasonConsts = o.reasons
    .map((r) => `    public const string R_${r} = "order.cancelled.${r}";`)
    .join('\n');
  write(root, 'src/Cleansia.Core.Domain/Orders/OrderCancellationReasons.cs', `
public static class OrderCancellationReasons
{
${reasonConsts}
}
`);
  const mapped = (surface) => o.reasons.filter(() => o.reasonMissingOn !== surface);
  write(
    root,
    'src/Cleansia.App/libs/cleansia-customer-features/orders/src/lib/order-detail/order-detail.component.ts',
    mapped('web')
      .map((r) => `      case 'order.cancelled.${r}':\n        return 'pages.order_detail.cancellation_reason.${r}';`)
      .join('\n'),
  );
  write(
    root,
    'src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/orders/OrderDetailScreen.kt',
    mapped('android')
      .map((r) => `    "order.cancelled.${r}" ->\n        stringResource(R.string.order_cancelled_reason_${r})`)
      .join('\n'),
  );
  write(
    root,
    'src/cleansia_ios/CleansiaCustomer/Sources/Features/Orders/CancellationReasonCopy.swift',
    mapped('ios')
      .map((r) => `        "order.cancelled.${r}": "order_cancelled_reason_${r}",`)
      .join('\n'),
  );

  write(root, 'src/Cleansia.App/libs/shared/models/src/lib/models/booking-window.models.ts', `
export const FIRST_WINDOW_HOUR = ${o.firstHour};
export const LAST_WINDOW_HOUR = ${o.lastHour};
export const EXPRESS_LEAD_TIME_HOURS = ${o.tsExpressLead};
export const STANDARD_LEAD_TIME_HOURS = ${o.standardLead};
export const EXPRESS_SURCHARGE_RATE = ${o.tsExpressRate};
export const INCREASED_DIRTINESS_SURCHARGE_RATE = ${o.tsIncreasedDirtinessRate};
export const HEAVY_DIRTINESS_SURCHARGE_RATE = ${o.tsHeavyDirtinessRate};
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
            cancel_policy_note: o.webCancelPolicyNote,
            plus_perk_grace: o.webPlusPerkGrace,
            dirtiness: { surcharge: o.webDirtinessSurcharge },
          },
          plus: {
            row_grace_without: o.webGraceWithout,
            row_grace_with: o.webGraceWith,
            perk_cancel_body: o.webPlusPerkCancelBody,
          },
          membership: {
            perk_grace: o.webMembershipPerkGrace,
          },
          home: {
            plus: {
              perk_grace: o.webHomePlusPerkGrace,
            },
            rules: {
              cancel_desc: o.webCancelDesc,
              rethink_desc: o.webRethinkDesc,
              lead_value: o.webLeadValue,
              express_value: o.webExpressValue,
              we_cancel_value: o.webWeCancelValue,
              we_cancel_value_refund_only: o.webWeCancelRefundOnly,
            },
            quote: {
              date_hint: o.webDateHint,
            },
          },
          order_detail: {
            cancellation_reason: Object.fromEntries(
              mapped('web-locale').map((r) => [r, `Reason ${r} (${locale})`]),
            ),
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
    <string name="booking_cancel_grace_note">${o.androidGraceNote}</string>
    <string name="membership_perk_grace_desc">${o.androidPerkGrace}</string>
    <string name="help_faq_a1">${o.androidFaqA1}</string>
    <string name="dirtiness_increased_price" formatted="false">${o.androidIncreasedPrice}</string>
    <string name="dirtiness_heavy_price" formatted="false">${o.androidHeavyPrice}</string>
    <string name="dirtiness_surcharge_increased" formatted="false">${o.androidSurchargeIncreased}</string>
    <string name="dirtiness_surcharge_heavy" formatted="false">${o.androidSurchargeHeavy}</string>
    <string name="notification_order_no_cleaner_refunded_body">${o.androidNoShowBody}</string>
    <string name="booking_trust_insured">${o.androidInsured}</string>
    <string name="booking_trust_insured_no_figure">${o.androidInsuredNoFigure}</string>
    <string name="help_faq_a3">${o.androidFaq}</string>
    <string name="help_faq_a3_no_figure">${o.androidFaqNoFigure}</string>
${mapped('android-locale').map((r) => `    <string name="order_cancelled_reason_${r}">Reason ${r}</string>\n`).join('')}${o.androidSeasonal === null ? '' : `    <string name="home_seasonal_subtitle">${o.androidSeasonal}</string>\n`}</resources>`,
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
        booking_cancel_grace_note: { localizations: localizations(o.iosGraceNote) },
        membership_perk_grace_desc: { localizations: localizations(o.iosPerkGrace) },
        help_faq_a1: { localizations: localizations(o.iosFaqA1) },
        booking_dirtiness_increased_rate: { localizations: localizations(o.iosIncreasedRate) },
        booking_dirtiness_heavy_rate: { localizations: localizations(o.iosHeavyRate) },
        booking_dirtiness_surcharge_increased: { localizations: localizations(o.iosSurchargeIncreased) },
        booking_dirtiness_surcharge_heavy: { localizations: localizations(o.iosSurchargeHeavy) },
        'push.order.no_cleaner_refunded.body': { localizations: localizations(o.iosNoShowBody) },
        booking_trust_insured: { localizations: localizations(o.iosInsured) },
        booking_trust_insured_no_figure: { localizations: localizations(o.iosInsuredNoFigure) },
        help_faq_a3: { localizations: localizations(o.iosFaq) },
        help_faq_a3_no_figure: { localizations: localizations(o.iosFaqNoFigure) },
        ...Object.fromEntries(
          mapped('ios-locale').map((r) => [`order_cancelled_reason_${r}`, { localizations: localizations(`Reason ${r}`) }]),
        ),
        ...(o.iosSeasonal === null ? {} : { home_seasonal_subtitle: { localizations: localizations(o.iosSeasonal) } }),
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

// ─── 1a. The grace after booking is stated from the policy on every client ──
scenario(
  'a web Plus row that promises 30 minutes is caught',
  { webGraceWith: '30 minutes' },
  { code: 1, mentions: ['pages.plus.row_grace_with', 'does not state 60 minutes'] },
);
scenario(
  'an Android grace note that drops the Plus figure is caught',
  { androidGraceNote: 'Within 15 minutes of booking you pay nothing.' },
  { code: 1, mentions: ['booking_cancel_grace_note', 'does not state 60 minutes'] },
);
scenario(
  'an iOS perk that states the old first-time 60 for everyone is caught',
  { iosPerkGrace: 'Cancel free within 60 minutes of booking' },
  { code: 1, mentions: ['membership_perk_grace_desc', 'does not state 15 minutes'] },
);
scenario(
  'a policy change to the Plus grace flags every surface that still states the old figure',
  { gracePlus: '90' },
  { code: 1, mentions: ['web/en', 'android/values', 'ios/en', 'does not state 90 minutes'] },
);
// The first booking gets 60 minutes since the 2026-09-28 ruling; the copy before it gave a
// first-time customer the standard 15 and named only Plus. The comparison row states just the two
// figures, so there the old copy loses a figure.
scenario(
  'a web comparison row that gives a first booking only 15 minutes again is caught',
  { webGraceWithout: '15 minutes' },
  { code: 1, mentions: ['pages.plus.row_grace_without', 'does not state 60 minutes'] },
);
// Everywhere else the old copy keeps every figure — its "60 with Cleansia Plus" is the first-booking
// figure too while the two graces are equal — and loses only the first-booking clause. These are the
// exact strings phase 1 replaced (P1-W01, P1-A02, P1-I01).
for (const [where, override] of [
  ['web/en — pages.home.rules.rethink_desc', { webRethinkDesc: 'Until someone accepts the job, cancelling costs nothing however close the clean is. Once someone has, you still have 15 minutes from when you booked — 60 minutes with Cleansia Plus.' }],
  ['web/en — pages.order.cancel_policy_note', { webCancelPolicyNote: 'Free cancellation up to 24 hours ahead. Within 15 minutes of booking you pay nothing — within 60 minutes with Cleansia Plus.' }],
  ['android/values — booking_cancel_grace_note', { androidGraceNote: 'Within 15 minutes of booking you pay nothing — within 60 minutes with Cleansia Plus.' }],
  ['android/values — help_faq_a1', { androidFaqA1: 'Open your booking from the Orders tab and tap \\"Cancel\\". Cancellations free of charge up to 24 hours before the cleaning start time. Within 15 minutes of booking you pay nothing — within 60 minutes with Cleansia Plus.' }],
  ['ios/en — booking_cancel_grace_note', { iosGraceNote: 'Within 15 minutes of booking you pay nothing — within 60 minutes with Cleansia Plus.' }],
  ['ios/en — help_faq_a1', { iosFaqA1: 'Open your booking from the Orders tab and tap "Cancel". Cancellations are free up to 24 hours before the cleaning start time. Within 15 minutes of booking you pay nothing — within 60 minutes with Cleansia Plus.' }],
]) {
  scenario(
    `${where} back at the copy before the 2026-09-28 ruling is caught`,
    override,
    { code: 1, mentions: [where, 'no longer names the first-booking grace'], silentAbout: ['does not state'] },
  );
}
scenario(
  'a grace sentence stating a first-booking figure the policy does not hold is caught',
  { iosGraceNote: 'Within 15 minutes of booking you pay nothing — 30 on your first booking, 60 with Cleansia Plus.' },
  { code: 1, mentions: ['ios/en — booking_cancel_grace_note', 'states 30'] },
);
scenario(
  'a policy change to the first-booking grace flags every sentence that names a first booking',
  { graceFirstBooking: '30' },
  {
    code: 1,
    mentions: [
      'does not state 30 minutes',
      'pages.home.rules.rethink_desc',
      'pages.order.cancel_policy_note',
      'pages.plus.row_grace_without',
      'android/values',
      'ios/en',
      'booking_cancel_grace_note',
      'help_faq_a1',
    ],
    // The Plus perks name the standard and Plus figures only.
    silentAbout: ['membership_perk_grace_desc', 'pages.membership.perk_grace', 'pages.home.plus.perk_grace', 'pages.plus.row_grace_with ='],
  },
);
for (const [where, override] of [
  ['web/en — pages.home.rules.rethink_desc', { webRethinkDesc: 'Once someone has, cancelling costs a fee.' }],
  ['web/en — pages.home.plus.perk_grace', { webHomePlusPerkGrace: 'Cancel free after every booking' }],
  ['web/en — pages.order.cancel_policy_note', { webCancelPolicyNote: 'Free cancellation up to 24 hours ahead.' }],
  ['web/en — pages.membership.perk_grace', { webMembershipPerkGrace: 'Cancel free after every booking' }],
  ['web/en — pages.plus.perk_cancel_body', { webPlusPerkCancelBody: 'Without membership the line is 24 hours.' }],
  ['android/values — help_faq_a1', { androidFaqA1: 'Cancellations free of charge up to 24 hours before.' }],
  ['ios/en — help_faq_a1', { iosFaqA1: 'Cancellations are free up to 24 hours before.' }],
]) {
  scenario(
    `${where} that no longer states the grace is caught`,
    override,
    { code: 1, mentions: [where, 'does not state 15 minutes'] },
  );
}
scenario(
  'a BookingPolicy that no longer declares OopsWindowMinutesFirstBooking is a finding',
  { graceFirstBooking: null },
  { code: 1, mentions: ['could not read `OopsWindowMinutesFirstBooking`'] },
);

// ─── 1b. Every platform cancellation reason renders on the three clients ────
// A key the server writes that a client cannot turn into a sentence reaches the customer as
// silence, and the checker carries no allow-list entry for it.
scenario('a reason mapped and localised everywhere passes', { reasons: ['company_wind_down'] }, { code: 0 });
scenario(
  'the reason of the unfilled-order sweep must render like the others',
  { reasons: ['no_cleaner_available'], reasonMissingOn: 'web' },
  { code: 1, mentions: ['no_cleaner_available'] },
);
for (const surface of ['web', 'android', 'ios', 'web-locale', 'android-locale', 'ios-locale']) {
  scenario(
    `a reason the ${surface} surface does not render fails`,
    { reasons: ['company_wind_down'], reasonMissingOn: surface },
    { code: 1, mentions: ['company_wind_down'] },
  );
}

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

// ─── 2b. Money figures in copy come from the market (ADR-0060 D4) ─────────────────────
// The credit and the insurance ceiling are data now; the checker pins the SHAPE of the copy — the
// placeholder is there, no figure is baked in beside it, no currency word rides along.
scenario(
  'catches a literal figure creeping back into the home page credit line',
  { webWeCancelValue: 'Everything back + 250 CZK credit' },
  { code: 1, mentions: ['web/en', 'does not carry the {{amount}} placeholder', 'bakes a figure in', 'names a currency'] },
);
scenario(
  'catches an Android push quoting the apology amount again',
  { androidNoShowBody: 'We refunded booking #%1$s and added 250 Kč credit.' },
  { code: 1, mentions: ['android/en', 'does not carry the %2$s placeholder', 'bakes a figure in (250)', 'names a currency'] },
);
scenario(
  'catches an iOS push quoting the apology amount again',
  { iosNoShowBody: 'We refunded booking #%1$@ and added 250 Kč credit.' },
  { code: 1, mentions: ['ios/en', 'bakes a figure in (250)'] },
);
scenario(
  'does not mistake a loc-arg slot for a figure',
  { androidNoShowBody: 'Booking #%1$s was refunded, with %2$s credit for next time.', iosNoShowBody: 'Booking #%1$@ was refunded, with %2$@ credit for next time.' },
  { code: 0 },
);
scenario(
  'catches an insurance claim with the ceiling baked in',
  { androidInsured: 'Insured up to 1 000 000 Kč', iosFaq: 'Covered by insurance up to 1,000,000 CZK per booking.' },
  { code: 1, mentions: ['android/en', 'booking_trust_insured', 'ios/en', 'help_faq_a3'] },
);
scenario(
  'catches the deleted seasonal card coming back',
  { androidSeasonal: 'Window + upholstery combo — +450 CZK this month' },
  { code: 1, mentions: ['android/en', 'home_seasonal_subtitle is back'] },
);
// The desc line beside the value explains WHO qualifies and carries no number on purpose; asserting
// on it would have made the gate cry wolf on honest copy, which the header calls the worse failure.
scenario(
  'says nothing about the we_cancel_desc line, which quotes no amount',
  {},
  { code: 0, silentAbout: ['we_cancel_desc'] },
);

// ─── 2c. The legal seed carries the market placeholders and no baked money (ADR-0060 D4) ────
// The legal texts are seed markdown now, one folder per effective date; the version the server stamps
// on a consent is that folder's date, so nothing is pinned between a constant and a locale file any
// more. What the gate still holds is the copy's SHAPE, in the newest version only — an older version
// is immutable and past changing — across every language file it carries.
scenario(
  'catches a terms seed that names a currency instead of the placeholder',
  { seedByFile: { 'terms-of-service/en': 'Prices are displayed in CZK and are the final amount payable.' } },
  {
    code: 1,
    mentions: ['terms-of-service/any/2026-09-14/en.md', 'does not carry the {{currency}} placeholder', 'names a currency'],
    silentAbout: ['/cs.md', '/sk.md', '/ru.md', '/uk.md', 'privacy-policy/'],
  },
);
// It is the "Kč" that fires here, not the figure: a paragraph with no placeholder is not read for
// baked amounts (a legal text states hours, percentages and a phone number on purpose), so the
// ceiling alone, with no currency word, would pass. That residual is pinned, not implied.
scenario(
  'catches a currency word in one language of the privacy seed',
  { seedByFile: { 'privacy-policy/cs': 'Vaše soukromí.\n\n## Pojištění\n\nPojištěno do výše 1 000 000 Kč na zakázku.' } },
  {
    code: 1,
    mentions: ['privacy-policy/any/2026-09-14/cs.md', 'names a currency'],
    silentAbout: ['bakes a figure in', '/en.md', 'terms-of-service/'],
  },
);
// The contract for work binds the price through the order's snapshot; a figure pasted into the text
// would outlive the market's price and contradict the record.
scenario(
  'catches a baked price in one language of the work-contract seed',
  { seedByFile: { 'work-contract/cs': 'Smlouva o dílo.\n\n## Cena\n\nCena díla je {{currency}} 1000 za úklid.' } },
  {
    code: 1,
    mentions: ['work-contract/any/2026-09-14/cs.md', 'bakes a figure in'],
    silentAbout: ['/en.md', 'terms-of-service/', 'privacy-policy/'],
  },
);
scenario(
  'catches a figure baked in beside the placeholder',
  { seedByFile: { 'terms-of-service/uk': 'Ціни вказані в {{currency}}, мінімальне замовлення 500.' } },
  { code: 1, mentions: ['terms-of-service/any/2026-09-14/uk.md', 'bakes a figure in (500)'] },
);
scenario(
  'reads only the newest version — an older, immutable text is past changing',
  { seedOtherVersions: { '2026-01-01': { 'terms-of-service': 'Prices are displayed in CZK.' } } },
  { code: 0 },
);
scenario(
  'orders the versions by date, not by the directory listing',
  { seedOtherVersions: { '2026-12-01': { 'terms-of-service': 'Prices are displayed in CZK.' } } },
  { code: 1, mentions: ['terms-of-service/any/2026-12-01/en.md', 'names a currency'], silentAbout: ['2026-09-14'] },
);
scenario(
  'a language file missing from the newest version is a finding, not a silent pass',
  { seedLanguages: LOCALES.filter((l) => l !== 'sk') },
  {
    code: 1,
    mentions: ['terms-of-service/any/2026-09-14/sk.md — is missing', 'privacy-policy/any/2026-09-14/sk.md — is missing'],
    silentAbout: ['/en.md', '/cs.md'],
  },
);
scenario(
  'a seed tree with no dated version is a finding, not a crash',
  { seedPresent: false },
  { code: 1, mentions: ['terms-of-service/any', 'has no dated version folder'] },
);
// The policy figures and the contact number are integers a legal text states on purpose; flagging
// them would make the gate cry wolf on every honest seed file.
scenario(
  'does not mistake the policy percentages, hours or a phone number for a baked amount',
  {},
  { code: 0, silentAbout: ['bakes a figure in'] },
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

// ─── 4b. The dirtiness rates are read from BookingPolicy and pinned in the copy ─────────────
// The web states them through `{{rate}}` from its shared mirror; Android and iOS state them as
// literals on the level chip and on the surcharge line. Strictly: the rate the key belongs to, and
// no other.
scenario(
  'states the dirtiness rates BookingPolicy holds',
  {},
  { code: 0, mentions: ['dirtiness +30%/+60%'] },
);
scenario(
  'moving a dirtiness rate in C# fails every surface still quoting the old one',
  { increasedDirtinessRate: '0.35m' },
  {
    code: 1,
    mentions: [
      'INCREASED_DIRTINESS_SURCHARGE_RATE',
      'android/values — dirtiness_increased_price',
      'android/values — dirtiness_surcharge_increased',
      'ios/en — booking_dirtiness_increased_rate',
      'ios/en — booking_dirtiness_surcharge_increased',
      'IncreasedDirtinessSurchargeRate is 35%',
    ],
    silentAbout: ['HEAVY_DIRTINESS_SURCHARGE_RATE', 'dirtiness_heavy_price', 'surcharge_heavy'],
  },
);
scenario(
  'catches the web mirror drifting from a dirtiness rate',
  { tsHeavyDirtinessRate: '0.5' },
  { code: 1, mentions: ['HEAVY_DIRTINESS_SURCHARGE_RATE', 'is 0.5'] },
);
scenario(
  'catches an Android increased chip showing the heavy rate',
  { androidIncreasedPrice: '+60%' },
  { code: 1, mentions: ['dirtiness_increased_price', 'states 60%', 'is 30%'] },
);
scenario(
  'catches an iOS surcharge line quoting a stale rate beside the right one',
  { iosSurchargeHeavy: 'Heavy dirtiness surcharge (+60%, was +50%)' },
  { code: 1, mentions: ['booking_dirtiness_surcharge_heavy', 'states 60%, 50%'] },
);
scenario(
  'catches a rate chip that states no rate at all',
  { iosIncreasedRate: 'Surcharge' },
  { code: 1, mentions: ['booking_dirtiness_increased_rate', 'states no rate'] },
);
scenario(
  'catches the web level chip baking a rate in instead of the placeholder',
  { webDirtinessSurcharge: '+30%' },
  { code: 1, mentions: ['pages.order.dirtiness.surcharge', 'does not carry the {{rate}} placeholder', 'bakes a rate in'] },
);
for (const [name, override] of [
  ['IncreasedDirtinessSurchargeRate', { increasedDirtinessRate: null }],
  ['HeavyDirtinessSurchargeRate', { heavyDirtinessRate: null }],
]) {
  scenario(
    `a BookingPolicy that no longer declares ${name} is a finding`,
    override,
    { code: 1, mentions: [`could not read \`${name}\``] },
  );
}

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
  'a space before the percent sign in the dirtiness copy is the same rate',
  {
    androidIncreasedPrice: '+30 %',
    iosSurchargeHeavy: 'Příplatek za silné znečištění (+60 %)',
    webDirtinessSurcharge: '+{{rate}} %',
  },
  { code: 0 },
);
// Each locale names a first booking with its own words and case ending; the shipped phrasing of
// every one must read as a first-booking clause.
scenario(
  'the first-booking clause is recognised in every locale',
  {
    webRethinkDesc: 'Jakmile ji někdo přijme, máte pořád 15 minut od objednání — u první objednávky nebo s Cleansia Plus 60 minut.',
    webGraceWithout: '15 minút (pri prvej objednávke 60)',
    webCancelPolicyNote: 'Бесплатная отмена за 24 часа. В течение 15 минут после заказа вы не платите ничего — для первого заказа или с Cleansia Plus в течение 60 минут.',
    androidGraceNote: 'Протягом 15 хвилин після замовлення ви не платите нічого — для першого замовлення або з Cleansia Plus протягом 60 хвилин.',
  },
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
