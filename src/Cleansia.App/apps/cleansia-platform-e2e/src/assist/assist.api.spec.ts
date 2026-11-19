import { expect, test } from '@playwright/test';
import { call, list, signIn, str } from '../support/dev';
import { afterPhoto, confirmAccounts } from '../support/flows';
import { emailOf, loadRun } from '../support/run';
import { cachedAdmin } from '../support/web';

// Stage 'assist': what a mobile lane cannot do in its app, done through the API for the lane's own run (E2E_RUN_ID).
// One E2E_ASSIST mode per invocation; every other test skips.
//   confirm   the DEV-only confirm SQL, for accounts registered in the app (E2E_ASSIST_EMAIL: a customer that must then sign in)
//   address   a saved address for E2E_ASSIST_EMAIL (the emulator's map cannot pick one)
//   photo     an after photo on E2E_ASSIST_ORDER as the run's cleaner (the iOS camera gap, F-037/F-040)
//   evidence  E2E_ASSIST_EMAIL's consents, its register audit row and its saved cards (E2E_ASSIST_EXPECT=card|nocard)
const mode = process.env['E2E_ASSIST'] ?? '';
const email = process.env['E2E_ASSIST_EMAIL'] ?? '';
const orderId = process.env['E2E_ASSIST_ORDER'] ?? '';
const cards = process.env['E2E_ASSIST_EXPECT'] ?? '';
if (!['confirm', 'address', 'photo', 'evidence'].includes(mode)) throw new Error('E2E_ASSIST must be confirm, address, photo or evidence');
if (cards && !['card', 'nocard'].includes(cards)) throw new Error('E2E_ASSIST_EXPECT must be card or nocard');
const run = loadRun();
const needs = (want: string, ...vars: [string, string][]) => {
  test.skip(mode !== want, `E2E_ASSIST=${mode}`);
  for (const [name, value] of vars) if (!value) throw new Error(`E2E_ASSIST=${want} needs ${name}`);
};

test.describe('assist', () => {
  test('confirm: the DEV-only SQL confirms the accounts registered in the app, and the named customer signs in', async () => {
    needs('confirm');
    test.setTimeout(20 * 60_000);
    // Another dispatch may have confirmed the account first (F-087): the sign-in, not this dispatch's log, proves it.
    const out = await confirmAccounts(run.password, email ? [['customer', email]] : []);
    console.log(out);
    expect(out).toContain('completed success');
  });

  test('address: the account gets a saved address in Prague', async () => {
    needs('address', ['E2E_ASSIST_EMAIL', email]);
    const s = await signIn('customer', email, run.password);
    const r = await call(s, 'POST', '/api/SavedAddress/Add', {
      label: 'Home', street: 'Václavské náměstí 1', city: 'Praha', zipCode: '11000', countryId: run.countryId, setAsDefault: true, latitude: 50.0811, longitude: 14.428,
    });
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    console.log(`saved address ${str(r.body, 'id')} for ${email}`);
  });

  test('photo: the run\'s cleaner uploads an after photo on the order', async () => {
    needs('photo', ['E2E_ASSIST_ORDER', orderId]);
    const s = await signIn('partner-mobile', emailOf(run, 'cleaner'), run.password);
    const r = await afterPhoto(s, orderId, `E2E run ${run.runId} assist`);
    expect(r.status, JSON.stringify(r.body)).toBe(200);
  });

  const cardTags = cards === 'card' ? ['@E2E-PAY-022'] : cards === 'nocard' ? ['@E2E-PAY-023'] : [];
  test('evidence: the app registration wrote both consents and its audit row, and the saved cards are as expected', { tag: ['@E2E-AUTH-002', ...cardTags] }, async () => {
    needs('evidence', ['E2E_ASSIST_EMAIL', email]);
    const s = await signIn('customer', email, run.password);
    const userId = str(s.login?.body, 'userId');
    // ConsentType 0 is TermsOfService, 1 is PrivacyPolicy; each granted and covering the text in force.
    const consents = list((await call(s, 'GET', '/api/v1/Gdpr/consents')).body, '');
    for (const type of [0, 1]) expect(consents).toContainEqual(expect.objectContaining({ consentType: type, isGranted: true, coversCurrentVersion: true }));
    // The register row is written for an anonymous actor: it may carry the new user as the actor or as the resource.
    const audit = async (by: string) =>
      list((await call(await cachedAdmin(), 'GET', `/api/CustomerAudit/get-paged?Offset=0&Limit=10&Filter.Action=customer.account.register&Filter.${by}=${userId}`)).body, 'data');
    const register = [...(await audit('UserId')), ...(await audit('ResourceId'))];
    expect(register.map((r) => str(r, 'action'))).toContain('customer.account.register');
    const saved = list((await call(s, 'GET', '/api/SavedCard/GetMine')).body, '');
    console.log(`saved cards of ${email}: ${JSON.stringify(saved)}`);
    // Without E2E_ASSIST_EXPECT the cards are printed, not asserted.
    if (cards) expect(saved.map((c) => [str(c, 'brand').toLowerCase(), str(c, 'last4')])).toEqual(cards === 'card' ? [['visa', '4242']] : []);
  });
});
