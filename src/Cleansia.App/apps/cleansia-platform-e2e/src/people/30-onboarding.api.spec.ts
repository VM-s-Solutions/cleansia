import { expect, test } from '@playwright/test';
import { call, errorKeys, list, named, png, Session, signIn, str } from '../support/dev';
import { cleanerProfile, confirmAccounts } from '../support/flows';
import { emailOf, flag, loadRun, Role, saveRun, setFlag } from '../support/run';
import { cachedAdmin } from '../support/web';

// Stage 'people', file 30 (Day 1, after people/10 and the web sign-in of people/20): one confirmation dispatch for
// every account people/10 registered, the cleaners' first sessions, and the applicant's way to a rejection: approval
// refused until the profile and the documents are complete (AUTH-047), then rejected with a reason they read (AUTH-049).
// The applicant ends Rejected and is never approved; E (cleaner3) is left with no profile for people/40's lock screen.
test.describe.configure({ mode: 'serial' });

const run = loadRun();
const CONFIRMED: Role[] = ['applicant', 'cleaner3', 'first', 'nocard', 'debtor', 'cashcap'];
const CONTRACT = ['', 'Pending', 'Active', 'Terminated', 'Approved', 'Rejected'];
const REASON = 'Photo of the identity card is unreadable';
const applicantId = () => run.ids?.['applicantEmployee'] ?? '';
const contract = async () => str((await call(await cachedAdmin(), 'GET', `/api/AdminEmployee/details/${applicantId()}`)).body, 'contractStatus');
const approve = async () => call(await cachedAdmin(), 'POST', `/api/AdminEmployee/${applicantId()}/approve`, { workCountryId: run.countryId, notes: `E2E run ${run.runId}` });

let applicant: Session;

test.describe('people: onboarding', () => {
  test('one DEV-only SQL dispatch confirms every account people/10 registered', async () => {
    test.skip(!flag(run, 'people.registered'), 'people/10 registers the run\'s new people first');
    test.skip(!!flag(run, 'people.confirmed'), `confirmed at ${flag(run, 'people.confirmed')}`);
    test.setTimeout(20 * 60_000);
    console.log(confirmAccounts());
    // Proved by signing in, not by the log: a mobile lane's own dispatch may confirm these accounts first.
    for (const role of CONFIRMED) await signIn(role === 'applicant' || role === 'cleaner3' ? 'partner' : 'customer', emailOf(run, role), run.password);
    setFlag(run, 'people.confirmed');
  });

  test('after confirmation both cleaners get a session, with the profile not complete', { tag: ['@E2E-AUTH-034'] }, async () => {
    test.skip(!flag(run, 'people.confirmed'), 'the confirmation dispatch above runs first');
    test.skip(!!flag(run, 'auth034.after'), `shown at ${flag(run, 'auth034.after')}: E's profile may be complete since`);
    for (const role of ['applicant', 'cleaner3'] as const) {
      // signIn throws unless the login opens a confirmed session (the web host's CSRF token and cookies).
      const s = await signIn('partner', emailOf(run, role), run.password);
      const check = (await call(s, 'GET', '/api/Employee/CheckCurrentEmployee')).body;
      expect(check, role).toMatchObject({ hasCompletedProfile: false });
      expect(named(check, 'contractStatus', CONTRACT), role).toBe('Pending');
      const me = (await call(s, 'GET', '/api/Employee/GetCurrentEmployee')).body;
      run.ids = { ...run.ids, [role]: str(s.login?.body, 'userId'), [`${role}Employee`]: str(me, 'id') };
      saveRun(run);
      if (role === 'applicant') applicant = s;
    }
    expect(applicantId()).not.toBe('');
    setFlag(run, 'auth034.after');
  });

  test('approval is refused while the profile is incomplete, then while the identity card is missing, pending and rejected', { tag: ['@E2E-AUTH-047'] }, async () => {
    test.skip(!applicantId(), 'the cleaners\' first sessions above record the applicant');
    const now = await contract();
    test.skip(now !== 'Pending', `the applicant is ${now} already: AUTH-047 ran on this run`);
    applicant ??= await signIn('partner', emailOf(run, 'applicant'), run.password);
    // Separate rule chains: each refusal lists every failed chain, so the key asked about is looked for, not matched alone.
    const incomplete = await approve();
    expect(errorKeys(incomplete.body), JSON.stringify(incomplete.body)).toContain('employee.profile_incomplete');
    const saved = await call(applicant, 'PUT', '/api/Employee/UpdateEmployee', cleanerProfile(run, 'applicant'));
    expect(saved.status, JSON.stringify(saved.body)).toBe(200);
    const bank = await call(applicant, 'PUT', '/api/Employee/UpdateBankDetails', {
      employeeId: null, iban: 'CZ6508000000192000145399', bankCountryId: run.countryId, accountPrefix: '19', accountNumber: '2000145399',
      bankCode: '0800', swift: null, bankName: 'Test bank', holderName: 'E2E Applicant',
    });
    expect(bank.status, JSON.stringify(bank.body)).toBe(200);
    const noCard = await approve();
    expect(errorKeys(noCard.body), JSON.stringify(noCard.body)).toContain('employee.documents_not_approved');
    expect(errorKeys(noCard.body)).not.toContain('employee.profile_incomplete');
    const upload = await call(applicant, 'POST', '/api/Employee/SaveMyDocuments', {
      documents: [{ documentType: 1, file: { fileName: 'e2e-identity-card.png', base64Content: `data:image/png;base64,${png()}`, contentType: 'image/png' }, description: `E2E run ${run.runId}` }],
    });
    expect(upload.status, JSON.stringify(upload.body)).toBe(200);
    const pending = await approve();
    expect(errorKeys(pending.body), JSON.stringify(pending.body)).toContain('employee.documents_not_approved');
    const a = await cachedAdmin();
    const docs = list((await call(a, 'POST', '/api/AdminEmployeeDocument/get-paged', { filter: { employeeId: applicantId(), isActive: true }, offset: 0, limit: 50 })).body, 'data');
    expect(docs.length).toBeGreaterThan(0);
    for (const d of docs) {
      const r = await call(a, 'POST', `/api/AdminEmployeeDocument/${str(d, 'id')}/reject`, { documentId: str(d, 'id'), notes: REASON });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    const rejectedCard = await approve();
    expect(errorKeys(rejectedCard.body), JSON.stringify(rejectedCard.body)).toContain('employee.documents_not_approved');
    expect(await contract()).toBe('Pending');
  });

  test('the admin rejects the applicant with a reason, once; the applicant may still edit and reads the reason verbatim', { tag: ['@E2E-AUTH-049'] }, async () => {
    test.skip(!applicantId(), 'the cleaners\' first sessions above record the applicant');
    const a = await cachedAdmin();
    const reject = (reason: string) => call(a, 'POST', `/api/AdminEmployee/${applicantId()}/reject`, { reason });
    if ((await contract()) !== 'Rejected') {
      const long = await reject('x'.repeat(501));
      expect(errorKeys(long.body), JSON.stringify(long.body)).toContain('common.max_length');
      expect(await contract()).toBe('Pending');
      const ok = await reject(REASON);
      expect(ok.status, JSON.stringify(ok.body)).toBe(200);
    }
    const detail = (await call(a, 'GET', `/api/AdminEmployee/details/${applicantId()}`)).body;
    expect(detail).toMatchObject({ contractStatus: 'Rejected', rejectionReason: REASON });
    const again = await reject(REASON);
    expect(errorKeys(again.body), JSON.stringify(again.body)).toContain('employee.already_rejected');
    applicant ??= await signIn('partner', emailOf(run, 'applicant'), run.password);
    const edit = await call(applicant, 'PUT', '/api/Employee/UpdateEmployee', cleanerProfile(run, 'applicant'));
    expect(edit.status, JSON.stringify(edit.body)).toBe(200);
    const check = (await call(applicant, 'GET', '/api/Employee/CheckCurrentEmployee')).body;
    expect(named(check, 'contractStatus', CONTRACT)).toBe('Rejected');
    expect(str(check, 'rejectionReason')).toBe(REASON);
  });
});
