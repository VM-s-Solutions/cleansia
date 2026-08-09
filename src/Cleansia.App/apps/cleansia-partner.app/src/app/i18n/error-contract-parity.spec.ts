import { existsSync, readdirSync, readFileSync } from 'fs';
import { dirname, join, relative } from 'path';

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

// The host that serves this app: Cleansia.Web.Partner listens on :5000 and the
// partner dev server proxies /api to it (apps/cleansia-partner.app/proxy.conf.json).
const HOST_CONTROLLERS_DIR = join(
  SOLUTION_DIR,
  'Cleansia.Web.Partner/Controllers'
);

const TAKE_ORDER_PATH = join(FEATURES_DIR, 'Orders/TakeOrder.cs');

const I18N_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/apps/cleansia-partner.app/src/assets/i18n'
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

function keysEmittedBy(emitterPath: string): string[] {
  const source = readFileSync(emitterPath, 'utf8');
  const constants = parseBusinessErrorConstants();
  const emitted = new Set<string>();
  const regex = /BusinessErrorMessage\.(\w+)/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(source)) !== null) {
    const value = constants.get(match[1]);
    if (value) emitted.add(value);
  }
  return [...emitted].sort();
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
    const className = file.slice(file.lastIndexOf('/') + 1, -'.cs'.length);
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
          const provenance = `${controller.replace('.cs', '')} -> ${relative(
            FEATURES_DIR,
            file
          )}`;
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

// Partner-surface error contract: the BusinessErrorMessage dot-values the
// Partner API (Cleansia.Web.Partner) can return and the shared
// HttpErrorInterceptor resolves under the api.* namespace.
//
// This list is HAND-KEPT and that is deliberate: it carries the reachability
// judgement deriveHostSurface() cannot make, since a feature class can emit keys
// that no partner branch reaches. What it is no longer allowed to be is the only
// input — deriveHostSurface() reads the controllers, and every key it finds must
// appear here or in DELIBERATELY_NOT_TRANSLATED with a reason. A list that is
// merely remembered cannot fail on a key added after it was written.
const PARTNER_SURFACE_ERROR_KEYS: readonly string[] = [
  // Auth — partner login / confirm / reset / refresh
  'auth.insufficient_privileges',
  'auth.invalid_confirmation_code',
  'auth.invalid_google_token',
  'auth.invalid_refresh_token',
  'auth.invalid_reset_token',
  'auth.refresh_token_reused',
  'auth.same_reset_password',
  // Same GoogleAuth endpoint as the customer host: an identity matching no account is refused rather
  // than provisioned, so the partner surface can return it too.
  'auth.social_account_not_found',
  'auth.too_many_attempts',
  // Shared field-level rules
  'common.max_length',
  'common.required',
  // Order lifecycle the cleaner drives: take → on the way → start → cash → complete
  'order.after_photos.required',
  'order.card_payment_already_settled',
  'order.card_payment_in_progress',
  'order.card_payment_unverified',
  'order.cash_already_collected',
  'order.cash_not_collected',
  'order.completion_notes.too_long',
  'order.employee_already_assigned',
  'order.employee_already_has_order_in_progress',
  'order.employee_not_assigned',
  'order.no_available_spots',
  'order.not_confirmed',
  'order.not_found',
  'order.not_in_progress',
  'order.not_takeable',
  'order.payment_not_confirmed',
  // The take gate has its own terminal-state keys: the flat order.already_* pair stays with the
  // customer and admin cancel paths, which no partner controller dispatches.
  'order.take.already_cancelled',
  'order.take.already_completed',
  'order.time_conflict',
  'order.weekly_limit_reached',
  // Employee profile + documents
  'employee.job_radius_out_of_range',
  'employee.not_allowed_to_update',
  'employee.not_approved',
  'employee.not_found',
  'employee.profile_incomplete',
  'employee_document.not_found',
  'employee_document.not_owned',
  'employee_document.unauthorized',
  'general.not_found',
  // Order photos + document uploads
  'file.count_exceeded',
  'file.invalid_file_type',
  'file.required',
  'file.size_exceeded',
  // Profile address / country
  'country.not_existing_id',
  'country.not_serviced',
  'dispute.max_length_exceeded',
  'language.not_supported',
  'validation.date_must_be_in_past',
  'validation.invalid_age',
  'validation.invalid_availability_format',
  'validation.invalid_date',
  'validation.invalid_password',
  // Country-scoped IČO/VAT format checks on the cleaner's own profile save
  // (UpdateEmployee, dispatched by Cleansia.Web.Partner).
  'validation.registration_number.invalid_format',
  'validation.vat_number.invalid_format',
  // User account
  'user.email_confirmed',
  'user.existing_email',
  'user.existing_phone_number',
  'user.not_allowed_to_update',
  'user.not_existing_email',
  'user.not_existing_id',
  'user.not_found',
  // GDPR consents
  'gdpr.consent_already_granted',
  'gdpr.consent_not_found',
  // Payout destination — UpdateBankDetails runs the whole PayoutDetailsValidator
  // chain, so every arm of it is reachable from the cleaner's bank-details form;
  // GetMyPayoutDetails returns payout.not_found.
  'payout.not_found',
  'validation.payout.account_number_required',
  'validation.payout.country_not_supported',
  'validation.payout.iban_country_mismatch',
  'validation.payout.iban_mismatch',
  'validation.payout.invalid_account_number',
  'validation.payout.invalid_account_prefix',
  'validation.payout.invalid_bank_code',
  'validation.payout.invalid_iban',
  'validation.payout.invalid_swift',
  'validation.payout.looks_like_card',
  'validation.payout.scheme_not_supported',
  'validation.payout.swift_required',
  // Payroll — invoices, pay periods, pay calculation
  'payroll.employee_not_assigned',
  'payroll.invoice.not_found',
  'payroll.no_active_period',
  'payroll.no_pay_configuration',
  'payroll.pay.already_calculated',
  'payroll.pay_period.not_found',
  'company.not_found',
  'currency.invalid',
  'receipt.not_found',
  // Pay configuration
  'pay_config.already_exists',
  'pay_config.base_pay_negative',
  'pay_config.cannot_have_both',
  'pay_config.distance_rate_negative',
  'pay_config.extra_per_bathroom_negative',
  'pay_config.extra_per_room_negative',
  'pay_config.has_order_pays',
  'pay_config.maximum_less_than_minimum',
  'pay_config.maximum_pay_negative',
  'pay_config.minimum_pay_negative',
  'pay_config.not_found',
  'pay_config.service_or_package_required',
  // Stripe webhook payload guards
  'payment.json_payload_required',
  'payment.stripe_signature_required',
];

// Reachable from a Cleansia.Web.Partner controller and deliberately left out of
// the contract above. This is the only escape from the coverage test, so each
// entry states why the cleaner can never read the string — "we did not get to it
// yet" is PENDING_TRANSLATION, not this list.
const DELIBERATELY_NOT_TRANSLATED: ReadonlyArray<{
  key: string;
  reason: string;
}> = [];

// Roster keys that no BusinessErrorMessage reference anywhere in
// Cleansia.Core.AppServices emits — the constant is declared and dead. Asserted
// as an exact set in both directions: a roster entry that goes dead has to be
// listed or deleted, and one that comes back to life has to leave.
const DECLARED_BUT_NEVER_EMITTED: readonly string[] = [];

// Contract keys that have no partner translation yet. Every entry here is a
// cleaner who gets "An error occurred. Please try again." instead of the real
// reason, so the list may only ever shrink: translate the key in all five
// locales and delete its line. A key that is BOTH listed here and translated
// fails the ratchet below.
const PENDING_TRANSLATION: readonly string[] = [];

const TRANSLATED_CONTRACT_KEYS = PARTNER_SURFACE_ERROR_KEYS.filter(
  (key) => !PENDING_TRANSLATION.includes(key)
);

describe('error-contract parity (partner app)', () => {
  const en = readLocale('en');

  describe('the partner surface derived from Cleansia.Web.Partner', () => {
    const surface = deriveHostSurface();
    const excluded = DELIBERATELY_NOT_TRANSLATED.map((e) => e.key);

    it('reaches real controllers and real dispatch sites', () => {
      expect(surface.controllers).toBeGreaterThanOrEqual(15);
      expect(surface.sites.length).toBeGreaterThanOrEqual(60);
      expect(surface.featureClasses.size).toBeGreaterThanOrEqual(55);
      expect(surface.keys.size).toBeGreaterThanOrEqual(70);
    });

    it('resolves every dispatch site to a feature file', () => {
      expect(surface.unresolved).toEqual([]);
    });

    it('walks controller to feature file to constant to dot-value', () => {
      expect([...(surface.keys.get('employee.job_radius_out_of_range') ?? [])]).toEqual([
        'EmployeeController -> Employees/UpdateJobRadius.cs',
      ]);
      expect([...(surface.keys.get('order.not_takeable') ?? [])]).toContain(
        'OrderController -> Orders/TakeOrder.cs'
      );
    });

    it('leaves no derived key unclassified', () => {
      const unclassified = [...surface.keys.keys()]
        .filter(
          (key) =>
            !PARTNER_SURFACE_ERROR_KEYS.includes(key) && !excluded.includes(key)
        )
        .sort();
      expect(unclassified).toEqual([]);
    });

    it('excludes only keys that really are reachable and really are untranslated', () => {
      const notReachable = excluded.filter((key) => !surface.keys.has(key));
      const alsoOnTheContract = excluded.filter((key) =>
        PARTNER_SURFACE_ERROR_KEYS.includes(key)
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
      const dead = PARTNER_SURFACE_ERROR_KEYS.filter(
        (key) => !emitted.has(key)
      ).sort();
      expect(dead).toEqual([...DECLARED_BUT_NEVER_EMITTED].sort());
    });
  });

  it('every partner-surface key exists as a BusinessErrorMessage value', () => {
    const backendValues = parseBusinessErrorValues();
    const orphaned = PARTNER_SURFACE_ERROR_KEYS.filter(
      (key) => !backendValues.has(key)
    );
    expect(orphaned).toEqual([]);
  });

  it('every partner-surface key resolves to a real string under api.* in en.json', () => {
    const missing = TRANSLATED_CONTRACT_KEYS.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !value || value.trim().length === 0;
    });
    expect(missing).toEqual([]);
  });

  it('every partner-surface key has a non-empty translation in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = TRANSLATED_CONTRACT_KEYS.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('the five locale files have identical api.* key sets', () => {
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

  it('the pending list only holds keys that really are untranslated', () => {
    const alreadyTranslated = PENDING_TRANSLATION.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !!value && value.trim().length > 0;
    });
    expect(alreadyTranslated).toEqual([]);
  });

  it('the pending list holds only real contract keys', () => {
    const stale = PENDING_TRANSLATION.filter(
      (key) => !PARTNER_SURFACE_ERROR_KEYS.includes(key)
    );
    expect(stale).toEqual([]);
  });

  describe('the take-order refusal reasons a racing cleaner hits', () => {
    // Read out of TakeOrder.cs rather than listed here. A hand-kept list cannot fail on a key
    // the backend added after it was written, which is exactly how order.take.already_* reached
    // three clients before this app: the missing key renders "An error occurred. Please try
    // again." — indistinguishable from a 500 — and the cleaner reclicks the same dead job.
    const takeRefusals = keysEmittedBy(TAKE_ORDER_PATH);

    it('are read out of the emitter, not remembered', () => {
      expect(takeRefusals).toEqual(expect.arrayContaining(
        ['order.take.already_cancelled', 'order.take.already_completed']
      ));
      expect(takeRefusals.length).toBeGreaterThanOrEqual(10);
    });

    it('are all on the partner surface contract', () => {
      const unlisted = takeRefusals.filter(
        (key) => !PARTNER_SURFACE_ERROR_KEYS.includes(key)
      );
      expect(unlisted).toEqual([]);
    });

    it('are all translated in all five locales', () => {
      for (const locale of LOCALES) {
        const data = readLocale(locale);
        const missing = takeRefusals.filter((key) => {
          const value = resolveKey(data, `api.${key}`);
          return !value || value.trim().length === 0;
        });
        expect({ locale, missing }).toEqual({ locale, missing: [] });
      }
    });
  });

  it('the generic fallback key resolves in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const value = resolveKey(data, GENERIC_FALLBACK_KEY);
      expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
    }
  });
});
