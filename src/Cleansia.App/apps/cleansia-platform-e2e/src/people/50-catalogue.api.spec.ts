import { expect, test } from '@playwright/test';
import { anonymous, at, call, errorKeys, list, named, payStripeCheckout, png, poll, Session, signIn, str } from '../support/dev';
import { serviceIds } from '../support/flows';
import { emailOf, flag, loadRun, RunState, saveRun, scratch } from '../support/run';
import { cachedAdmin, cachedAdminAs } from '../support/web';

// Stage 'people', file 50 (Day 1, after the web onboarding of people/40): the Manager creates the throwaway
// two-cleaner service and its pay config (BRIEF §2.5), E (cleaner3) completes the profile and the documents, has a spare
// document rejected with a note (AUTH-045), accepts the legal documents and is approved, and the debtor and the cash-cap
// customer save a card in Stripe test Checkout. E2E_SCRATCH=1 builds the service but creates nothing.
test.describe.configure({ mode: 'serial' });

const run = loadRun();
// One property's refusals arrive joined in one string ("a; b"): split them so a key is looked for on its own.
const keys = (body: unknown) => errorKeys(body).flatMap((k) => k.split('; '));
const BATHROOM = 'Bathroom Cleaning';
const DOC_TYPE = ['', 'IdentityCard', 'Passport', 'DriversLicense', 'WorkPermit', 'Contract', 'Certificate'];
const DOC_STATUS = ['', 'Pending', 'Approved', 'Rejected'];
const CONTRACT = ['', 'Pending', 'Active', 'Terminated', 'Approved', 'Rejected'];
const NOTE = 'The certificate is not readable, upload it again';
const rand = (digits: number) => String(Math.floor(Math.random() * 10 ** digits)).padStart(digits, '0');
const employeeE = () => run.ids?.['cleaner3Employee'] ?? '';
const upload = (type: number, name: string) => ({
  documentType: type, file: { fileName: name, base64Content: `data:image/png;base64,${png()}`, contentType: 'image/png' }, description: `E2E run ${run.runId}`,
});

/** The partner profile body a cleaner saves (as the book stage's cleaner does). */
const profile = (r: RunState, lastName: string) => ({
  employeeId: null, firstName: 'E2E', lastName, birthDate: '1990-05-15', street: 'Karlovo náměstí 10', city: 'Praha', zipCode: '12000',
  countryId: r.countryId, state: null, nationalityId: r.countryId, phone: `+420777${rand(6)}`, passportId: `E2E${rand(6)}CZ`, entityType: 1,
  registrationNumber: '12345670', legalEntityName: null, emergencyName: null, emergencyPhone: null, consent: true,
});

let e: Session;
const cleanerE = async () => (e ??= await signIn('partner', emailOf(run, 'cleaner3'), run.password));
const myDocs = async () => list((await call(await cleanerE(), 'GET', '/api/Employee/GetMyDocuments')).body, 'documents');

test.describe('people: catalogue and E', () => {
  test('the Manager creates the two-cleaner service (119 min + 1 per room) and its platform pay config, and the catalogue offers it', async () => {
    test.skip(!!run.serviceId && !!run.payConfigId, `the service ${run.serviceId} and pay config ${run.payConfigId} exist`);
    // Built from Bathroom Cleaning on every run; a scratch run (no Manager) reads as the published administrator and stops there.
    const manager = scratch() ? await cachedAdmin() : await cachedAdminAs('manager', run);
    const [bathroomId] = await serviceIds(await anonymous('customer'), run, [BATHROOM]);
    const bathroom = (await call(manager, 'GET', `/api/AdminService/details/${bathroomId}`)).body;
    const prices = (bathroom as { prices?: Record<string, { basePrice: number }> }).prices ?? {};
    const translations = (bathroom as { translations?: Record<string, unknown> }).translations ?? {};
    expect(Object.keys(prices)).toContain('CZK');
    expect(Object.keys(translations).length).toBeGreaterThan(0);
    const name = `E2E two-cleaner ${run.runId}`;
    const service = {
      categoryId: str(bathroom, 'categoryId'), name, description: `E2E run ${run.runId}: 2 seats from 121 booked minutes`, estimatedTime: 119, minutesPerRoom: 1,
      prices: Object.fromEntries(Object.entries(prices).map(([code, p]) => [code, { basePrice: code === 'CZK' ? 500 : p.basePrice, perRoomPrice: 0 }])),
      translations: Object.fromEntries(Object.keys(translations).map((lang) => [lang, { name, description: 'E2E' }])),
    };
    const configs = list((await call(manager, 'GET', `/api/AdminPayConfig/get-paged?Offset=0&Limit=50&Filter.ServiceId=${bathroomId}`)).body, 'data');
    const platform = configs.find((c) => !str(c, 'employeeId') && str(c, 'currencyCode') === 'CZK');
    expect(platform, 'Bathroom Cleaning has a platform CZK pay config').toBeTruthy();
    test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run creates no service (its body and pay config were read)');
    if (!run.serviceId) {
      const created = await call(manager, 'POST', '/api/AdminService/create', service);
      expect(created.status, JSON.stringify(created.body)).toBe(200);
      run.serviceId = str(created.body, 'serviceId');
      saveRun(run);
    }
    const pay = await call(manager, 'POST', '/api/AdminPayConfig/create', {
      employeeId: null, serviceId: run.serviceId, packageId: null, basePay: Number(str(platform, 'basePay')), extraPerRoom: Number(str(platform, 'extraPerRoom')),
      extraPerBathroom: Number(str(platform, 'extraPerBathroom')), minimumPay: Number(str(platform, 'minimumPay')), maximumPay: Number(str(platform, 'maximumPay')),
      currencyId: str(platform, 'currencyId'), description: `E2E run ${run.runId}`,
    });
    expect(pay.status, JSON.stringify(pay.body)).toBe(200);
    run.payConfigId = str(pay.body, 'payConfigId');
    saveRun(run);
    // Without the pay config the catalogue withholds the service; with it the market offers it at 500.
    const offered = await poll(
      async () => ((await call(await anonymous('customer'), 'GET', `/api/Service/GetOverview?countryId=${run.countryId}`)).body as unknown[]).find((s) => str(s, 'id') === run.serviceId),
      Boolean,
      60_000,
    );
    expect(offered, `${name} in the CZ catalogue`).toBeTruthy();
  });

  test('E completes the profile and uploads an identity card and a certificate; the admin approves the card and rejects the certificate with a note, which E reads and replaces', { tag: ['@E2E-AUTH-045'] }, async () => {
    test.skip(!employeeE(), 'people/30 records E (cleaner3) after the confirmation');
    const s = await cleanerE();
    if (at((await call(s, 'GET', '/api/Employee/CheckCurrentEmployee')).body, 'hasCompletedProfile') !== true) {
      const saved = await call(s, 'PUT', '/api/Employee/UpdateEmployee', profile(run, 'cleaner3'));
      expect(saved.status, JSON.stringify(saved.body)).toBe(200);
      const bank = await call(s, 'PUT', '/api/Employee/UpdateBankDetails', {
        employeeId: null, iban: 'CZ6508000000192000145399', bankCountryId: run.countryId, accountPrefix: '19', accountNumber: '2000145399',
        bankCode: '0800', swift: null, bankName: 'Test bank', holderName: 'E2E Cleaner3',
      });
      expect(bank.status, JSON.stringify(bank.body)).toBe(200);
    }
    if (!(await myDocs()).length) {
      const docs = await call(s, 'POST', '/api/Employee/SaveMyDocuments', { documents: [upload(1, 'e2e-identity-card.png'), upload(6, 'e2e-certificate.png')] });
      expect(docs.status, JSON.stringify(docs.body)).toBe(200);
    }
    const a = await cachedAdmin();
    const adminDocs = list((await call(a, 'POST', '/api/AdminEmployeeDocument/get-paged', { filter: { employeeId: employeeE(), isActive: true }, offset: 0, limit: 50 })).body, 'data');
    const card = adminDocs.find((d) => named(d, 'documentType', DOC_TYPE) === 'IdentityCard');
    const certificate = adminDocs.find((d) => named(d, 'documentType', DOC_TYPE) === 'Certificate');
    expect(card && certificate, JSON.stringify(adminDocs)).toBeTruthy();
    if (named(card, 'status', DOC_STATUS) !== 'Approved') {
      const ok = await call(a, 'POST', `/api/AdminEmployeeDocument/${str(card, 'id')}/approve`, { documentId: str(card, 'id'), notes: `E2E run ${run.runId}` });
      expect(ok.status, JSON.stringify(ok.body)).toBe(200);
    }
    if (named(certificate, 'status', DOC_STATUS) === 'Pending') {
      const no = await call(a, 'POST', `/api/AdminEmployeeDocument/${str(certificate, 'id')}/reject`, { documentId: str(certificate, 'id'), notes: NOTE });
      expect(no.status, JSON.stringify(no.body)).toBe(200);
    }
    const unknown = await call(a, 'POST', '/api/AdminEmployeeDocument/01AAAAAAAAAAAAAAAAAAAAAAAA/reject', { documentId: '01AAAAAAAAAAAAAAAAAAAAAAAA', notes: NOTE });
    expect(keys(unknown.body), JSON.stringify(unknown.body)).toContain('general.not_found');
    const mine = await myDocs();
    const myCard = mine.find((d) => named(d, 'documentType', DOC_TYPE) === 'IdentityCard');
    const myCertificate = mine.find((d) => named(d, 'documentType', DOC_TYPE) === 'Certificate');
    expect(named(myCard, 'status', DOC_STATUS)).toBe('Approved');
    if (named(myCertificate, 'status', DOC_STATUS) === 'Rejected') {
      expect(str(myCertificate, 'reviewNotes')).toBe(NOTE);
      const replaced = await call(s, 'PUT', `/api/Employee/ReplaceMyDocument/${str(myCertificate, 'documentId')}`, { file: upload(6, 'e2e-certificate-2.png').file, description: `E2E run ${run.runId} again` });
      expect(replaced.status, JSON.stringify(replaced.body)).toBe(200);
    }
    // The replacement does not touch the approved card.
    expect(named((await myDocs()).find((d) => named(d, 'documentType', DOC_TYPE) === 'IdentityCard'), 'status', DOC_STATUS)).toBe('Approved');
  });

  test('E accepts the legal documents in force and the admin approves E', async () => {
    test.skip(!employeeE(), 'people/30 records E (cleaner3) after the confirmation');
    const a = await cachedAdmin();
    test.skip(str((await call(a, 'GET', `/api/AdminEmployee/details/${employeeE()}`)).body, 'contractStatus') === 'Approved', 'E is approved already');
    const s = await cleanerE();
    const legal = (await call(s, 'GET', '/api/Employee/GetMyLegalDocuments')).body as unknown[];
    expect(legal.length).toBeGreaterThan(0);
    for (const d of legal.filter((x) => !(x as { isAccepted?: boolean }).isAccepted)) {
      const r = await call(s, 'POST', '/api/Employee/AcceptLegalDocument', { acceptedTextId: str(d, 'legalDocumentTextId') });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    const ok = await call(a, 'POST', `/api/AdminEmployee/${employeeE()}/approve`, { workCountryId: run.countryId, notes: `E2E run ${run.runId}` });
    expect(ok.status, JSON.stringify(ok.body)).toBe(200);
    expect(str((await call(a, 'GET', `/api/AdminEmployee/details/${employeeE()}`)).body, 'contractStatus')).toBe('Approved');
    expect(named((await call(s, 'GET', '/api/Employee/CheckCurrentEmployee')).body, 'contractStatus', CONTRACT)).toBe('Approved');
  });

  for (const role of ['debtor', 'cashcap'] as const) {
    test(`the ${role} customer saves a card in Stripe test Checkout`, async ({ page }) => {
      test.skip(!flag(run, 'people.confirmed'), 'people/30 confirms the run\'s new customers first');
      const s = await signIn('customer', emailOf(run, role), run.password);
      run.ids = { ...run.ids, [role]: str(s.login?.body, 'userId') };
      saveRun(run);
      const cards = async () => JSON.stringify((await call(s, 'GET', '/api/SavedCard/GetMine')).body);
      test.skip((await cards()).includes('4242'), `the ${role} has a saved card already`);
      const session = await call(s, 'POST', '/api/SavedCard/CreateCheckoutSession', { consentAccepted: true, countryId: run.countryId });
      expect(session.status, JSON.stringify(session.body)).toBe(200);
      await payStripeCheckout(page, str(session.body, 'checkoutUrl'), emailOf(run, role));
      expect(await poll(cards, (v) => v.includes('4242'))).toContain('4242');
    });
  }
});
