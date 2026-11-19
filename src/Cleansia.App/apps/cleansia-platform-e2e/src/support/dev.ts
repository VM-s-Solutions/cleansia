import { APIRequestContext, Frame, Page, request } from '@playwright/test';
import { crc32, deflateSync } from 'node:zlib';

// DEV only, and fixed here on purpose: no variable can point this suite at another environment.
export const HOSTS = {
  partner: 'https://api-cleansia-partner-weu-dev.azurewebsites.net',
  admin: 'https://api-cleansia-admin-weu-dev.azurewebsites.net',
  'partner-mobile': 'https://api-cleansia-partner-mobile-weu-dev.azurewebsites.net',
  customer: 'https://api-cleansia-customer-weu-dev.azurewebsites.net',
  'customer-mobile': 'https://api-cleansia-customer-mobile-weu-dev.azurewebsites.net',
} as const;
export type Host = keyof typeof HOSTS;

/** The published DEV administrator (README.md). */
export const DEV_ADMIN = { email: 'admin@cleansia.local', password: 'Admin123!' };

export interface Reply {
  status: number;
  body: unknown;
  raw: Buffer;
  headers: Record<string, string>;
}

export interface Session {
  host: Host;
  ctx: APIRequestContext;
  headers: Record<string, string>;
  login?: Reply;
}

const lastSignIn: Partial<Record<Host, number>> = {};
const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

export async function anonymous(host: Host): Promise<Session> {
  return { host, ctx: await request.newContext({ baseURL: HOSTS[host] }), headers: {} };
}

/**
 * Signs in on one host. A web host keeps its HttpOnly cookies in the context and is sent the login's CSRF token
 * as X-CSRF-Token (its body token is empty); a mobile host is sent the body token as a bearer. Throws unless a
 * confirmed session opened.
 */
export async function signIn(host: Host, email: string, password: string): Promise<Session> {
  const s = await anonymous(host);
  await paceSignIn(host);
  const login = await call(s, 'POST', host === 'admin' ? '/api/AdminAuth/Login' : '/api/Auth/Login', { email, password, rememberMe: true });
  const mobile = host.endsWith('-mobile');
  if (login.status !== 200 || !str(login.body, mobile ? 'token' : 'csrfToken') || at(login.body, 'isEmailConfirmed') !== true) {
    throw new Error(`sign-in of ${email} on ${host} failed: HTTP ${login.status} ${login.raw.toString().slice(0, 300)}`);
  }
  s.headers = mobile ? { Authorization: `Bearer ${str(login.body, 'token')}` } : { 'X-CSRF-Token': str(login.body, 'csrfToken') };
  s.login = login;
  return s;
}

/** The 'auth' rate window admits 10 anonymous calls a minute per IP (RateLimitPolicies.cs): sign-ins on one host, API or web, go 6.5 s apart. */
export async function paceSignIn(host: Host) {
  const wait = (lastSignIn[host] ?? 0) + 6_500 - Date.now();
  if (wait > 0) await sleep(wait);
  lastSignIn[host] = Date.now();
}

/** How long a 429 asks to wait: Retry-After in seconds or as an HTTP date, else 30 s; never under 1 s. */
export function retryAfterMs(header: string | undefined, now = Date.now()): number {
  const seconds = Number(header);
  const ms = header && Number.isFinite(seconds) ? seconds * 1000 : header && !Number.isNaN(Date.parse(header)) ? Date.parse(header) - now : 30_000;
  return Math.max(ms, 1_000);
}

/** One HTTP call; a 429 (a rate window, refused before any handler runs) is retried up to 3 times after its Retry-After. */
export async function call(s: Session, method: string, path: string, data?: unknown): Promise<Reply> {
  let res = await s.ctx.fetch(path, { method, headers: { Accept: 'application/json', ...s.headers }, data, failOnStatusCode: false, timeout: 90_000 });
  for (let retry = 1; res.status() === 429 && retry <= 3; retry++) {
    await sleep(retryAfterMs(res.headers()['retry-after']));
    res = await s.ctx.fetch(path, { method, headers: { Accept: 'application/json', ...s.headers }, data, failOnStatusCode: false, timeout: 90_000 });
  }
  const raw = await res.body();
  let body: unknown = null;
  try {
    body = raw.length ? JSON.parse(raw.toString()) : null;
  } catch {
    body = raw.toString();
  }
  return { status: res.status(), body, raw, headers: res.headers() };
}

/** Reads a dotted path ("data.0.id") out of a JSON reply. */
export function at(v: unknown, path: string): unknown {
  if (!path) return v;
  return path.split('.').reduce<unknown>((o, k) => (o !== null && typeof o === 'object' ? (o as Record<string, unknown>)[k] : undefined), v);
}
export const str = (v: unknown, path: string) => {
  const x = at(v, path);
  return x === undefined || x === null ? '' : String(x);
};
export const num = (v: unknown, path: string) => Number(at(v, path));
export const list = (v: unknown, path: string): unknown[] => {
  const x = at(v, path);
  return Array.isArray(x) ? x : [];
};

/** The error keys of a refusal: the values of its `errors` map, one property's several keys split where FluentValidation joins them with '; '. */
export function errorKeys(v: unknown): string[] {
  const e = at(v, 'errors');
  return e && typeof e === 'object' ? Object.values(e).flat().flatMap((k) => String(k).split('; ')) : [];
}

export const ORDER_STATUS = ['New', 'Pending', 'Confirmed', 'OnTheWay', 'InProgress', 'Completed', 'Cancelled'];
export const PAYMENT_STATUS = ['', 'Pending', 'Paid', 'Failed', 'Refunded', 'Disputed', 'PartiallyRefunded'];

/** An enum the hosts send either as a number or as { name, value }, read as its name. */
export function named(v: unknown, path: string, names: string[]): string {
  const x = at(v, path);
  if (x !== null && typeof x === 'object') return str(x, 'name');
  return typeof x === 'number' ? (names[x] ?? String(x)) : String(x ?? '');
}

/** Polls `read` until `done` holds or `ms` runs out; returns the last value read. */
export async function poll<T>(read: () => Promise<T>, done: (v: T) => boolean, ms = 120_000, every = 5_000): Promise<T> {
  const end = Date.now() + ms;
  let v = await read();
  while (!done(v) && Date.now() < end) {
    await new Promise((r) => setTimeout(r, every));
    v = await read();
  }
  return v;
}

/** A distinct 16x16 PNG, base64, so no two uploads of a run are the same image. */
export function png(): string {
  const chunk = (type: string, data: Buffer) => {
    const body = Buffer.concat([Buffer.from(type), data]);
    const out = Buffer.alloc(body.length + 8);
    out.writeUInt32BE(data.length, 0);
    body.copy(out, 4);
    out.writeUInt32BE(crc32(body), body.length + 4);
    return out;
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(16, 0);
  header.writeUInt32BE(16, 4);
  header.set([8, 2, 0, 0, 0], 8);
  const rgb = [Math.random() * 255, Math.random() * 255, Math.random() * 255].map(Math.floor);
  const rows = Buffer.concat(Array.from({ length: 16 }, () => Buffer.from([0, ...Array.from({ length: 16 }, () => rgb).flat()])));
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk('IHDR', header), chunk('IDAT', deflateSync(rows)), chunk('IEND', Buffer.alloc(0))]).toString('base64');
}

/**
 * Pays a Stripe TEST-mode hosted Checkout page (payment or setup mode) as a customer's browser does, and waits
 * for Stripe to send the browser back. Refuses any URL that is not a test-mode Checkout session. The form may sit
 * in the page or in one of its iframes, so fields are found by their accessible names in whichever frame has them.
 */
export async function payStripeCheckout(page: Page, url: string, email: string, card = '4242424242424242') {
  if (!/^https:\/\/checkout\.stripe\.com\/.*cs_test_/.test(url)) throw new Error('refusing: not a Stripe TEST checkout url');
  await page.goto(url);
  const cardBox = (f: Frame) => f.getByRole('textbox', { name: /card number/i });
  const root = await poll(
    async () => {
      for (const f of page.frames()) if (await cardBox(f).count()) return f;
      return undefined;
    },
    Boolean,
    60_000,
    500,
  );
  if (!root) throw new Error('the Stripe Checkout card field never appeared');
  const field = (name: RegExp) => root.getByRole('textbox', { name });
  if (await field(/^email$/i).isEditable()) await field(/^email$/i).fill(email);
  await cardBox(root).pressSequentially(card, { delay: 15 });
  await field(/expiration|expiry/i).pressSequentially('1234', { delay: 15 });
  await field(/security code|cvc/i).pressSequentially('123', { delay: 15 });
  if (await field(/name on card|cardholder/i).count()) await field(/name on card|cardholder/i).fill('E2E Customer');
  const country = root.getByRole('combobox', { name: /country/i });
  if (await country.count()) await country.selectOption('CZ');
  const zip = field(/zip|postal/i);
  if ((await zip.count()) && (await zip.isVisible())) await zip.fill('11000');
  await root.getByRole('button', { name: /^(Pay|Save|Set up|Confirm|Subscribe)\b/ }).filter({ hasNotText: 'Link' }).last().click();
  await page.waitForURL((u) => u.hostname !== 'checkout.stripe.com', { timeout: 90_000, waitUntil: 'commit' });
  return page.url();
}
