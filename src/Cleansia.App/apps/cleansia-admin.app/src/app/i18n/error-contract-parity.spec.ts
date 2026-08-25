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

// The host that serves this app: Cleansia.Web.Admin listens on :5001 and the
// admin dev server proxies /api to it (apps/cleansia-admin.app/proxy.conf.json).
const HOST_CONTROLLERS_DIR = join(
  SOLUTION_DIR,
  'Cleansia.Web.Admin/Controllers'
);

const I18N_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n'
);

// Everything the admin app compiles: its own source plus the admin feature libs it consumes. Both are
// walked by the errors.* retirement guard below — a resurrected key is as likely to land in a lib as
// in the app shell.
const ADMIN_SOURCE_ROOTS = [
  join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia-admin.app/src'),
  join(SOLUTION_DIR, 'Cleansia.App/libs/cleansia-admin-features'),
];

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

function namespaceKeySet(
  locale: Record<string, unknown>,
  namespace: string
): Set<string> {
  const block = (locale as Record<string, unknown>)[namespace];
  return new Set([...flattenKeys(block)].map((k) => `${namespace}.${k}`));
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

// Admin resolves a backend error through TWO namespaces, and both are live:
//
//   api.*    — the shared HttpErrorInterceptorFn, which admin inherits via
//              COMMON_INTERCEPTORS_FN. It fires for EVERY non-404/403 error and
//              looks up `api.${dotValue}`, falling back to the generic message
//              when the key is absent. This is the canonical path.
//   errors.* — the per-feature XXX_ERROR_KEY_MAP resolvers that several admin
//              features still carry (orders, disputes, refunds, referrals).
//              Back-compat only; new work uses the interceptor path.
//
// A key written under only one of them when the reading path uses the other
// reads as "An error occurred. Please try again." — the exact silent swallow
// this guard exists to catch.
// Admin-surface error contract: every BusinessErrorMessage dot-value a
// Cleansia.Web.Admin controller can return, translated under api.* in all five
// locales so the shared interceptor path resolves it.
//
// This list is HAND-KEPT and that is deliberate: it carries the reachability
// judgement deriveHostSurface() cannot make, since a feature class can emit keys
// that no admin branch reaches. What it is no longer allowed to be is the only
// input — deriveHostSurface() reads the controllers, and every key it finds must
// appear here or in DELIBERATELY_NOT_TRANSLATED with a reason. A list that is
// merely remembered cannot fail on a key added after it was written.
const ADMIN_SURFACE_ERROR_KEYS: readonly string[] = [
  // Admin sign-in and the admin-user screen
  'admin_user.cannot_deactivate_last_admin',
  'admin_user.cannot_deactivate_self',
  'admin_user.cannot_delete_self',
  'admin_user.cannot_target_admin_via_gdpr_tool',
  'admin_user.email_exists',
  'admin_user.not_found',
  'auth.account_locked',
  'auth.current_password_invalid',
  'auth.insufficient_privileges',
  'auth.invalid_refresh_token',
  'auth.refresh_token_reused',
  // Audit log detail
  'audit.not_found',
  // Field-level rules every admin form posts through
  'common.invalid_enum_value',
  'common.max_length',
  'common.required',
  'email.invalid_email',
  'email.invalid_format',
  'general.not_found',
  'validation.date_must_be_in_past',
  'validation.invalid_age',
  'validation.invalid_availability_format',
  'validation.invalid_date',
  'validation.invalid_password',
  'validation.must_be_positive',
  // Platform configuration: company, countries, currencies, languages
  'company.exists_for_country',
  'company.in_use',
  'company.not_found',
  'country.in_use',
  'country.iso_code_already_exists',
  'country.not_existing_id',
  'country.not_found',
  'country.not_serviced',
  'country.required',
  'currency.cannot_delete_default',
  'currency.code_already_exists',
  'currency.exchange_rate_must_be_positive',
  'currency.in_use',
  'currency.invalid',
  'currency.not_found',
  'language.code_already_exists',
  'language.in_use',
  'language.not_found',
  'language.not_supported',
  // Catalogue: services, packages, serviced cities
  'package.in_use',
  'package.invalid_weight',
  'package.not_found',
  'service.category_not_found',
  'service.in_use',
  'service.not_found',
  'service_city.already_exists',
  'service_city.not_found',
  // Employees: approval, documents, payout reveal
  'employee.already_approved',
  'employee.already_rejected',
  'employee.documents_not_approved',
  'employee.not_found',
  'employee.pay_config_missing',
  'employee.profile_incomplete',
  'employee.weekly_limit_invalid',
  'employee_document.deletion_already_resolved',
  'employee_document.not_found',
  'payout.not_found',
  // Orders, disputes, refunds, receipts
  'dispute.already_resolved',
  'dispute.invalid_refund_amount',
  'dispute.invalid_status_transition',
  'dispute.max_length_exceeded',
  'dispute.not_found',
  'dispute.not_owned_by_user',
  'order.already_cancelled',
  'order.already_completed',
  'order.employee_already_assigned',
  'order.employee_not_assigned',
  'order.in_progress_cannot_cancel',
  'order.invalid_status_transition',
  'order.no_available_spots',
  'order.not_found',
  'receipt.not_found',
  'refund.line_invalid',
  'refund.lines_required',
  'refund.nothing_refundable',
  'refund.order_not_refundable',
  'refund.override_reason_required',
  // Payroll: pay configs, pay periods, invoices
  'pay_config.already_exists',
  'pay_config.base_pay_negative',
  'pay_config.cannot_have_both',
  'pay_config.distance_rate_negative',
  'pay_config.extra_per_bathroom_negative',
  'pay_config.extra_per_room_negative',
  'pay_config.has_order_pays',
  'pay_config.last_for_live_catalogue_entry',
  'pay_config.maximum_less_than_minimum',
  'pay_config.maximum_pay_negative',
  'pay_config.minimum_pay_negative',
  'pay_config.not_found',
  'pay_config.service_or_package_required',
  'pay_period.already_paid',
  'pay_period.has_order_pays',
  'pay_period.invalid_duration',
  'pay_period.not_closed',
  'pay_period.overlapping_period',
  'payroll.invoice.already_cancelled',
  'payroll.invoice.already_exists',
  'payroll.invoice.already_paid',
  'payroll.invoice.cannot_cancel_paid',
  'payroll.invoice.invalid_status',
  'payroll.invoice.not_approved',
  'payroll.invoice.not_found',
  // cdd3133b — RegenerateInvoicePdf now RECORDS a failed render on the row instead of
  // clearing the flag it never set, so this key became reachable rather than theoretical.
  'payroll.invoice.pdf_generation_failed',
  // ADR-0046 — the payout invoice's variabilni symbol. All four are raised on
  // admin-reachable routes (AdminPayrollController.GenerateInvoice /
  // AssignInvoiceVariableSymbol, AdminInvoiceController.MarkInvoicePaid), so a missing
  // locale renders the generic error on the one screen where an admin has to act on it.
  'payroll.invoice.reference_already_assigned',
  'payroll.invoice.reference_capacity_exhausted',
  'payroll.invoice.reference_missing',
  'payroll.invoice.reference_unavailable',
  'payroll.no_unpaid_order_pays',
  'payroll.pay_period.not_found',
  'payroll.pay_period.not_open',
  'payroll.unpaid_invoices_exist',
  // Growth: promo codes, referrals, loyalty, membership plans
  'loyalty.points_exceed_sanity_cap',
  'loyalty.reason_required',
  'loyalty.tier_config_not_found',
  'loyalty.tier_perks_json_invalid',
  'membership.plan.code_already_exists',
  'membership.plan.discount_out_of_range',
  'membership.plan.not_found',
  'promo.amount_must_be_positive',
  'promo.code_already_exists',
  'promo.code_invalid_format',
  'promo.not_found',
  'promo.percent_out_of_range',
  'promo.validity_range_invalid',
  'referral.not_accepted',
  'referral.not_found',
  'referral.not_qualified',
  'referral.reason_required',
  // Feature flags and email templates
  'feature_flag.already_exists',
  'feature_flag.not_found',
  'template.email.invalid_type',
  'template.email.key_exists',
  'template.email.not_found',
  // Users
  'user.not_existing_id',
  'user.not_found',
];

// Reachable from a Cleansia.Web.Admin controller and deliberately left out of the
// contract above. This is the only escape from the coverage test, so each entry
// states why an operator can never read the string.
const DELIBERATELY_NOT_TRANSLATED: ReadonlyArray<{
  key: string;
  reason: string;
}> = [];

// Contract keys that no BusinessErrorMessage reference anywhere in
// Cleansia.Core.AppServices emits — the constant is declared and dead. Asserted
// as an exact set in both directions: a contract entry that goes dead has to be
// listed or deleted, and one that comes back to life has to leave.
const DECLARED_BUT_NEVER_EMITTED: readonly string[] = [];

const ADMIN_PAYOUT_SURFACE_ERROR_KEYS: readonly string[] = [
  // AdminEmployeeController: GetEmployeePayoutDetails + RevealEmployeePayoutDetails.
  'payout.not_found',
];

// Reachable from a partner host today, not from an admin one — the admin API
// exposes no payout WRITE endpoint, so the PayoutDetailsValidator chain cannot
// run behind an admin request. Shipped ahead of the admin bank-details editor so
// the strings are never the thing that is missing when it lands; asserted for
// translation but deliberately NOT asserted as admin contract.
const ADMIN_PAYOUT_EDITOR_KEYS: readonly string[] = [
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
];

const PAYOUT_KEYS = [
  ...ADMIN_PAYOUT_SURFACE_ERROR_KEYS,
  ...ADMIN_PAYOUT_EDITOR_KEYS,
];

describe('error-contract parity (admin app)', () => {
  const en = readLocale('en');

  describe('the admin surface derived from Cleansia.Web.Admin', () => {
    const surface = deriveHostSurface();
    const excluded = DELIBERATELY_NOT_TRANSLATED.map((e) => e.key);

    it('reaches real controllers and real dispatch sites', () => {
      expect(surface.controllers).toBeGreaterThanOrEqual(25);
      expect(surface.sites.length).toBeGreaterThanOrEqual(120);
      expect(surface.featureClasses.size).toBeGreaterThanOrEqual(120);
      expect(surface.keys.size).toBeGreaterThanOrEqual(100);
    });

    it('resolves every dispatch site to a feature file', () => {
      expect(surface.unresolved).toEqual([]);
    });

    it('walks controller to feature file to constant to dot-value', () => {
      expect([...(surface.keys.get('payroll.invoice.not_approved') ?? [])]).toContain(
        'AdminInvoiceController -> EmployeePayroll/MarkInvoicePaid.cs'
      );
      expect([...(surface.keys.get('audit.not_found') ?? [])]).toEqual([
        'AdminAuditLogController -> Auditing/GetAdminActionAuditById.cs',
      ]);
    });

    it('leaves no derived key unclassified', () => {
      const unclassified = [...surface.keys.keys()]
        .filter(
          (key) =>
            !ADMIN_SURFACE_ERROR_KEYS.includes(key) && !excluded.includes(key)
        )
        .sort();
      expect(unclassified).toEqual([]);
    });

    it('excludes only keys that really are reachable and really are untranslated', () => {
      const notReachable = excluded.filter((key) => !surface.keys.has(key));
      const alsoOnTheContract = excluded.filter((key) =>
        ADMIN_SURFACE_ERROR_KEYS.includes(key)
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
      const dead = ADMIN_SURFACE_ERROR_KEYS.filter(
        (key) => !emitted.has(key)
      ).sort();
      expect(dead).toEqual([...DECLARED_BUT_NEVER_EMITTED].sort());
    });
  });

  it('every admin-surface key exists as a BusinessErrorMessage value', () => {
    const backendValues = parseBusinessErrorValues();
    const orphaned = ADMIN_SURFACE_ERROR_KEYS.filter(
      (key) => !backendValues.has(key)
    );
    expect(orphaned).toEqual([]);
  });

  it('every admin-surface key resolves to a real string under api.* in en.json', () => {
    const missing = ADMIN_SURFACE_ERROR_KEYS.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !value || value.trim().length === 0;
    });
    expect(missing).toEqual([]);
  });

  it('every admin-surface key has a non-empty translation in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = ADMIN_SURFACE_ERROR_KEYS.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('every admin payout-surface key exists as a BusinessErrorMessage value', () => {
    const backendValues = parseBusinessErrorValues();
    const orphaned = PAYOUT_KEYS.filter((key) => !backendValues.has(key));
    expect(orphaned).toEqual([]);
  });

  it('every payout key resolves to a real string under api.* in en.json', () => {
    const missing = PAYOUT_KEYS.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !value || value.trim().length === 0;
    });
    expect(missing).toEqual([]);
  });

  it('every payout key has a non-empty translation in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = PAYOUT_KEYS.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  // `errors` was the second namespace here until CL-021 retired it. Its retirement is asserted by
  // 'the legacy errors.* namespace stays retired' below, which is a stronger check than key-set parity:
  // parity would pass on five locales that all still carried it.
  for (const namespace of ['api'] as const) {
    it(`the five locale files have identical ${namespace}.* key sets`, () => {
      const enKeys = namespaceKeySet(en, namespace);
      expect(enKeys.size).toBeGreaterThan(0);
      for (const locale of LOCALES) {
        if (locale === 'en') continue;
        const localeKeys = namespaceKeySet(readLocale(locale), namespace);
        const missingInLocale = [...enKeys].filter((k) => !localeKeys.has(k));
        const extraInLocale = [...localeKeys].filter((k) => !enKeys.has(k));
        expect({ locale, missingInLocale, extraInLocale }).toEqual({
          locale,
          missingInLocale: [],
          extraInLocale: [],
        });
      }
    });

    it(`no ${namespace}.* value is blank in any locale`, () => {
      for (const locale of LOCALES) {
        const data = readLocale(locale);
        const blank = [...namespaceKeySet(data, namespace)].filter((key) => {
          const value = resolveKey(data, key);
          return !value || value.trim().length === 0;
        });
        expect({ locale, blank }).toEqual({ locale, blank: [] });
      }
    });
  }


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

    for (const key of [...namespaceKeySet(en, 'api')]) {
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

  it('the generic fallback key resolves in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const value = resolveKey(data, GENERIC_FALLBACK_KEY);
      expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
    }
  });

  // The legacy admin-only `errors.*` namespace is gone (CL-021). It existed because several admin
  // features resolved through their own XXX_ERROR_KEY_MAP instead of the shared interceptor, so admin
  // carried a SECOND copy of 169 keys that api.* already had 164 of. Two namespaces for one fact is
  // the defect this repo refuses everywhere else, and it had a live cost: a key written under only one
  // of them reads as "An error occurred. Please try again."
  //
  // These two assertions are what stop it growing back — one for the data, one for the readers.
  describe('the legacy errors.* namespace stays retired', () => {
    it('no locale carries an errors block', () => {
      const offenders = LOCALES.filter((locale) => 'errors' in readLocale(locale));
      expect(offenders).toEqual([]);
    });

    it('nothing in the admin app resolves an errors.* key', () => {
      const offenders: string[] = [];
      const walk = (dir: string) => {
        for (const entry of readdirSync(dir, { withFileTypes: true })) {
          const full = join(dir, entry.name);
          if (entry.isDirectory()) {
            if (entry.name === 'node_modules') continue;
            walk(full);
            continue;
          }
          if (!/\.(ts|html)$/.test(entry.name)) continue;
          if (full === __filename) continue;
          const source = readFileSync(full, 'utf8');
          if (/['"`]errors\.[a-z_]/.test(source)) {
            offenders.push(relative(ADMIN_SOURCE_ROOTS[0], full));
          }
        }
      };
      for (const root of ADMIN_SOURCE_ROOTS) {
        if (existsSync(root)) walk(root);
      }
      expect(offenders.sort()).toEqual([]);
    });
  });
});
