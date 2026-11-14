import { expect, test } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';
import { anonymous, call, DEV_ADMIN, named, ORDER_STATUS, PAYMENT_STATUS, payStripeCheckout, png, poll, Session, signIn, str } from '../support/dev';
import { emailOf, loadRun, RunState, saveRun } from '../support/run';
import { adminSignIn, cachedAdmin, customerSignIn, partnerSignIn, placeOrderInWizard, SITES } from '../support/web';
import { nextSlots } from '../support/slots';

// Stage 'book', web half: browsers on the deployed customer and partner webs and on the admin SPA served locally.
// It runs on the run the API half started (or the run E2E_RUN_ID names) with its own people, so the API half's jobs
// stay untouched: a web customer who books through the wizard and a web cleaner who is approved in the admin SPA,
// takes the jobs and, in 'execute', works them.
test.describe.configure({ mode: 'serial' });

let run: RunState;
let customer: Session;
let slots: Date[];
const rand = (digits: number) => String(Math.floor(Math.random() * 10 ** digits)).padStart(digits, '0');
const orderOf = (key: string) => run.orders[key];

test.beforeAll(() => {
  run = loadRun();
});

async function readOrder(id: string) {
  return (await call(customer, 'GET', `/api/Order/GetById?OrderId=${id}`)).body;
}

/** Books one order through the wizard, pays it in Stripe test Checkout when it is a card order, and records it in the run. */
async function bookOnWeb(page: import('@playwright/test').Page, key: string, slot: Date, payment: 'card' | 'cash') {
  const id = await placeOrderInWizard(page, run, slot, payment);
  run.orders[key] = { id, slot: slot.toISOString(), total: 0, payment };
  saveRun(run);
  if (payment === 'card') {
    await payStripeCheckout(page, page.url(), emailOf(run, 'webcustomer'));
    await page.waitForURL(/\/checkout\/success/, { timeout: 60_000 });
    await expect(page.locator('main')).toContainText('Payment received');
    const paid = await poll(() => readOrder(id), (v) => named(v, 'paymentStatus', PAYMENT_STATUS) === 'Paid');
    expect(named(paid, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
  } else {
    await expect(page.locator('main')).toContainText('Booking confirmed');
  }
  const body = await readOrder(id);
  orderOf(key).number = str(body, 'displayOrderNumber') || str(body, 'orderNumber');
  orderOf(key).total = Number(str(body, 'totalPrice'));
  saveRun(run);
  expect(orderOf(key).number).toMatch(/^ORD-/);
  return body;
}

test.describe('people', () => {
  test('a web customer and a second cleaner register, the DEV-only SQL confirms them, and the paperwork is filed', async ({ page }) => {
    test.setTimeout(20 * 60_000);
    const anon = await anonymous('customer');
    run.countryId ??= str(((await call(anon, 'GET', '/api/Country/GetServiced')).body as unknown[]).find((c) => str(c, 'isoCode') === 'CZE' || str(c, 'isoAlpha2') === 'CZ'), 'id');
    for (const [host, path, role, last] of [['customer', '/api/Auth/Register', 'webcustomer', 'Web Customer'], ['partner', '/api/Auth/RegisterEmployee', 'cleaner2', 'Applicant']] as const) {
      const body = { email: emailOf(run, role), password: run.password, firstName: 'E2E', lastName: last, language: 'en', countryId: run.countryId, termsAccepted: true };
      const r = await call(await anonymous(host), 'POST', path, body);
      if (!(run.employee2Id && role === 'cleaner2')) expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    saveRun(run);
    const out = execFileSync('bash', [join(__dirname, '..', '..', 'scripts', 'confirm-accounts.sh'), emailOf(run, 'webcustomer')], { encoding: 'utf8', timeout: 18 * 60_000 });
    expect(out).toContain(emailOf(run, 'webcustomer'));

    customer = await signIn('customer', emailOf(run, 'webcustomer'), run.password);
    const addr = await call(customer, 'POST', '/api/SavedAddress/Add', { label: 'Home', street: 'Václavské náměstí 1', city: 'Praha', zipCode: '11000', countryId: run.countryId, setAsDefault: true, latitude: 50.0811, longitude: 14.428 });
    expect(addr.status, JSON.stringify(addr.body)).toBe(200);
    run.webAddressId = str(addr.body, 'id');
    const session = await call(customer, 'POST', '/api/SavedCard/CreateCheckoutSession', { consentAccepted: true, countryId: run.countryId });
    expect(session.status, JSON.stringify(session.body)).toBe(200);
    await payStripeCheckout(page, str(session.body, 'checkoutUrl'), emailOf(run, 'webcustomer'));
    expect(JSON.stringify(await poll(async () => (await call(customer, 'GET', '/api/SavedCard/GetMine')).body, (v) => JSON.stringify(v).includes('4242')))).toContain('4242');

    if (!run.employee2Id) {
      const cleaner2 = await signIn('partner', emailOf(run, 'cleaner2'), run.password);
      run.employee2Id = str((await call(cleaner2, 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
      saveRun(run);
      const profile = await call(cleaner2, 'PUT', '/api/Employee/UpdateEmployee', {
        employeeId: null, firstName: 'E2E', lastName: 'Applicant', birthDate: '1991-06-16', street: 'Karlovo náměstí 10', city: 'Praha',
        zipCode: '12000', countryId: run.countryId, state: null, nationalityId: run.countryId, phone: `+420777${rand(6)}`,
        passportId: `E2E${rand(6)}CZ`, entityType: 1, registrationNumber: '12345670', legalEntityName: null, emergencyName: null,
        emergencyPhone: null, consent: true,
      });
      expect(profile.status, JSON.stringify(profile.body)).toBe(200);
      const bank = await call(cleaner2, 'PUT', '/api/Employee/UpdateBankDetails', {
        employeeId: null, iban: 'CZ6508000000192000145399', bankCountryId: run.countryId, accountPrefix: '19', accountNumber: '2000145399',
        bankCode: '0800', swift: null, bankName: 'Test bank', holderName: 'E2E Applicant',
      });
      expect(bank.status, JSON.stringify(bank.body)).toBe(200);
      const docs = await call(cleaner2, 'POST', '/api/Employee/SaveMyDocuments', {
        documents: [{ documentType: 1, file: { fileName: 'e2e-identity-card.png', base64Content: `data:image/png;base64,${png()}`, contentType: 'image/png' }, description: `E2E run ${run.runId}` }],
      });
      expect(docs.status, JSON.stringify(docs.body)).toBe(200);
      for (const d of (await call(cleaner2, 'GET', '/api/Employee/GetMyLegalDocuments')).body as unknown[]) {
        const a = await call(cleaner2, 'POST', '/api/Employee/AcceptLegalDocument', { acceptedTextId: str(d, 'legalDocumentTextId') });
        expect(a.status, JSON.stringify(a.body)).toBe(200);
      }
    }
  });

  test('the admin signs in on the admin web, reads the application, approves the identity card and the cleaner', { tag: ['@E2E-AUTH-052', '@E2E-AUTH-043', '@E2E-AUTH-044', '@E2E-AUTH-046'] }, async ({ page }) => {
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await page.goto(`${SITES.admin}/employee-management/${run.employee2Id}`);
    await expect(page.getByText('E2E Applicant').first()).toBeVisible();
    await expect(page.locator('main')).toContainText(emailOf(run, 'cleaner2'));
    await expect(page.locator('main')).toContainText('Pending');
    await expect(page.locator('main')).toContainText('e2e-identity-card.png');
    await page.locator('button', { hasText: /^\s*Approve\s*$/ }).click();
    await expect(page.getByText('Document approved successfully')).toBeVisible();
    await page.getByRole('button', { name: /Approve Partner/ }).click();
    const dialog = page.locator('.p-dialog');
    await dialog.locator('.p-select').click();
    await page.getByRole('option', { name: /\(CZE\)/ }).click();
    await dialog.locator('textarea').fill(`E2E run ${run.runId}`);
    await dialog.getByRole('button', { name: 'Approve Partner' }).click();
    await expect(dialog).toBeHidden();
    await page.reload();
    await expect(page.locator('main')).toContainText('Approved');
    const detail = await call(await cachedAdmin(), 'GET', `/api/AdminEmployee/details/${run.employee2Id}`);
    expect(str(detail.body, 'contractStatus')).toBe('Approved');
  });
});

test.describe('customer web', () => {
  test.beforeAll(async () => {
    customer = await signIn('customer', emailOf(run, 'webcustomer'), run.password);
    slots = nextSlots(new Date(), 4);
  });

  test('the customer books by card in the wizard, pays in Stripe test Checkout and reads the paid order', { tag: ['@E2E-AUTH-009', '@E2E-AUTH-014', '@E2E-BOOK-011', '@E2E-BOOK-028', '@E2E-BOOK-048', '@E2E-BOOK-065', '@E2E-PAY-002'] }, async ({ page }) => {
    await customerSignIn(page, emailOf(run, 'webcustomer'), run.password);
    expect(await page.evaluate(() => document.cookie)).not.toMatch(/token|jwt|auth/i);
    await page.reload();
    await expect(page).toHaveURL(/\/orders/);
    await bookOnWeb(page, 'webCard', slots[0], 'card');
    await expect(page.locator('main')).toContainText(`CZK ${orderOf('webCard').total} paid`);
    await page.goto(`${SITES.customer}/orders/${orderOf('webCard').id}`);
    const main = page.locator('main');
    await expect(main).toContainText(orderOf('webCard').number as string);
    await expect(main).toContainText('Card · Paid');
    await expect(main).toContainText(`CZK ${orderOf('webCard').total}`);
    await expect(main).toContainText('New');
    await expect(page.getByRole('button', { name: 'Cancel booking' })).toBeVisible();
  });

  test('the customer books cash with the saved card in the wizard', { tag: ['@E2E-BOOK-031', '@E2E-TAKE-002'] }, async ({ page }) => {
    await customerSignIn(page, emailOf(run, 'webcustomer'), run.password);
    await bookOnWeb(page, 'webCash', slots[1], 'cash');
    const body = await readOrder(orderOf('webCash').id);
    expect(named(body, 'paymentStatus', PAYMENT_STATUS)).toBe('Pending');
    expect(named(body, 'status', ORDER_STATUS) || named(body, 'orderStatus', ORDER_STATUS)).toBe('New');
  });

  test('the customer books two more paid card jobs, to be cancelled in settle', { tag: ['@E2E-BOOK-028'] }, async ({ page }) => {
    await customerSignIn(page, emailOf(run, 'webcustomer'), run.password);
    await bookOnWeb(page, 'webCancelCustomer', slots[2], 'card');
    await bookOnWeb(page, 'webCancelAdmin', slots[3], 'card');
  });
});

test.describe('partner web', () => {
  test('the cleaner reads the board without the household and takes both jobs with the contract', { tag: ['@E2E-TAKE-001', '@E2E-TAKE-002', '@E2E-TAKE-004', '@E2E-TAKE-010', '@E2E-TAKE-013', '@E2E-TAKE-016'] }, async ({ page }) => {
    customer = await signIn('customer', emailOf(run, 'webcustomer'), run.password);
    await partnerSignIn(page, emailOf(run, 'cleaner2'), run.password);
    for (const key of ['webCard', 'webCash']) {
      const o = orderOf(key);
      await page.goto(`${SITES.partner}/orders`);
      const row = page.locator('tr', { hasText: o.number });
      await expect(row).toBeVisible();
      const text = await row.innerText();
      expect(text).not.toMatch(/Václavské|e2e\.|@example|\+420/);
      expect(text).toContain('New');
      if (key === 'webCard') expect(text).toContain('Paid');
      await row.locator('button.action-btn').click();
      const dialog = page.getByRole('dialog');
      await expect(dialog).toContainText('Contract for Work');
      await expect(dialog).toContainText(o.number as string);
      const accept = dialog.getByRole('button', { name: 'Accept and take the job' });
      await expect(accept).toBeDisabled();
      await dialog.locator('input[type=checkbox]').check({ force: true });
      await accept.click();
      await expect(page.getByText('Order taken successfully!')).toBeVisible();
      o.taken = true;
      saveRun(run);
      await expect(page.locator('tr', { hasText: o.number }).filter({ hasText: 'Confirmed' })).toBeVisible();
      const seen = await readOrder(o.id);
      expect(named(seen, 'status', ORDER_STATUS) || named(seen, 'orderStatus', ORDER_STATUS)).toBe('Confirmed');
      if (key === 'webCard') expect(named(seen, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
    }
  });

  test('the cleaner opens the job page, which carries the accepted contract and the start action', { tag: ['@E2E-TAKE-013'] }, async ({ page }) => {
    await partnerSignIn(page, emailOf(run, 'cleaner2'), run.password);
    await page.goto(`${SITES.partner}/orders/${orderOf('webCard').id}`);
    await expect(page.locator('main')).toContainText('You accepted the contract for work');
    await expect(page.getByRole('button', { name: 'Start Order' })).toBeVisible();
  });
});

test.describe('after the take', () => {
  test('the admin sees the seat and the accepted contract, the customer sees the order confirmed', { tag: ['@E2E-TAKE-014', '@E2E-TAKE-015'] }, async ({ page, browser }) => {
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await page.goto(`${SITES.admin}/order-management/${orderOf('webCard').id}`);
    const main = page.locator('main');
    await expect(main).toContainText('Confirmed');
    await expect(main).toContainText('E2E Applicant');
    await expect(main).toContainText(/Accepted .*v\d{4}-\d{2}-\d{2}/);
    const ctx = await browser.newContext({ locale: 'en-US' });
    const web = await ctx.newPage();
    await customerSignIn(web, emailOf(run, 'webcustomer'), run.password);
    await web.goto(`${SITES.customer}/orders/${orderOf('webCard').id}`);
    await expect(web.locator('main')).toContainText('Confirmed ·');
    await ctx.close();
  });
});
