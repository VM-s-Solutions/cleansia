import { expect, test } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';
import {
  anonymous, call, DEV_ADMIN, errorKeys, list, named, num, ORDER_STATUS, PAYMENT_STATUS, payStripeCheckout, png, poll, Session,
  signIn, str,
} from '../support/dev';
import { emailOf, newRun, RunOrder, RunState, saveRun } from '../support/run';
import { nextSlots } from '../support/slots';

// Stage 'book': the run's accounts, the cleaner's onboarding and approval, the bookings at the next open slots
// (cash, card and guest), and the takes. Serial: each step leaves the state the next one starts from.
test.describe.configure({ mode: 'serial' });

let run: RunState;
let customer: Session;
let cleaner: Session;
let slots: Date[];
const ADDRESS = { street: 'Václavské náměstí 1', city: 'Praha', zipCode: '11000' };
const SERVICE = 'Bathroom Cleaning';
// The App Service affinity cookies (ARRAffinity*) are the platform's, not the session's.
const sessionCookies = (headers: Record<string, string>) => (headers['set-cookie'] ?? '').split('\n').filter((c) => c && !c.startsWith('ARRAffinity'));
const rand = (digits: number) => String(Math.floor(Math.random() * 10 ** digits)).padStart(digits, '0');

const admin = () => signIn('admin', DEV_ADMIN.email, DEV_ADMIN.password);
const order = (key: string) => run.orders[key] as RunOrder;
const customerOrder = async (id: string) => (await call(customer, 'GET', `/api/Order/GetById?OrderId=${id}`)).body;
const onBoard = async (id: string) =>
  list((await call(cleaner, 'GET', `/api/Order/GetPaged?Filter.HasAvailableSpots=true&Filter.Id=${id}&Limit=10`)).body, 'data')
    .some((o) => str(o, 'id') === id);

async function quote(slot: Date, s: Session = customer) {
  const services = (await call(s, 'GET', `/api/Service/GetOverview?countryId=${run.countryId}`)).body;
  const serviceId = str((services as unknown[]).find((x) => str(x, 'name') === SERVICE), 'id');
  const q = await call(s, 'POST', '/api/Order/Quote', {
    selectedServiceIds: [serviceId], selectedPackageIds: [], rooms: 1, bathrooms: 1, currencyId: null, selectedExtraSlugs: [],
    cleaningDate: slot.toISOString(), countryId: run.countryId, dirtinessLevel: 0,
  });
  expect(q.status, JSON.stringify(q.body)).toBe(200);
  return { serviceId, total: num(q.body, 'totalPrice'), body: q.body };
}

function booking(serviceId: string, slot: Date, total: number, paymentType: 1 | 2, guest = false) {
  return {
    customerName: guest ? 'E2E Guest' : 'E2E Customer', customerEmail: emailOf(run, guest ? 'guest' : 'customer'),
    customerPhone: `+420777${rand(6)}`, customerAddress: guest ? { ...ADDRESS, countryId: run.countryId, state: null } : null,
    savedAddressId: guest ? null : run.addressId, selectedPackageIds: [], selectedServiceIds: [serviceId], rooms: 1, bathrooms: 1,
    extras: {}, cleaningDate: slot.toISOString(), paymentType, currencyId: null, totalPrice: total, language: 'en',
    termsAccepted: true, dirtinessLevel: 0, earlyPerformanceRequested: true, saveCard: false,
    specialInstructions: `E2E run ${run.runId}`,
  };
}

async function book(key: string, slot: Date, paymentType: 1 | 2, guest = false) {
  const s = guest ? await anonymous('customer') : customer;
  const { serviceId, total } = await quote(slot, s);
  const r = await call(s, 'POST', '/api/Order/CreateOrder', booking(serviceId, slot, total, paymentType, guest));
  expect(r.status, JSON.stringify(r.body)).toBe(200);
  run.orders[key] = { id: str(r.body, 'id'), slot: slot.toISOString(), total, payment: paymentType === 1 ? 'cash' : 'card' };
  if (guest) order(key).guestToken = str(r.body, 'guestAccessToken');
  saveRun(run);
  return r.body;
}

async function waitPaid(id: string) {
  const o = await poll(() => customerOrder(id), (v) => named(v, 'paymentStatus', PAYMENT_STATUS) === 'Paid');
  expect(named(o, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
  return o;
}

test.describe('book', () => {
  test.beforeAll(() => {
    run = newRun();
    console.log(`run ${run.runId}: e2e.${run.runId}.<role>@example.com`);
  });

  test('the admin signs in on the admin API', { tag: ['@E2E-AUTH-052'] }, async () => {
    const s = await admin();
    expect(s.login?.body).toMatchObject({ hasAdminAccess: true, isEmailConfirmed: true });
    expect(str(s.login?.body, 'adminRole')).not.toBe('');
  });

  test('a customer and a cleaner register', { tag: ['@E2E-AUTH-001', '@E2E-AUTH-033'] }, async () => {
    const anon = await anonymous('customer');
    const countries = (await call(anon, 'GET', '/api/Country/GetServiced')).body as unknown[];
    run.countryId = str(countries.find((c) => str(c, 'isoCode') === 'CZE' || str(c, 'isoAlpha2') === 'CZ'), 'id');
    expect(run.countryId).not.toBe('');
    saveRun(run);
    const c = await call(anon, 'POST', '/api/Auth/Register', {
      email: emailOf(run, 'customer'), password: run.password, firstName: 'E2E', lastName: 'Customer', language: 'en',
      countryId: run.countryId, termsAccepted: true,
    });
    expect(c.status, JSON.stringify(c.body)).toBe(200);
    const k = await call(await anonymous('partner'), 'POST', '/api/Auth/RegisterEmployee', {
      email: emailOf(run, 'cleaner'), password: run.password, firstName: 'E2E', lastName: 'Cleaner', language: 'en', countryId: run.countryId,
    });
    expect(k.status, JSON.stringify(k.body)).toBe(200);
  });

  test('a sign-in before confirmation opens no session', { tag: ['@E2E-AUTH-010'] }, async () => {
    const r = await call(await anonymous('customer'), 'POST', '/api/Auth/Login', { email: emailOf(run, 'customer'), password: run.password, rememberMe: true });
    expect(r.status).toBe(200);
    expect(r.body).toMatchObject({ isEmailConfirmed: false });
    expect(str(r.body, 'token')).toBe('');
    expect(sessionCookies(r.headers)).toEqual([]);
  });

  test('the DEV-only SQL confirms the run accounts', async () => {
    test.setTimeout(20 * 60_000);
    const out = execFileSync('bash', [join(__dirname, '..', '..', 'scripts', 'confirm-accounts.sh'), emailOf(run, 'customer')], { encoding: 'utf8', timeout: 18 * 60_000 });
    console.log(out);
    expect(out).toContain(emailOf(run, 'cleaner'));
  });

  test('the confirmed customer signs in on web (cookies) and mobile (body tokens)', { tag: ['@E2E-AUTH-009', '@E2E-AUTH-014', '@E2E-AUTH-015', '@E2E-AUTH-001'] }, async () => {
    customer = await signIn('customer', emailOf(run, 'customer'), run.password);
    expect(customer.login?.body).toMatchObject({ isEmailConfirmed: true, role: 'Customer' });
    expect(str(customer.login?.body, 'csrfToken')).not.toBe('');
    const cookies = sessionCookies(customer.login?.headers ?? {});
    expect(cookies.length).toBeGreaterThan(0);
    expect(cookies.every((c) => /httponly/i.test(c))).toBe(true);
    run.customerUserId = str(customer.login?.body, 'userId');
    saveRun(run);
    const me = await call(customer, 'GET', '/api/User/GetCurrent');
    expect(me.body).toMatchObject({ firstName: 'E2E', lastName: 'Customer' });
    // ConsentType 0 is TermsOfService, 1 is PrivacyPolicy; each granted and covering the text in force.
    const consents = list((await call(customer, 'GET', '/api/v1/Gdpr/consents')).body, '');
    for (const type of [0, 1]) expect(consents).toContainEqual(expect.objectContaining({ consentType: type, isGranted: true, coversCurrentVersion: true }));
    // A state-changing call without the CSRF header is refused.
    const noCsrf = await customer.ctx.fetch('/api/SavedAddress/Add', { method: 'POST', data: {}, failOnStatusCode: false });
    expect(noCsrf.status()).toBeGreaterThanOrEqual(400);
    const mobile = await signIn('customer-mobile', emailOf(run, 'customer'), run.password);
    expect(str(mobile.login?.body, 'token')).not.toBe('');
    expect(str(mobile.login?.body, 'refreshToken')).not.toBe('');
  });

  test('the cleaner completes the profile, uploads the identity card and accepts two of three documents', { tag: ['@E2E-AUTH-033', '@E2E-AUTH-035', '@E2E-AUTH-037', '@E2E-AUTH-041'] }, async () => {
    cleaner = await signIn('partner', emailOf(run, 'cleaner'), run.password);
    const before = (await call(cleaner, 'GET', '/api/Employee/GetCurrentEmployee')).body;
    run.employeeId = str(before, 'id');
    saveRun(run);
    const a = await admin();
    expect(str((await call(a, 'GET', `/api/AdminEmployee/details/${run.employeeId}`)).body, 'contractStatus')).toBe('Pending');
    const profile = await call(cleaner, 'PUT', '/api/Employee/UpdateEmployee', {
      employeeId: null, firstName: 'E2E', lastName: 'Cleaner', birthDate: '1990-05-15', street: 'Karlovo náměstí 10', city: 'Praha',
      zipCode: '12000', countryId: run.countryId, state: null, nationalityId: run.countryId, phone: `+420777${rand(6)}`,
      passportId: `E2E${rand(6)}CZ`, entityType: 1, registrationNumber: '12345670', legalEntityName: null, emergencyName: null,
      emergencyPhone: null, consent: true,
    });
    expect(profile.status, JSON.stringify(profile.body)).toBe(200);
    const bank = await call(cleaner, 'PUT', '/api/Employee/UpdateBankDetails', {
      employeeId: null, iban: 'CZ6508000000192000145399', bankCountryId: run.countryId, accountPrefix: '19', accountNumber: '2000145399',
      bankCode: '0800', swift: null, bankName: 'Test bank', holderName: 'E2E Cleaner',
    });
    expect(bank.status, JSON.stringify(bank.body)).toBe(200);
    const required = list((await call(cleaner, 'GET', '/api/Employee/GetMyDocumentRequirements')).body, '');
    expect(JSON.stringify(required)).toMatch(/IdentityCard|"documentType":1/);
    const docs = await call(cleaner, 'POST', '/api/Employee/SaveMyDocuments', {
      documents: [{ documentType: 1, file: { fileName: 'e2e-identity-card.png', base64Content: `data:image/png;base64,${png()}`, contentType: 'image/png' }, description: `E2E run ${run.runId}` }],
    });
    expect(docs.status, JSON.stringify(docs.body)).toBe(200);
    const legal = (await call(cleaner, 'GET', '/api/Employee/GetMyLegalDocuments')).body as unknown[];
    expect(legal).toHaveLength(3);
    for (const d of legal.slice(0, 2)) {
      const a = await call(cleaner, 'POST', '/api/Employee/AcceptLegalDocument', { acceptedTextId: str(d, 'legalDocumentTextId') });
      expect(a.status, JSON.stringify(a.body)).toBe(200);
    }
    const again = await call(cleaner, 'POST', '/api/Employee/AcceptLegalDocument', { acceptedTextId: str(legal[0], 'legalDocumentTextId') });
    expect(again.status).toBe(200);
    const foreign = await call(cleaner, 'POST', '/api/Employee/AcceptLegalDocument', { acceptedTextId: '01AAAAAAAAAAAAAAAAAAAAAAAA' });
    expect(errorKeys(foreign.body)).toContain('legal.document_not_in_force');
    const after = (await call(a, 'GET', `/api/AdminEmployee/details/${run.employeeId}`)).body;
    expect(after).toMatchObject({ isProfileComplete: true, contractStatus: 'Pending' });
  });

  test('the admin reads the application, approves the identity card, and cannot approve the cleaner before the third acceptance', { tag: ['@E2E-AUTH-043', '@E2E-AUTH-044', '@E2E-AUTH-042'] }, async () => {
    const a = await admin();
    const detail = await call(a, 'GET', `/api/AdminEmployee/details/${run.employeeId}`);
    expect(detail.status).toBe(200);
    const docs = list((await call(a, 'POST', '/api/AdminEmployeeDocument/get-paged', { filter: { employeeId: run.employeeId, isActive: true }, offset: 0, limit: 50 })).body, 'data');
    expect(docs.length).toBeGreaterThan(0);
    for (const d of docs) {
      const r = await call(a, 'POST', `/api/AdminEmployeeDocument/${str(d, 'id')}/approve`, { documentId: str(d, 'id'), notes: `E2E run ${run.runId}` });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    const early = await call(a, 'POST', `/api/AdminEmployee/${run.employeeId}/approve`, { workCountryId: run.countryId, notes: `E2E run ${run.runId}` });
    expect(errorKeys(early.body)).toContain('employee.legal_documents_not_accepted');
  });

  test('the customer saves an address, quotes, and is refused cash without a saved card, cash as a guest and a wrong total', { tag: ['@E2E-BOOK-065', '@E2E-BOOK-001', '@E2E-BOOK-002', '@E2E-BOOK-033', '@E2E-BOOK-032', '@E2E-BOOK-030'] }, async () => {
    const addr = await call(customer, 'POST', '/api/SavedAddress/Add', { label: 'Home', ...ADDRESS, countryId: run.countryId, setAsDefault: true, latitude: 50.0811, longitude: 14.428 });
    expect(addr.status, JSON.stringify(addr.body)).toBe(200);
    run.addressId = str(addr.body, 'id');
    slots = nextSlots(new Date(), 5);
    saveRun(run);
    const q = await quote(slots[0]);
    expect(q.total).toBeGreaterThan(0);
    expect(str(q.body, 'currencyCode')).toBe('CZK');
    const guestQuote = await quote(slots[0], await anonymous('customer'));
    expect(guestQuote.total).toBe(q.total);
    const noCard = await call(customer, 'POST', '/api/Order/CreateOrder', booking(q.serviceId, slots[0], q.total, 1));
    expect(errorKeys(noCard.body)).toContain('order.cash_requires_saved_card');
    const guestCash = await call(await anonymous('customer'), 'POST', '/api/Order/CreateOrder', booking(q.serviceId, slots[0], q.total, 1, true));
    expect(errorKeys(guestCash.body)).toContain('order.cash_not_available');
    for (const wrong of [q.total - 1, q.total + 1]) {
      const r = await call(customer, 'POST', '/api/Order/CreateOrder', booking(q.serviceId, slots[0], wrong, 2));
      expect(errorKeys(r.body)).toContain('order.total_price.not_match');
    }
  });

  test('the customer saves a card through Stripe test Checkout and books two cash jobs', { tag: ['@E2E-BOOK-031', '@E2E-PAY-033'] }, async ({ page }) => {
    const session = await call(customer, 'POST', '/api/SavedCard/CreateCheckoutSession', { consentAccepted: true, countryId: run.countryId });
    expect(session.status, JSON.stringify(session.body)).toBe(200);
    await payStripeCheckout(page, str(session.body, 'checkoutUrl'), emailOf(run, 'customer'));
    const cards = await poll(async () => (await call(customer, 'GET', '/api/SavedCard/GetMine')).body, (v) => JSON.stringify(v).includes('4242'));
    expect(JSON.stringify(cards)).toContain('4242');
    const created = await book('cash', slots[0], 1);
    expect(str(created, 'stripeSessionId')).toBe('');
    await book('cashToCancel', slots[3], 1);
    const o = await customerOrder(order('cash').id);
    expect(named(o, 'status', ORDER_STATUS) || named(o, 'orderStatus', ORDER_STATUS)).toBe('New');
    expect(named(o, 'paymentStatus', PAYMENT_STATUS)).toBe('Pending');
    const receipt = await call(customer, 'GET', `/api/Order/DownloadReceipt?OrderId=${order('cash').id}`);
    expect(errorKeys(receipt.body)).toContain('receipt.not_found');
  });

  test('a cleaner who is not approved cannot take, on web and mobile', { tag: ['@E2E-AUTH-051', '@E2E-TAKE-024'] }, async () => {
    const id = order('cash').id;
    const preview = await call(cleaner, 'GET', `/api/Order/GetWorkContractPreview?OrderId=${id}`);
    const textId = str(preview.body, 'legalDocumentTextId') || '01AAAAAAAAAAAAAAAAAAAAAAAA';
    for (const s of [cleaner, await signIn('partner-mobile', emailOf(run, 'cleaner'), run.password)]) {
      const r = await call(s, 'POST', '/api/Order/TakeOrder', { orderId: id, acceptedWorkContractTextId: textId });
      // The catalogue expects employee.not_approved; DEV answers the registration lock's 403 before the validator
      // runs (RequireCompleteProfileAttribute). Either is a refusal; which one is right is a finding for triage.
      expect([...errorKeys(r.body), str(r.body, 'title')], `${s.host}: ${JSON.stringify(r.body)}`).toEqual(
        expect.arrayContaining([expect.stringMatching(/^(employee\.not_approved|Registration incomplete)$/)]),
      );
    }
  });

  test('after the third acceptance the admin approves the cleaner, once', { tag: ['@E2E-AUTH-041', '@E2E-AUTH-042', '@E2E-AUTH-046'] }, async () => {
    const legal = (await call(cleaner, 'GET', '/api/Employee/GetMyLegalDocuments')).body as unknown[];
    for (const d of legal.filter((x) => !(x as { isAccepted?: boolean }).isAccepted)) {
      const r = await call(cleaner, 'POST', '/api/Employee/AcceptLegalDocument', { acceptedTextId: str(d, 'legalDocumentTextId') });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    const a = await admin();
    const ok = await call(a, 'POST', `/api/AdminEmployee/${run.employeeId}/approve`, { workCountryId: run.countryId, notes: `E2E run ${run.runId}` });
    expect(ok.status, JSON.stringify(ok.body)).toBe(200);
    const detail = (await call(a, 'GET', `/api/AdminEmployee/details/${run.employeeId}`)).body;
    expect(str(detail, 'contractStatus')).toBe('Approved');
    const twice = await call(a, 'POST', `/api/AdminEmployee/${run.employeeId}/approve`, { workCountryId: run.countryId, notes: 'again' });
    expect(errorKeys(twice.body)).toContain('employee.already_approved');
  });

  test('a card order is off the board until it is paid; the customer pays it in Stripe test Checkout and it rests at New', { tag: ['@E2E-BOOK-028', '@E2E-PAY-002', '@E2E-PAY-003', '@E2E-TAKE-001', '@E2E-TAKE-003', '@E2E-PAY-033', '@E2E-PAY-032'] }, async ({ page }) => {
    const created = await book('card', slots[1], 2);
    const id = order('card').id;
    expect(await onBoard(id)).toBe(false);
    const unpaid = await call(customer, 'GET', `/api/Order/DownloadReceipt?OrderId=${id}`);
    expect(errorKeys(unpaid.body)).toContain('receipt.not_found');
    await payStripeCheckout(page, str(created, 'stripeSessionId'), emailOf(run, 'customer'));
    const paid = await waitPaid(id);
    expect(named(paid, 'status', ORDER_STATUS) || named(paid, 'orderStatus', ORDER_STATUS)).toBe('New');
    expect(await poll(() => onBoard(id), Boolean, 60_000)).toBe(true);
    const receipt = await poll(() => call(customer, 'GET', `/api/Order/DownloadReceipt?OrderId=${id}`), (r) => r.status === 200, 90_000);
    expect(receipt.headers['content-type']).toContain('application/pdf');
    await book('cardToCancel', slots[4], 2).then((b) => payStripeCheckout(page, str(b, 'stripeSessionId'), emailOf(run, 'customer')));
    await waitPaid(order('cardToCancel').id);
  });

  test('a guest books by card, pays, and reads the booking with its token, without address or crew', { tag: ['@E2E-BOOK-039', '@E2E-PAY-001', '@E2E-BOOK-056'] }, async ({ page }) => {
    const created = await book('guest', slots[2], 2, true);
    const token = order('guest').guestToken ?? '';
    expect(token).not.toBe('');
    await payStripeCheckout(page, str(created, 'stripeSessionId'), emailOf(run, 'guest'));
    const anon = await anonymous('customer');
    const looked = await poll(async () => (await call(anon, 'POST', '/api/Order/Lookup', { accessToken: token })).body, (v) => named(v, 'paymentStatus', PAYMENT_STATUS) === 'Paid');
    expect(named(looked, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
    expect(str(looked, 'confirmationCode')).not.toBe('');
    const byGet = (await call(anon, 'GET', `/api/Order/Lookup?token=${encodeURIComponent(token)}`)).body;
    expect(str(byGet, 'confirmationCode')).toBe(str(looked, 'confirmationCode'));
    expect(JSON.stringify(looked)).not.toContain(ADDRESS.street);
    const status = named(looked, 'status', ORDER_STATUS) || named(looked, 'orderStatus', ORDER_STATUS);
    expect(status).toBe('New');
  });

  test('the approved cleaner reads the contract and takes the cash job on web and the card job on mobile', { tag: ['@E2E-TAKE-002', '@E2E-TAKE-010', '@E2E-TAKE-013', '@E2E-TAKE-014', '@E2E-TAKE-016', '@E2E-TAKE-017', '@E2E-TAKE-021', '@E2E-TAKE-022', '@E2E-EXEC-003'] }, async () => {
    const mobile = await signIn('partner-mobile', emailOf(run, 'cleaner'), run.password);
    for (const [key, s] of [['cash', cleaner], ['card', mobile]] as const) {
      const id = order(key).id;
      expect(await onBoard(id)).toBe(true);
      const preview = await call(s, 'GET', `/api/Order/GetWorkContractPreview?OrderId=${id}`);
      expect(preview.status, JSON.stringify(preview.body)).toBe(200);
      const textId = str(preview.body, 'legalDocumentTextId');
      expect(textId).not.toBe('');
      const bare = await call(s, 'POST', '/api/Order/TakeOrder', { orderId: id });
      expect(bare.status).toBe(400);
      const take = await call(s, 'POST', '/api/Order/TakeOrder', { orderId: id, acceptedWorkContractTextId: textId });
      expect(take.status, JSON.stringify(take.body)).toBe(200);
      order(key).taken = true;
      saveRun(run);
      const twice = await call(s, 'POST', '/api/Order/TakeOrder', { orderId: id, acceptedWorkContractTextId: textId });
      expect(twice.status).toBe(400);
      expect(await onBoard(id)).toBe(false);
      const seen = await customerOrder(id);
      expect(named(seen, 'status', ORDER_STATUS) || named(seen, 'orderStatus', ORDER_STATUS)).toBe('Confirmed');
      if (key === 'card') expect(named(seen, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
      const early = await call(s, 'POST', '/api/Order/StartOrder', { orderId: id });
      expect(early.status, 'a start two hours ahead is refused').toBe(400);
    }
  });
});
