import { Page } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';
import { call, Host, list, named, PAYMENT_STATUS, payStripeCheckout, png, poll, Reply, Session, signIn, str } from './dev';
import { THROWAWAY } from './plan';
import { emailOf, Role, RunState, saveRun } from './run';
import { AdminRole, cachedAdmin, cachedAdminAs } from './web';

// The API journeys every phase-2 stage repeats. Each returns DEV's reply and asserts nothing, so a test asserts the
// refusal or the success it is about; only book() writes the run state (the order it created).

const rows = (body: unknown) => (list(body, 'data').length ? list(body, 'data') : list(body, 'items'));
const rand = (digits: number) => String(Math.floor(Math.random() * 10 ** digits)).padStart(digits, '0');

export interface Selection {
  /** Catalogue service names; THROWAWAY is the run's two-cleaner service (run.serviceId). */
  services: string[];
  extras?: string[];
  rooms: number;
  bathrooms: number;
  level: number;
  slot: Date;
}

export interface Booking extends Selection {
  payment: 'card' | 'cash';
  /** The account that books it; a guest booking (guest: true) books anonymously with that account's e-mail. */
  customer: Role;
  guest?: boolean;
  /** A signed-in booking's saved address; a guest's is sent inline. */
  addressId?: string;
  saveCard?: boolean;
  accessInstructions?: string;
  crew?: Role[];
}

/** The service ids of a selection, by name from the market's catalogue (the throwaway from the run). */
export async function serviceIds(s: Session, run: RunState, names: string[]): Promise<string[]> {
  const catalogue = (await call(s, 'GET', `/api/Service/GetOverview?countryId=${run.countryId}`)).body as unknown[];
  return names.map((n) => {
    const id = n === THROWAWAY ? (run.serviceId ?? '') : str((catalogue ?? []).find((x) => str(x, 'name') === n), 'id');
    if (!id) throw new Error(`no ${n} in the DEV catalogue of run ${run.runId}`);
    return id;
  });
}

/** POST api/Order/Quote for a selection; `total` is the quoted totalPrice. */
export async function quote(s: Session, run: RunState, sel: Selection) {
  const ids = await serviceIds(s, run, sel.services);
  const reply = await call(s, 'POST', '/api/Order/Quote', {
    selectedServiceIds: ids, selectedPackageIds: [], rooms: sel.rooms, bathrooms: sel.bathrooms, currencyId: null,
    selectedExtraSlugs: sel.extras ?? [], cleaningDate: sel.slot.toISOString(), countryId: run.countryId, dirtinessLevel: sel.level,
  });
  return { reply, total: Number(str(reply.body, 'totalPrice')), serviceIds: ids };
}

/**
 * Quotes, then POST api/Order/CreateOrder at the quoted total (or `total` when given, for a wrong-total probe). On 200
 * it records the order under `key` in the run; any other reply is returned as it is, so a refusal can be asserted.
 */
export async function book(s: Session, run: RunState, key: string, b: Booking, total?: number): Promise<Reply> {
  const guest = !!b.guest;
  const q = await quote(s, run, b);
  // A quote the window refuses prices nothing: send 0 so the create answers with its own refusal keys.
  const price = total ?? (Number.isFinite(q.total) ? q.total : 0);
  const reply = await call(s, 'POST', '/api/Order/CreateOrder', {
    customerName: `E2E ${b.customer}`, customerEmail: emailOf(run, b.customer), customerPhone: `+420777${rand(6)}`,
    customerAddress: guest ? { street: 'Václavské náměstí 1', city: 'Praha', zipCode: '11000', countryId: run.countryId, state: null } : null,
    savedAddressId: guest ? null : (b.addressId ?? null), selectedPackageIds: [], selectedServiceIds: q.serviceIds,
    rooms: b.rooms, bathrooms: b.bathrooms, extras: Object.fromEntries((b.extras ?? []).map((x) => [x, true])),
    cleaningDate: b.slot.toISOString(), paymentType: b.payment === 'cash' ? 1 : 2, currencyId: null, totalPrice: price, language: 'en',
    termsAccepted: true, dirtinessLevel: b.level, earlyPerformanceRequested: true, saveCard: !!b.saveCard,
    specialInstructions: `E2E run ${run.runId} ${key}`, accessInstructions: b.accessInstructions ?? null,
  });
  if (reply.status === 200) {
    run.orders[key] = {
      id: str(reply.body, 'id'), slot: b.slot.toISOString(), total: price, payment: b.payment, customer: b.customer, crew: b.crew,
      createdAt: new Date().toISOString(), level: b.level,
      ...(guest ? { guestToken: str(reply.body, 'guestAccessToken') } : {}),
    };
    saveRun(run);
  }
  return reply;
}

/** Pays a card booking's Stripe TEST Checkout (CreateOrder's stripeSessionId) and waits until the order reads Paid; returns the order. */
export async function payCard(page: Page, customer: Session, run: RunState, key: string, checkoutUrl: string) {
  const o = run.orders[key];
  await payStripeCheckout(page, checkoutUrl, emailOf(run, o.customer ?? 'customer'));
  const read = async () => (await call(customer, 'GET', `/api/Order/GetById?OrderId=${o.id}`)).body;
  return poll(read, (v) => named(v, 'paymentStatus', PAYMENT_STATUS) === 'Paid');
}

/** The partner profile body a cleaner saves (UpdateEmployee): complete, so only the documents stand before approval. */
export const cleanerProfile = (run: RunState, lastName: string) => ({
  employeeId: null, firstName: 'E2E', lastName, birthDate: '1990-05-15', street: 'Karlovo náměstí 10', city: 'Praha', zipCode: '12000',
  countryId: run.countryId, state: null, nationalityId: run.countryId, phone: `+420777${rand(6)}`, passportId: `E2E${rand(6)}CZ`, entityType: 1,
  registrationNumber: '12345670', legalEntityName: null, emergencyName: null, emergencyPhone: null, consent: true,
});

/** The work contract text a take accepts (GET api/Order/GetWorkContractPreview). */
export async function contractText(s: Session, orderId: string) {
  return str((await call(s, 'GET', `/api/Order/GetWorkContractPreview?OrderId=${orderId}`)).body, 'legalDocumentTextId');
}

/** POST api/Order/TakeOrder with the contract text (read first when not given). */
export async function take(s: Session, orderId: string, textId?: string) {
  return call(s, 'POST', '/api/Order/TakeOrder', { orderId, acceptedWorkContractTextId: textId ?? (await contractText(s, orderId)) });
}

export const start = (s: Session, orderId: string) => call(s, 'POST', '/api/Order/StartOrder', { orderId });

/** An after photo (photoType 2), a distinct PNG each time. */
export const afterPhoto = (s: Session, orderId: string, note = 'E2E') =>
  call(s, 'POST', '/api/Order/UploadPhoto', { orderId, photoType: 2, fileName: 'after.png', contentType: 'image/png', fileData: png(), notes: note });

export const complete = (s: Session, orderId: string, notes = 'E2E') => call(s, 'POST', '/api/Order/CompleteOrder', { orderId, completionNotes: notes });
export const markCash = (s: Session, orderId: string) => call(s, 'POST', '/api/Order/MarkCashCollected', { orderId });
export const reportNotPaid = (s: Session, orderId: string) => call(s, 'POST', '/api/Order/ReportCashNotPaid', { orderId });

/** The customer's cancellation preview (fee, rate, tier, oopsWindowMinutes, refund). */
export const preview = (s: Session, orderId: string) => call(s, 'GET', `/api/Order/CancellationPreview?orderId=${orderId}`);
export const cancel = (s: Session, orderId: string, reason = 'E2E') => call(s, 'POST', '/api/Order/Cancel', { orderId, reason });

/** A run account's notification feed, newest first: the mobile hosts serve it (api/Notification/Paged). */
export async function feed(run: RunState, role: Role, host: Extract<Host, 'customer-mobile' | 'partner-mobile'>) {
  const s = await signIn(host, emailOf(run, role), run.password);
  return rows((await call(s, 'GET', '/api/Notification/Paged?Offset=0&Limit=50')).body);
}

/** The admin notification feed as one admin role sees it (shared DEV-wide: filter by the run's order ids). */
export async function adminFeed(run: RunState, role: AdminRole) {
  return rows((await call(await cachedAdminAs(role, run), 'GET', '/api/AdminNotification/get-paged?Offset=0&Limit=50')).body);
}

/** The pay period open now: { id, startDate, endDate }, or undefined when DEV has none. */
export async function openPeriod() {
  const periods = rows((await call(await cachedAdmin(), 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
  const open = periods.find((p) => str(p, 'status') === 'Open' || str(p, 'status.name') === 'Open');
  return open ? { id: str(open, 'id'), startDate: str(open, 'startDate'), endDate: str(open, 'endDate') } : undefined;
}

/** A cleaner's pay rows in one period (orderPays), read as that cleaner. */
export async function periodPays(cleaner: Session, employeeId: string, periodId: string) {
  return list((await call(cleaner, 'GET', `/api/EmployeePayroll/GetPeriodPays?EmployeeId=${employeeId}&PayPeriodId=${periodId}`)).body, 'orderPays');
}

/** Polls the admin invoice list until the cleaner's invoice of the period appears or `ms` runs out (a minute apart). */
export async function waitInvoice(employeeId: string, periodId: string, ms: number) {
  return poll(
    async () => rows((await call(await cachedAdmin(), 'GET', `/api/AdminInvoice/get-paged?Offset=0&Limit=10&Filter.EmployeeId=${employeeId}&Filter.PayPeriodId=${periodId}`)).body)[0],
    Boolean,
    ms,
    60_000,
  );
}

/**
 * Runs the DEV-only confirm SQL through Execute SQL Script (scripts/confirm-accounts.sh), then proves each account
 * confirmed by signing it in on its host: the script confirms only the e2e accounts still unconfirmed, so another
 * dispatch (another stage's, a mobile lane's) may have confirmed one first, and its e-mail is then not in this log
 * (F-087). Throws on a dispatch that failed and on an account that still opens no session; returns the dispatch's output.
 */
export async function confirmAccounts(password: string, accounts: [Host, string][]) {
  const script = join(__dirname, '..', '..', 'scripts', 'confirm-accounts.sh');
  const out = execFileSync('bash', [script], { encoding: 'utf8', timeout: 18 * 60_000 });
  for (const [host, email] of accounts) await signIn(host, email, password);
  return out;
}
