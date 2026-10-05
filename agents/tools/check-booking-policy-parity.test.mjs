#!/usr/bin/env node
/**
 * Self-test for the booking-policy parity gate (`check-booking-policy-parity.mjs`).
 *
 * It never touches the working tree: every scenario materialises a fixture repository under a
 * throwaway directory — a BookingPolicy.cs, the web's shared model and its two size pickers, five web
 * locale files, five Android string files, the two iOS catalogs, the two mobile `PropertySize` files
 * and the legal seed's markdown files — and runs the tool against it with `--root=`.
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

/** §3b — the web's referral rows and their twins, as shipped. */
const WEB_REFERRAL = {
  'auth.register.referral.dialog_helper': "Have a friend's code? You get {{amount}} in credit after your first cleaning.",
  'auth.register.referral.dialog_helper_no_amount': "Have a friend's code? Enter it here.",
  'auth.register.referral.dialog_success': "Code accepted — you'll get {{amount}} in credit after your first cleaning.",
  'auth.register.referral.dialog_success_no_amount': 'Code accepted.',
  'auth.register.referral.dialog_success_named': "Code from {{name}} accepted — you'll get {{amount}} in credit after your first cleaning.",
  'auth.register.referral.dialog_success_named_no_amount': 'Code from {{name}} accepted.',
  'pages.rewards.referral.section_title': 'Invite friends — get {{amount}} in credit',
  'pages.rewards.referral.section_title_no_amount': 'Invite friends',
  'pages.rewards.referral.subtitle': 'Share your code. When a friend finishes their first cleaning, you get {{amount}} in credit towards your next bookings, and they get credit where their market offers it.',
  'pages.rewards.referral.subtitle_no_amount': 'Share your code with friends who could use a cleaning.',
};
/**
 * §3b — Android's referral rows, twins, heading and invite, as shipped; iOS carries the same keys with
 * `%@` slots. Each row states the reader's own figure, and the figured invite `loyalty_referral_share_text`
 * is retired on both platforms, so the fixture leaves it out.
 */
const ANDROID_REFERRAL = {
  home_upsell_referral_desc: 'You get %1$s credit once their first cleaning is completed, and they get credit where their market offers it.',
  home_upsell_referral_desc_generic: 'Send a friend your code to enter when they sign up.',
  booking_referral_code_dialog_helper: "Have a friend\\'s code? You get %1$s credit after your first completed cleaning.",
  booking_referral_code_dialog_helper_no_figure: "Have a friend\\'s code? Enter it here.",
  booking_referral_code_dialog_success: "Code accepted — you\\'ll get %1$s credit after your first completed cleaning.",
  booking_referral_code_dialog_success_no_figure: 'Code accepted.',
  booking_referral_code_dialog_success_named: "Code from %1$s accepted — you\\'ll get %2$s credit after your first completed cleaning.",
  booking_referral_code_dialog_success_named_no_figure: 'Code from %1$s accepted.',
  loyalty_referral_subtitle: "Share your code. When a friend\\'s first cleaning is completed, you get %1$s credit, and they get credit where their market offers it.",
  loyalty_referral_subtitle_no_figure: 'Share your code with friends to enter when they sign up.',
  loyalty_referral_share_text_no_figure: 'Join me on Cleansia! Use my code %1$s at signup: %2$s',
  loyalty_referral_section_title: 'Invite friends',
};
const toIos = (value) => value.replace(/\$s/g, '$@').replace(/\\'/g, "'");

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
    /** The no-figure insurance claims, retired on 2026-10-04; `null` leaves the key out, as shipped. */
    androidInsuredNoFigure: null,
    androidFaq: 'Covered by insurance up to %1$s per booking.',
    androidFaqNoFigure: null,
    androidHomeInsured: null,
    androidSeasonal: null,
    iosNoShowBody: 'Nobody could take booking #%1$@, so we refunded it and added %2$@ credit towards your next clean.',
    iosInsured: 'Insured up to %1$@',
    iosInsuredNoFigure: null,
    iosFaq: 'Covered by insurance up to %1$@ per booking.',
    iosFaqNoFigure: null,
    iosHomeInsured: null,
    iosSeasonal: null,
    // The platform's cancellation reasons: every declared key is mapped and localised on the three
    // clients unless the scenario drops one surface.
    reasons: ['payment_not_completed', 'company_wind_down', 'no_cleaner_available'],
    reasonMissingOn: null,
    // Declared by the server and rendered by no client yet: the checker's own not-yet-rendered list.
    unrenderedReasons: ['customer_lockout'],
    // The largest home (§5). `null` leaves the constant, the key or the file out of the fixture.
    maxRooms: '8',
    maxBathrooms: '4',
    iosMaxRooms: '8',
    iosMaxBathrooms: '4',
    androidMaxRooms: '8',
    androidMaxBathrooms: '4',
    webRoomChoices: '[1, 2, 3, 4, 5, 6, 7, 8]',
    webBathroomChoices: '[1, 2, 3, 4]',
    recurringRoomChoices: '[1, 2, 3, 4, 5, 6, 7, 8]',
    recurringBathroomChoices: '[1, 2, 3, 4]',
    webSizeRefusal: 'A booking can include up to 8 rooms and 4 bathrooms.',
    androidSizeRefusal: 'A booking can include up to 8 rooms and 4 bathrooms.',
    iosCoreSizeRefusal: 'A booking can include up to 8 rooms and 4 bathrooms.',
    androidSizeCaption: 'Up to %1$d rooms and %2$d bathrooms',
    iosSizeCaption: 'Up to %1$lld rooms and %2$lld bathrooms',
    iosCorePresent: true,
    /** §3b — per-key changes over the shipped referral copy; a key set to `null` is left out. */
    webReferralPatch: {},
    androidReferralPatch: {},
    iosReferralPatch: {},
    ...overrides,
  };

  const webReferral = { ...WEB_REFERRAL, ...o.webReferralPatch };
  const androidReferral = { ...ANDROID_REFERRAL, ...o.androidReferralPatch };
  const iosReferral = {
    ...Object.fromEntries(Object.entries(ANDROID_REFERRAL).map(([key, value]) => [key, toIos(value)])),
    ...o.iosReferralPatch,
  };
  /** Places each dotted web key into the locale bundle. */
  const withWebReferral = (bundle) => {
    for (const [dotted, value] of Object.entries(webReferral)) {
      if (value === null) continue;
      const parts = dotted.split('.');
      let node = bundle;
      for (const part of parts.slice(0, -1)) node = node[part] ??= {};
      node[parts[parts.length - 1]] = value;
    }
    return bundle;
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
${o.maxRooms === null ? '' : `    public const int MaxRooms = ${o.maxRooms};`}
${o.maxBathrooms === null ? '' : `    public const int MaxBathrooms = ${o.maxBathrooms};`}
}
`);

  write(root, 'src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/PropertySize.swift', `
enum PropertySize {
    static let maxRooms = ${o.iosMaxRooms}
    static let maxBathrooms = ${o.iosMaxBathrooms}
}
`);
  write(root, 'src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/core/booking/PropertySize.kt', `
object PropertySize {
    const val MAX_ROOMS = ${o.androidMaxRooms}
    const val MAX_BATHROOMS = ${o.androidMaxBathrooms}
}
`);
  write(root, 'src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.component.ts', `
  readonly roomChoices = ${o.webRoomChoices};
  readonly bathroomChoices = ${o.webBathroomChoices};
`);
  write(root, 'src/Cleansia.App/libs/cleansia-customer-features/recurring-bookings/src/lib/create-recurring-wizard/create-recurring-wizard.component.ts', `
  protected readonly roomChoices = ${o.recurringRoomChoices};
  protected readonly bathroomChoices = ${o.recurringBathroomChoices};
`);

  if (o.seedPresent) {
    const audience = { 'terms-of-service': 'customer', 'privacy-policy': 'customer', 'work-contract': 'employee' };
    const seedFile = (type, version, lang, body) =>
      write(
        root,
        `src/Cleansia.Infra.Database/Seed/Legal/${audience[type]}/${type}/any/${version}/${lang}.md`,
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

  const reasonConsts = [...o.reasons, ...o.unrenderedReasons]
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
      JSON.stringify(withWebReferral({
        api: {
          order: { size_exceeds_maximum: o.webSizeRefusal },
        },
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
      }), null, 2),
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
${o.androidInsuredNoFigure === null ? '' : `    <string name="booking_trust_insured_no_figure">${o.androidInsuredNoFigure}</string>\n`}    <string name="help_faq_a3">${o.androidFaq}</string>
${o.androidFaqNoFigure === null ? '' : `    <string name="help_faq_a3_no_figure">${o.androidFaqNoFigure}</string>\n`}${o.androidHomeInsured === null ? '' : `    <string name="home_trust_insured">${o.androidHomeInsured}</string>\n`}    <string name="error_order_size_exceeds_maximum">${o.androidSizeRefusal}</string>
${o.androidSizeCaption === null ? '' : `    <string name="booking_size_limit_caption">${o.androidSizeCaption}</string>\n`}${mapped('android-locale').map((r) => `    <string name="order_cancelled_reason_${r}">Reason ${r}</string>\n`).join('')}${o.androidSeasonal === null ? '' : `    <string name="home_seasonal_subtitle">${o.androidSeasonal}</string>\n`}${Object.entries(androidReferral).filter(([, value]) => value !== null).map(([key, value]) => `    <string name="${key}">${value}</string>\n`).join('')}</resources>`,
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
        ...(o.iosInsuredNoFigure === null ? {} : { booking_trust_insured_no_figure: { localizations: localizations(o.iosInsuredNoFigure) } }),
        help_faq_a3: { localizations: localizations(o.iosFaq) },
        ...(o.iosFaqNoFigure === null ? {} : { help_faq_a3_no_figure: { localizations: localizations(o.iosFaqNoFigure) } }),
        ...(o.iosHomeInsured === null ? {} : { home_trust_insured: { localizations: localizations(o.iosHomeInsured) } }),
        booking_size_limit_caption: { localizations: localizations(o.iosSizeCaption) },
        ...Object.fromEntries(
          mapped('ios-locale').map((r) => [`order_cancelled_reason_${r}`, { localizations: localizations(`Reason ${r}`) }]),
        ),
        ...(o.iosSeasonal === null ? {} : { home_seasonal_subtitle: { localizations: localizations(o.iosSeasonal) } }),
        ...Object.fromEntries(
          Object.entries(iosReferral)
            .filter(([, value]) => value !== null)
            .map(([key, value]) => [key, { localizations: localizations(value) }]),
        ),
      },
    }, null, 2),
  );
  if (o.iosCorePresent) {
    write(
      root,
      'src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings',
      JSON.stringify({
        strings: {
          'error.order.size_exceeds_maximum': { localizations: localizations(o.iosCoreSizeRefusal) },
        },
      }, null, 2),
    );
  }

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
scenario(
  'the not-yet-rendered list may not name a reason the server no longer declares',
  { unrenderedReasons: [] },
  { code: 1, mentions: ['order.cancelled.customer_lockout is on the not-yet-rendered list but no longer declared'] },
);

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
  {
    code: 1,
    mentions: ['android/en — booking_trust_insured = ', 'ios/en — help_faq_a3 = '],
    silentAbout: ['_no_figure'],
  },
);
// Insurance is optional since 2026-10-04 and no market authors a ceiling, so the claims that stated
// none (the home trust strip's "Insured", the confirm badge's "Insured", "Covered by insurance.")
// were deleted on both platforms. A tree without them passes (scenario 1); any one back, on either
// platform, is the promise coming back.
const retiredInsuranceClaims = ['home_trust_insured', 'booking_trust_insured_no_figure', 'help_faq_a3_no_figure'];
for (const [platform, key, override] of [
  ['android', 'home_trust_insured', { androidHomeInsured: 'Insured' }],
  ['android', 'booking_trust_insured_no_figure', { androidInsuredNoFigure: 'Insured' }],
  ['android', 'help_faq_a3_no_figure', { androidFaqNoFigure: 'Covered by insurance.' }],
  ['ios', 'home_trust_insured', { iosHomeInsured: 'Insured' }],
  ['ios', 'booking_trust_insured_no_figure', { iosInsuredNoFigure: 'Insured' }],
  ['ios', 'help_faq_a3_no_figure', { iosFaqNoFigure: 'Covered by insurance.' }],
]) {
  scenario(
    `catches the no-figure insurance claim ${key} coming back on ${platform}`,
    override,
    {
      code: 1,
      mentions: [`${platform}/en — ${key} is back`, `${platform}/uk — ${key} is back`, 'insurance is optional'],
      silentAbout: [...retiredInsuranceClaims.filter((k) => k !== key), `${platform === 'ios' ? 'android' : 'ios'}/`],
    },
  );
}
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

// ─── 2b'. The referral reward is the market's credit, never a figure or points (owner ruling 2026-10-04) ────
// Until the ruling a referral paid 150 tier points, and every client said so as a literal. It pays
// `Currency.ReferralCredit` now: each row carries the credit's slot, and the twin a market with no figure
// reads promises nothing. Every drift case below exits 0 under a checker without §3b.
//
// Since the owner ruling of 2026-10-05 each side is paid in the currency it books in, so the two figures
// can differ: a row states only the reader's own, and the mobile invite — read by the friend, in their
// own market — names no figure at all. Those scenarios exit 0 under the checker of 2026-10-04.
scenario(
  'states that the referral rows are pinned',
  {},
  {
    code: 0,
    mentions: [
      "the referral credit comes from the market on 5 web and 5 mobile row(s), each with its no-figure twin and the reader's figure only",
      'the mobile invite names no figure',
    ],
  },
);
scenario(
  'catches a web row promising one figure to both sides again',
  { webReferralPatch: { 'auth.register.referral.dialog_helper': "Have a friend's code? You each get {{amount}} in credit after your first cleaning." } },
  {
    code: 1,
    mentions: ['web/en — auth.register.referral.dialog_helper = ', 'promises one figure to both sides'],
    silentAbout: ['does not carry the {{amount}} placeholder', 'android/', 'ios/'],
  },
);
scenario(
  'reads a shared-figure claim in its own locale',
  { androidReferralPatch: { loyalty_referral_subtitle: 'Sdílejte svůj kód. Po prvním dokončeném úklidu kamaráda oba získáte kredit %1$s.' } },
  {
    code: 1,
    mentions: ['android/cs — loyalty_referral_subtitle = ', 'promises one figure to both sides'],
    silentAbout: ['android/en —', 'android/sk —', 'android/uk —', 'android/ru —', 'ios/'],
  },
);
// The old Ukrainian and Russian subtitles said "you will each get" with a distributive "по" before the
// figure and no "each" word at all; the platform lexicons of 2026-10-05 let it through on iOS.
scenario(
  'catches the distributive "по" before the credit slot in Ukrainian and Russian',
  { iosReferralPatch: { loyalty_referral_subtitle: 'Друг і ви отримаєте по %1$@ кредиту після його першого завершеного прибирання.' } },
  {
    code: 1,
    mentions: ['ios/uk — loyalty_referral_subtitle = ', 'ios/ru — loyalty_referral_subtitle = ', 'promises one figure to both sides'],
    silentAbout: ['ios/en —', 'ios/cs —', 'ios/sk —', 'android/'],
  },
);
scenario(
  'does not read a "по" away from the credit slot as a shared figure',
  { webReferralPatch: { 'pages.rewards.referral.subtitle': 'Надішліть код по телефону — ви отримаєте кредит {{amount}}, а друг теж отримає кредит.' } },
  { code: 0 },
);
scenario(
  'catches the figured invite coming back on Android',
  { androidReferralPatch: { loyalty_referral_share_text: 'Get %1$s credit after your first completed Cleansia cleaning! Use my code %2$s at signup: %3$s' } },
  {
    code: 1,
    mentions: ['android/en — loyalty_referral_share_text is back', 'android/uk — loyalty_referral_share_text is back', 'names no figure'],
    silentAbout: ['ios/'],
  },
);
scenario(
  'catches the figured invite coming back on iOS',
  { iosReferralPatch: { loyalty_referral_share_text: 'Get %1$@ credit after your first completed Cleansia cleaning! Use my code %2$@ at signup: %3$@' } },
  {
    code: 1,
    mentions: ['ios/en — loyalty_referral_share_text is back', 'ios/ru — loyalty_referral_share_text is back'],
    silentAbout: ['android/'],
  },
);
scenario(
  'catches the invite that is sent everywhere naming a figure',
  { androidReferralPatch: { loyalty_referral_share_text_no_figure: 'Get 150 Kč credit after your first Cleansia cleaning! Use my code %1$s at signup: %2$s' } },
  {
    code: 1,
    mentions: [
      'android/en — loyalty_referral_share_text_no_figure = ',
      'bakes a figure in (150)',
      'names a currency',
      'promises credit',
    ],
    silentAbout: ['ios/', 'web/'],
  },
);
scenario(
  'an invite that is gone is a finding, not a silent pass',
  { iosReferralPatch: { loyalty_referral_share_text_no_figure: null } },
  {
    code: 1,
    mentions: ['ios/en — loyalty_referral_share_text_no_figure is missing', 'ios/uk — loyalty_referral_share_text_no_figure is missing'],
    silentAbout: ['android/'],
  },
);
scenario(
  'catches the web sign-up dialog promising 150 bonus points again',
  { webReferralPatch: { 'auth.register.referral.dialog_helper': "Have a friend's code? You'll both get 150 bonus points after your first cleaning." } },
  {
    code: 1,
    mentions: [
      'web/en — auth.register.referral.dialog_helper = ',
      'does not carry the {{amount}} placeholder',
      'bakes a figure in (150)',
      'promises points',
    ],
    silentAbout: ['android/', 'ios/'],
  },
);
scenario(
  'catches a web referral row that names the currency beside its slot',
  { webReferralPatch: { 'pages.rewards.referral.section_title': 'Invite friends — {{amount}} CZK in credit each' } },
  {
    code: 1,
    mentions: ['web/en — pages.rewards.referral.section_title = ', 'names a currency'],
    silentAbout: ['does not carry the {{amount}} placeholder'],
  },
);
scenario(
  'reads a referral row for points in its own locale',
  { webReferralPatch: { 'pages.rewards.referral.subtitle': 'Sdílejte kód — {{amount}} a body navíc.' } },
  {
    code: 1,
    mentions: ['web/cs — pages.rewards.referral.subtitle = ', 'web/sk — pages.rewards.referral.subtitle = ', 'promises points'],
    silentAbout: ['web/en —', 'web/ru —', 'web/uk —'],
  },
);
scenario(
  'a web twin missing from the locale files is a finding, not a silent pass',
  { webReferralPatch: { 'pages.rewards.referral.subtitle_no_amount': null } },
  {
    code: 1,
    mentions: [
      'web/en — pages.rewards.referral.subtitle_no_amount is missing',
      'web/uk — pages.rewards.referral.subtitle_no_amount is missing',
    ],
  },
);
scenario(
  'catches a web twin that still promises the credit',
  { webReferralPatch: { 'auth.register.referral.dialog_success_no_amount': 'Code accepted — your credit follows your first cleaning.' } },
  { code: 1, mentions: ['web/en — auth.register.referral.dialog_success_no_amount = ', 'promises credit'] },
);
scenario(
  'catches a web twin that renders the amount',
  { webReferralPatch: { 'pages.rewards.referral.section_title_no_amount': 'Invite friends — {{amount}} each' } },
  { code: 1, mentions: ['web/en — pages.rewards.referral.section_title_no_amount = ', 'carries the {{amount}} slot'] },
);
scenario(
  'catches the Android named success taking the credit in the name slot',
  { androidReferralPatch: { booking_referral_code_dialog_success_named: "Code accepted — you\\'ll both get %1$s credit after your first completed cleaning." } },
  { code: 1, mentions: ['android/en — booking_referral_code_dialog_success_named = ', 'does not carry the %2$s placeholder'] },
);
scenario(
  'catches an Android twin that promises points',
  { androidReferralPatch: { loyalty_referral_subtitle_no_figure: 'Share your code and collect points when friends sign up.' } },
  { code: 1, mentions: ['android/en — loyalty_referral_subtitle_no_figure = ', 'promises points'], silentAbout: ['ios/'] },
);
scenario(
  'catches the iOS heading that promised 150 points each',
  { iosReferralPatch: { loyalty_referral_section_title: 'Invite friends — earn 150 points each' } },
  {
    code: 1,
    mentions: ['ios/en — loyalty_referral_section_title = ', 'bakes a figure in (150)', 'promises points'],
    silentAbout: ['android/'],
  },
);
scenario(
  'catches an iOS row that states a figure instead of the slot',
  { iosReferralPatch: { home_upsell_referral_desc: 'You both get 150 Kč credit once their first cleaning is completed.' } },
  {
    code: 1,
    mentions: [
      'ios/en — home_upsell_referral_desc = ',
      'does not carry the %1$@ placeholder',
      'bakes a figure in (150)',
      'names a currency',
    ],
  },
);
scenario(
  'an iOS twin that is gone is a finding, not a silent pass',
  { iosReferralPatch: { home_upsell_referral_desc_generic: null } },
  {
    code: 1,
    mentions: ['ios/en — home_upsell_referral_desc_generic is missing', 'ios/uk — home_upsell_referral_desc_generic is missing'],
    silentAbout: ['android/'],
  },
);
scenario(
  'an Android twin that is gone is a finding, not a silent pass',
  { androidReferralPatch: { home_upsell_referral_desc_generic: null } },
  {
    code: 1,
    mentions: ['android/en — home_upsell_referral_desc_generic is missing', 'android/uk — home_upsell_referral_desc_generic is missing'],
    silentAbout: ['ios/'],
  },
);
// Ukrainian points are "бали"; a line that puts the credit on the customer's "баланс" is honest copy.
scenario(
  'does not read a balance as points',
  { webReferralPatch: { 'pages.rewards.referral.subtitle': 'Поділіться кодом — кредит {{amount}} зарахуємо на ваш баланс.' } },
  { code: 0 },
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
// The contract for work binds the price through the seat's snapshot; a figure pasted into the text
// would outlive the reward and contradict the record. It binds the company and the cleaner, so it is
// read under employee/.
scenario(
  'catches a baked price in one language of the work-contract seed',
  { seedByFile: { 'work-contract/cs': 'Smlouva o dílo.\n\n## Cena\n\nCena díla je {{currency}} 1000 za úklid.' } },
  {
    code: 1,
    mentions: ['employee/work-contract/any/2026-09-14/cs.md', 'bakes a figure in'],
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

// ─── 4c. The largest home is read from BookingPolicy and pinned on every client (D11) ─────────
// iOS and Android hold the caps as constants and the web as the last chip of its two pickers; the
// refusal states them as literals in three catalogs, and the mobile caption renders them through two
// placeholders whose noun forms were written for 8 and 4.
scenario(
  'states the size caps BookingPolicy holds',
  {},
  { code: 0, mentions: ['a home of up to 8 rooms and 4 bathrooms'] },
);
scenario(
  'moving MaxRooms in C# fails every surface still holding the old cap',
  { maxRooms: '10' },
  {
    code: 1,
    mentions: [
      'PropertySize.swift — `maxRooms` is 8, BookingPolicy.MaxRooms is 10',
      'PropertySize.kt — `MAX_ROOMS` is 8, BookingPolicy.MaxRooms is 10',
      'order-wizard.component.ts — `roomChoices` stops at 8',
      'create-recurring-wizard.component.ts — `roomChoices` stops at 8',
      'web/en — api.order.size_exceeds_maximum',
      'android/values — error_order_size_exceeds_maximum',
      'ios-core/en — error.order.size_exceeds_maximum',
      'does not state 10',
      'states 8, which is neither',
      'noun forms were written for 8 rooms and 4 bathrooms',
    ],
    silentAbout: ['`maxBathrooms`', '`MAX_BATHROOMS`', '`bathroomChoices`'],
  },
);
scenario(
  'catches the iOS stepper cap drifting on its own',
  { iosMaxBathrooms: '5' },
  {
    code: 1,
    mentions: ['PropertySize.swift — `maxBathrooms` is 5, BookingPolicy.MaxBathrooms is 4'],
    silentAbout: ['PropertySize.kt', 'noun forms'],
  },
);
scenario(
  'catches the Android stepper cap drifting on its own',
  { androidMaxRooms: '9' },
  { code: 1, mentions: ['PropertySize.kt — `MAX_ROOMS` is 9, BookingPolicy.MaxRooms is 8'], silentAbout: ['PropertySize.swift'] },
);
scenario(
  'catches a web picker that stops short of the cap',
  { recurringBathroomChoices: '[1, 2, 3]' },
  {
    code: 1,
    mentions: ['create-recurring-wizard.component.ts — `bathroomChoices` stops at 3, BookingPolicy.MaxBathrooms is 4'],
    silentAbout: ['order-wizard.component.ts'],
  },
);
scenario(
  'catches a refusal that states a stale cap',
  { androidSizeRefusal: 'A booking can include up to 6 rooms and 4 bathrooms.' },
  { code: 1, mentions: ['android/values — error_order_size_exceeds_maximum', 'does not state 8', 'states 6, which is neither'] },
);
scenario(
  'catches a refusal that drops a cap',
  { iosCoreSizeRefusal: 'A booking can include up to 8 rooms.' },
  { code: 1, mentions: ['ios-core/en — error.order.size_exceeds_maximum', 'does not state 4'] },
);
scenario(
  'catches a caption that bakes the caps in',
  { androidSizeCaption: 'Up to 8 rooms and 4 bathrooms', iosSizeCaption: 'Up to 8 rooms and %2$lld bathrooms' },
  {
    code: 1,
    mentions: [
      'android/values — booking_size_limit_caption',
      'does not carry the %1$d placeholder',
      'bakes a figure in (8, 4)',
      'ios/en — booking_size_limit_caption',
      'does not carry the %1$lld placeholder',
      'bakes a figure in (8)',
    ],
  },
);
scenario(
  'a missing caption is a finding, not a silent pass',
  { androidSizeCaption: null },
  { code: 1, mentions: ['android/values — booking_size_limit_caption is missing'] },
);
scenario(
  'a BookingPolicy that no longer declares MaxRooms is a finding',
  { maxRooms: null },
  { code: 1, mentions: ['could not read `MaxRooms`'], silentAbout: ['noun forms'] },
);
scenario(
  'a missing iOS core catalog is a finding, not a crash',
  { iosCorePresent: false },
  { code: 1, mentions: ['CleansiaCore/Resources/Localizable.xcstrings — is missing'] },
);

// ─── 5. It does not cry wolf ────────────────────────────────────────────────
scenario(
  'reads the iOS %lld slots as slots, not as figures',
  { iosSizeCaption: 'Nejvýše %1$lld pokojů a %2$lld koupelny' },
  { code: 0 },
);
scenario(
  'a refusal that names the caps in the other order still states both',
  { webSizeRefusal: 'Не більше 4 ванних кімнат і 8 кімнат в одному замовленні.' },
  { code: 0 },
);
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
