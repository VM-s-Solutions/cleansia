import { existsSync, readdirSync, readFileSync } from 'fs';
import { basename, dirname, join, relative, sep } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const GENERIC_FALLBACK_KEY = 'api.common.error_occurred';

function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const SOLUTION_DIR = findSolutionDir();

const APP_SERVICES_DIR = join(SOLUTION_DIR, 'Cleansia.Core.AppServices');

const BUSINESS_ERROR_MESSAGE_PATH = join(
  APP_SERVICES_DIR,
  'Common/BusinessErrorMessage.cs'
);

const FEATURES_DIR = join(APP_SERVICES_DIR, 'Features');

// The host that serves this app: Cleansia.Web.Customer listens on :5003 and the
// customer dev server proxies /api to it (apps/cleansia.app/proxy.conf.json).
const HOST_CONTROLLERS_DIR = join(
  SOLUTION_DIR,
  'Cleansia.Web.Customer/Controllers'
);

const I18N_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/apps/cleansia.app/src/assets/i18n'
);

function localePath(locale: Locale): string {
  return join(I18N_DIR, `${locale}.json`);
}

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(localePath(locale), 'utf8')) as Record<
    string,
    unknown
  >;
}

function parseBusinessErrorConstants(): Map<string, string> {
  const source = readFileSync(BUSINESS_ERROR_MESSAGE_PATH, 'utf8');
  const constants = new Map<string, string>();
  const regex = /public const string (\w+) = "([^"]+)";/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(source)) !== null) {
    constants.set(match[1], match[2]);
  }
  return constants;
}

function parseBusinessErrorValues(): Set<string> {
  return new Set(parseBusinessErrorConstants().values());
}

function listCsFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) found.push(...listCsFiles(full));
    else if (entry.name.endsWith('.cs')) found.push(full);
  }
  return found;
}

function featureFilesByClassName(): Map<string, string[]> {
  const index = new Map<string, string[]>();
  for (const file of listCsFiles(FEATURES_DIR)) {
    // basename, not lastIndexOf('/'): `file` comes from join(), which uses the platform
    // separator. On Windows that is a backslash, lastIndexOf('/') returns -1, and the key
    // becomes the whole absolute path — so every featureFiles.has(name) lookup missed and
    // this scanner silently resolved nothing. It passed on CI, which is Linux, for as long
    // as it has existed.
    const className = basename(file, '.cs');
    index.set(className, [...(index.get(className) ?? []), file]);
  }
  return index;
}

const MESSAGE_TYPE = '(?:Command|Query|Request)';

// The type a `Mediator.Send(x)` argument was bound to, resolved against the
// declaration nearest ABOVE the call — a parameter (`[FromBody] X.Command x`), a
// construction (`var x = new X.Query(...)`), or a `with` copy of either.
function resolveMessageType(
  source: string,
  before: number,
  identifier: string,
  depth = 0
): string | undefined {
  if (depth > 3) return undefined;
  const head = source.slice(0, before);
  let nearest: string | undefined;
  let nearestAt = -1;
  const declarations = [
    new RegExp(`\\b([A-Z]\\w*)\\.${MESSAGE_TYPE}\\??\\s+${identifier}\\b`, 'g'),
    new RegExp(`\\b${identifier}\\s*=\\s*new\\s+([A-Z]\\w*)\\.${MESSAGE_TYPE}\\b`, 'g'),
  ];
  for (const regex of declarations) {
    let match: RegExpExecArray | null;
    while ((match = regex.exec(head)) !== null) {
      if (match.index > nearestAt) {
        nearestAt = match.index;
        nearest = match[1];
      }
    }
  }
  const withCopy = new RegExp(`\\b${identifier}\\s*=\\s*(\\w+)\\s+with\\b`, 'g');
  let copyAt = -1;
  let copiedFrom: string | undefined;
  let match: RegExpExecArray | null;
  while ((match = withCopy.exec(head)) !== null) {
    if (match.index > copyAt) {
      copyAt = match.index;
      copiedFrom = match[1];
    }
  }
  if (copiedFrom && copyAt > nearestAt) {
    return resolveMessageType(source, copyAt, copiedFrom, depth + 1);
  }
  return nearest;
}

interface DispatchSite {
  controller: string;
  expression: string;
  featureClass?: string;
}

interface HostSurface {
  controllers: number;
  sites: DispatchSite[];
  unresolved: DispatchSite[];
  featureClasses: Set<string>;
  keys: Map<string, Set<string>>;
}

// Walks the controllers of the host that serves this app, resolves what each one
// dispatches to a feature file, and reads the BusinessErrorMessage constants out
// of it. Only what the tree actually contains: a key added to a new endpoint
// shows up here without anyone remembering to paste it into the roster below.
function deriveHostSurface(): HostSurface {
  const featureFiles = featureFilesByClassName();
  const constants = parseBusinessErrorConstants();
  const sites: DispatchSite[] = [];
  const featureClasses = new Set<string>();
  const keys = new Map<string, Set<string>>();
  const controllers = readdirSync(HOST_CONTROLLERS_DIR).filter((f) =>
    f.endsWith('.cs')
  );

  for (const controller of controllers) {
    const source = readFileSync(join(HOST_CONTROLLERS_DIR, controller), 'utf8');
    const dispatched = new Set<string>();

    const send = /Mediator\.Send(?:<[^>]*>)?\(\s*(new\s+)?(\w+)(?:\.(?:Command|Query|Request))?/g;
    let match: RegExpExecArray | null;
    while ((match = send.exec(source)) !== null) {
      const isConstruction = !!match[1];
      const name = isConstruction
        ? match[2]
        : resolveMessageType(source, match.index, match[2]);
      const site: DispatchSite = {
        controller,
        expression: `Mediator.Send(${isConstruction ? 'new ' : ''}${match[2]})`,
        featureClass: name && featureFiles.has(name) ? name : undefined,
      };
      sites.push(site);
      if (site.featureClass) dispatched.add(site.featureClass);
    }

    // MVC binds the message straight onto the action, and HandleResult<X.Response>
    // names the same feature class the send returned; both are dispatches too.
    const bindings = [
      new RegExp(`\\[From\\w+\\]\\s*([A-Z]\\w*)\\.${MESSAGE_TYPE}\\b`, 'g'),
      /HandleResult<\s*([A-Z]\w*)\.(?:Response|Command|Query|Request)\s*>/g,
    ];
    for (const regex of bindings) {
      while ((match = regex.exec(source)) !== null) {
        if (featureFiles.has(match[1])) dispatched.add(match[1]);
      }
    }

    for (const name of dispatched) {
      featureClasses.add(name);
      for (const file of featureFiles.get(name) ?? []) {
        const emitted = /BusinessErrorMessage\.(\w+)/g;
        const fileSource = readFileSync(file, 'utf8');
        while ((match = emitted.exec(fileSource)) !== null) {
          const value = constants.get(match[1]);
          if (!value) continue;
          // Forward slashes always. This string is an identity compared against literals in the
          // assertions below, so it must not carry the platform separator — relative() hands back
          // backslashes on Windows and every one of those comparisons missed.
          const provenance = `${controller.replace('.cs', '')} -> ${relative(
            FEATURES_DIR,
            file
          )
            .split(sep)
            .join('/')}`;
          keys.set(value, (keys.get(value) ?? new Set()).add(provenance));
        }
      }
    }
  }

  return {
    controllers: controllers.length,
    sites,
    unresolved: sites.filter((s) => !s.featureClass),
    featureClasses,
    keys,
  };
}

function keysEmittedAnywhere(): Set<string> {
  const constants = parseBusinessErrorConstants();
  const emitted = new Set<string>();
  for (const file of listCsFiles(APP_SERVICES_DIR)) {
    if (file.endsWith('BusinessErrorMessage.cs')) continue;
    const source = readFileSync(file, 'utf8');
    const regex = /BusinessErrorMessage\.(\w+)/g;
    let match: RegExpExecArray | null;
    while ((match = regex.exec(source)) !== null) {
      const value = constants.get(match[1]);
      if (value) emitted.add(value);
    }
  }
  return emitted;
}

function flattenKeys(obj: unknown, prefix = ''): Set<string> {
  const keys = new Set<string>();
  if (obj && typeof obj === 'object' && !Array.isArray(obj)) {
    for (const [key, value] of Object.entries(obj)) {
      const path = prefix ? `${prefix}.${key}` : key;
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        for (const nested of flattenKeys(value, path)) keys.add(nested);
      } else {
        keys.add(path);
      }
    }
  }
  return keys;
}

function apiKeySet(locale: Record<string, unknown>): Set<string> {
  const api = (locale as { api?: unknown }).api;
  return new Set([...flattenKeys(api)].map((k) => `api.${k}`));
}

function resolveKey(
  locale: Record<string, unknown>,
  dottedKey: string
): string | undefined {
  let node: unknown = locale;
  for (const segment of dottedKey.split('.')) {
    if (node && typeof node === 'object' && segment in node) {
      node = (node as Record<string, unknown>)[segment];
    } else {
      return undefined;
    }
  }
  return typeof node === 'string' ? node : undefined;
}

// Customer-surface error contract: BusinessErrorMessage dot-values a Customer
// API endpoint can return and the global HttpErrorInterceptor resolves under
// the api.* namespace.
//
// This list is HAND-KEPT and that is deliberate: it carries the reachability
// judgement deriveHostSurface() cannot make, since a feature class can emit keys
// that no customer branch reaches. What it is no longer allowed to be is the only
// input — deriveHostSurface() reads the controllers, and every key it finds must
// appear here or in DELIBERATELY_NOT_TRANSLATED with a reason. A list that is
// merely remembered cannot fail on a key added after it was written.
const CUSTOMER_SURFACE_ERROR_KEYS: readonly string[] = [
  // Auth — customer login / register / confirm / refresh
  'auth.invalid_confirmation_code',
  'auth.invalid_password_format',
  'auth.account_locked',
  'auth.too_many_attempts',
  'auth.current_password_invalid',
  'auth.insufficient_privileges',
  // Password reset: the forgot-password form posts a 6-digit code, so both
  // arms of the ChangePassword validator are reachable from the customer app.
  'auth.invalid_reset_token',
  'auth.same_reset_password',
  // Refresh: RefreshToken splits its failures in two — the reuse/theft signal
  // gets its own key, and expiry, an unknown token or an audience mismatch all
  // land on invalid_refresh_token.
  'auth.invalid_refresh_token',
  'auth.refresh_token_reused',
  // Provider mismatch: password login against an account that belongs to a
  // social provider. LoginValidator names the provider the account ACTUALLY
  // uses, so all four arms are reachable from the customer login form and each
  // needs its own string — external_type_error is the neutral fallback that any
  // AuthenticationType added later lands on.
  'auth.google_type_error',
  'auth.apple_type_error',
  'auth.external_type_error',
  'auth.internal_type_error',
  // Social token verification: the customer API rejects an id_token it cannot
  // verify. invalid_google_token is reachable from the existing Google button;
  // invalid_apple_token ships ahead of the Apple button so the string is never
  // the thing that is missing when it lands.
  'auth.invalid_google_token',
  'auth.invalid_apple_token',
  // Social sign-in provisions nothing: the terms tick lives on the signup screen, so a Google/Apple
  // identity that matches no account is refused here and sent there. Reachable from BOTH the login
  // and the register screen, since they share one endpoint.
  'auth.social_account_not_found',
  // Field-level rules the FluentValidation chains apply on almost every command
  // the booking, dispute, saved-address and profile forms post.
  'common.required',
  'common.max_length',
  'common.min_length',
  'common.invalid_enum_value',
  'email.invalid_format',
  'general.not_found',
  'validation.date_must_be_in_past',
  'validation.invalid_age',
  'validation.invalid_date',
  'validation.invalid_password',
  'validation.must_be_positive',
  // User — register / profile
  'user.email_confirmed',
  'user.not_allowed_to_update',
  'user.existing_email',
  'user.not_existing_email',
  'user.existing_phone_number',
  'user.not_found',
  // GetUser and UpdateCurrentUser both resolve the caller's own row and used to null-forgive it.
  // Guarding that null made this key reachable from the customer host for the first time.
  'user.not_existing_id',
  // Profile save carries the display-language preference, so the
  // UpdateCurrentUser language rule is reachable from the customer app.
  'language.not_supported',
  // Order — booking / cancel / review / lifecycle the customer can hit
  'order.cleaning_date.future',
  'order.cleaning_date.below_lead_time',
  'order.already_cancelled',
  'order.already_completed',
  'order.in_progress_cannot_cancel',
  'order.invalid_status_transition',
  'order.cancellation_window_closed',
  'order.address_exactly_one_required',
  'order.empty',
  'order.selected_package.invalid',
  'order.selected_services.invalid',
  'order.not_found',
  'order.no_available_spots',
  'order.weekly_limit_reached',
  'order.time_conflict',
  'order.total_price.positive',
  'order.total_price.not_match',
  'order.span_exceeds_maximum',
  'order.payment_gateway_unavailable',
  'currency.invalid',
  // Preferred cleaner — CreateOrder, CreateRecurringBooking and the standalone
  // ChoosePreferredCleaner all run the same two entitlement gates in the same
  // order, and the re-offer window closing is its own refusal.
  'order.preferred_employee.membership_required',
  'order.preferred_employee.not_eligible',
  'order.preferred_offer_closed',
  // Emitted by CreatePaymentIntent + ConfirmRecurringOrder, both dispatched by
  // Cleansia.Web.Customer. No web component calls either yet — the web card path
  // is createOrder's hosted-Checkout redirect — but the generated client carries
  // createPaymentIntent, so this ships ahead of the caller for the same reason
  // auth.invalid_apple_token does: on the money path, the string must never be
  // the thing that is missing when the button lands.
  'order.payment.already_paid',
  'order.creation_failed',
  'order.not_completed',
  'order.review.already_exists',
  'order.review.duplicate_tag',
  'order.review.rating_invalid',
  'order.review.tag_rating_mismatch',
  'order.review.too_many_tags',
  'order.review.unknown_tag',
  'order.note.content_required',
  'order.issue.description_required',
  // Address — saved addresses + order address
  'address.not_owned_by_user',
  'address.label_required',
  'address.already_exists',
  'address.invalid_length',
  'address.mapbox_coords_required',
  // Service area — booking geo gate
  'country.not_serviced',
  'country.required',
  'country.not_existing_id',
  'city.not_serviced',
  // Dispute — customer dispute flow
  'dispute.not_found',
  'dispute.already_exists',
  'dispute.invalid_refund_amount',
  'dispute.not_owned_by_user',
  'dispute.max_length_exceeded',
  // File — dispute evidence upload
  'file.content_type_doesnt_match',
  'file.invalid_file_type',
  'file.size_exceeded',
  'file.count_exceeded',
  'file.count_too_few',
  'file.required',
  'file.size_exceeded_10mb',
  'file.type_not_allowed',
  // Receipt — download
  'receipt.not_found',
  'receipt.generation_failed',
  // GDPR — export / delete / consent
  'gdpr.export_failed',
  'gdpr.deletion_failed',
  'gdpr.deletion_already_pending',
  'gdpr.deletion_blocked_by_order',
  'gdpr.deletion_blocked_by_invoice',
  'gdpr.consent_not_found',
  'gdpr.consent_already_granted',
  // Promo — checkout promo apply
  'promo.not_found',
  'promo.expired',
  'promo.not_yet_valid',
  'promo.inactive',
  'promo.global_limit_reached',
  'promo.per_user_limit_reached',
  'promo.below_minimum_order_amount',
  'promo.currency_mismatch',
  // Referral — validate
  'referral.not_found',
  'referral.self_referral',
  'referral.already_referred',
  'referral.inactive',
  // Membership — subscribe / cancel / swap
  'membership.plan.not_found',
  'membership.already_active',
  'membership.not_found',
  'membership.not_owned_by_user',
  'membership.stripe_customer_required',
  'membership.swap_same_plan',
  // The CreateOrder waiver rule runs BEFORE the price rule and has its own code
  // precisely so this never renders as the generic "the price changed".
  'membership.express_waiver.no_longer_available',
  // Recurring booking — create / manage
  'recurring_booking.not_found',
  'recurring_booking.not_owned_by_user',
  'recurring_booking.saved_address_not_found',
  'recurring_booking.no_services_or_packages',
  'recurring_booking.starts_on_in_past',
  'recurring_booking.ends_on_before_start',
  'recurring_booking.membership_required',
  // Device — push registration
  'device.invalid_platform',
  'device.not_found',
];

// Reachable from a Cleansia.Web.Customer controller and deliberately left out of
// the contract above. This is the only escape from the coverage test, so each
// entry states why the customer can never read the string.
const DELIBERATELY_NOT_TRANSLATED: ReadonlyArray<{
  key: string;
  reason: string;
}> = [
  {
    key: 'payment.json_payload_required',
    reason:
      'Guard on POST /api/Payment/webhook, whose only caller is Stripe: the body is read off the raw request, and the refusal is returned to a Stripe server, never rendered to a customer.',
  },
  {
    key: 'payment.stripe_signature_required',
    reason:
      'Same webhook guard, on the Stripe-Signature header. The generated client exposes webhook() because NSwag emits every route, but no facade calls it.',
  },
];

// Contract keys that no BusinessErrorMessage reference anywhere in
// Cleansia.Core.AppServices emits — the constant is declared and dead. Asserted
// as an exact set in both directions: a contract entry that goes dead has to be
// listed or deleted, and one that comes back to life has to leave.
//
// Two different things sit here. The promo.* codes are alive but travel a
// different road: ValidatePromoCode returns the PromoValidationError enum name in
// its Response, and the order-promo facade maps that onto api.promo.*, so no
// handler ever names the constant. The rest were declared for refusals that were
// never wired to a validator; their strings ship ahead of the rule, the way
// auth.invalid_apple_token does.
const DECLARED_BUT_NEVER_EMITTED: readonly string[] = [
  'file.count_too_few',
  'file.size_exceeded_10mb',
  'gdpr.deletion_failed',
  'gdpr.export_failed',
  'membership.not_owned_by_user',
  'membership.stripe_customer_required',
  'order.cancellation_window_closed',
  'order.creation_failed',
  'order.review.already_exists',
  'promo.below_minimum_order_amount',
  'promo.currency_mismatch',
  'promo.expired',
  'promo.global_limit_reached',
  'promo.inactive',
  'promo.not_yet_valid',
  'promo.per_user_limit_reached',
  'receipt.generation_failed',
  'referral.already_referred',
  'referral.inactive',
  'referral.self_referral',
];

describe('error-contract parity (customer app, EP-1/EP-2/DA-7)', () => {
  const en = readLocale('en');

  describe('the customer surface derived from Cleansia.Web.Customer', () => {
    const surface = deriveHostSurface();
    const excluded = DELIBERATELY_NOT_TRANSLATED.map((e) => e.key);

    it('reaches real controllers and real dispatch sites', () => {
      expect(surface.controllers).toBeGreaterThanOrEqual(15);
      expect(surface.sites.length).toBeGreaterThanOrEqual(55);
      expect(surface.featureClasses.size).toBeGreaterThanOrEqual(55);
      expect(surface.keys.size).toBeGreaterThanOrEqual(60);
    });

    it('resolves every dispatch site to a feature file', () => {
      expect(surface.unresolved).toEqual([]);
    });

    it('walks controller to feature file to constant to dot-value', () => {
      expect([...(surface.keys.get('order.total_price.not_match') ?? [])]).toContain(
        'OrderController -> Orders/CreateOrder.cs'
      );
      expect([...(surface.keys.get('order.preferred_offer_closed') ?? [])]).toEqual([
        'OrderController -> Orders/ChoosePreferredCleaner.cs',
      ]);
    });

    it('leaves no derived key unclassified', () => {
      const unclassified = [...surface.keys.keys()]
        .filter(
          (key) =>
            !CUSTOMER_SURFACE_ERROR_KEYS.includes(key) && !excluded.includes(key)
        )
        .sort();
      expect(unclassified).toEqual([]);
    });

    it('excludes only keys that really are reachable and really are untranslated', () => {
      const notReachable = excluded.filter((key) => !surface.keys.has(key));
      const alsoOnTheContract = excluded.filter((key) =>
        CUSTOMER_SURFACE_ERROR_KEYS.includes(key)
      );
      const actuallyTranslated = excluded.filter((key) =>
        resolveKey(en, `api.${key}`)
      );
      const unexplained = DELIBERATELY_NOT_TRANSLATED.filter(
        (entry) => entry.reason.trim().length === 0
      ).map((entry) => entry.key);
      expect({
        notReachable,
        alsoOnTheContract,
        actuallyTranslated,
        unexplained,
      }).toEqual({
        notReachable: [],
        alsoOnTheContract: [],
        actuallyTranslated: [],
        unexplained: [],
      });
    });

    it('reports contract keys the backend no longer emits anywhere', () => {
      const emitted = keysEmittedAnywhere();
      const dead = CUSTOMER_SURFACE_ERROR_KEYS.filter(
        (key) => !emitted.has(key)
      ).sort();
      expect(dead).toEqual([...DECLARED_BUT_NEVER_EMITTED].sort());
    });
  });

  it('AC4 contract: every customer-surface key exists as a BusinessErrorMessage value', () => {
    const backendValues = parseBusinessErrorValues();
    const orphaned = CUSTOMER_SURFACE_ERROR_KEYS.filter(
      (key) => !backendValues.has(key)
    );
    expect(orphaned).toEqual([]);
  });

  it('AC1/AC3: every customer-surface key resolves to a real string under api.* in en.json', () => {
    const missing = CUSTOMER_SURFACE_ERROR_KEYS.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !value || value.trim().length === 0;
    });
    expect(missing).toEqual([]);
  });

  it('AC2: the five locale files have identical api.* key sets', () => {
    const enApiKeys = apiKeySet(en);
    for (const locale of LOCALES) {
      if (locale === 'en') continue;
      const localeApiKeys = apiKeySet(readLocale(locale));
      const missingInLocale = [...enApiKeys].filter(
        (k) => !localeApiKeys.has(k)
      );
      const extraInLocale = [...localeApiKeys].filter((k) => !enApiKeys.has(k));
      expect({ locale, missingInLocale, extraInLocale }).toEqual({
        locale,
        missingInLocale: [],
        extraInLocale: [],
      });
    }
  });

  it('AC2: every customer-surface key has a non-empty translation in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = CUSTOMER_SURFACE_ERROR_KEYS.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });


  // ── Presence is not translation ─────────────────────────────────────────────
  //
  // Every assertion above this line is satisfied by an English string copied into
  // five files: the key exists, resolves, is non-empty, and the five key sets
  // match. That is exactly how a "translated" key ships untranslated, and it is
  // the one shape none of them can see.
  //
  // iOS has carried this check since its catalog guard was written
  // (StringCatalogCompletenessTests, rule (c) — "no non-English value equal to
  // the English source"). The web side never had it. Baseline measured
  // 2026-08-09 before the assertion was added: 559 api.* keys across all three
  // apps, four non-English locales each, ZERO echoes — so it lands with no
  // allow-list, and the first entry anyone needs will be a deliberate argument
  // rather than a grandfathered clause.
  //
  // Scoped to api.* on purpose. These are backend refusals — prose a person
  // reads when something went wrong — and there is no such thing as a
  // locale-invariant one. The wider bundle legitimately carries product names
  // and format-only strings, which is why iOS needs an allow-list and this
  // does not.
  it('no api.* value is left as the English source in another locale', () => {
    const en = readLocale('en');
    const echoed: string[] = [];

    for (const key of [...apiKeySet(en)]) {
      const source = resolveKey(en, key);
      if (typeof source !== 'string' || source.trim().length === 0) continue;

      for (const locale of LOCALES.filter((l) => l !== 'en')) {
        if (resolveKey(readLocale(locale), key) === source) {
          echoed.push(`${key} (${locale})`);
        }
      }
    }

    expect(echoed.sort()).toEqual([]);
  });

  it('AC4: the generic fallback key resolves in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const value = resolveKey(data, GENERIC_FALLBACK_KEY);
      expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
    }
  });
});
