import { expect, test } from '@playwright/test';
import { anonymous, call, DEV_ADMIN, errorKeys, list, named, Session, signIn, str } from '../support/dev';
import { emailOf, flag, loadRun, Role, saveRun, scratch, setFlag } from '../support/run';
import { dayAfter } from '../support/slots';
import { AdminRole, cachedAdmin, cachedAdminAs } from '../support/web';

// Stage 'people', file 10 (Day 1, 07:13 UTC): the run's new people register, the Administrator creates the Manager,
// Support and Accountant, the payroll actions are tried as Support and as Accountant, and the open pay period is read
// and recorded (run.periodAtStart, run.day2). E2E_SCRATCH=1 skips the role accounts and what needs them.
test.describe.configure({ mode: 'serial' });

const run = loadRun();
// One property's refusals arrive joined in one string ("a; b"): split them so a key is looked for on its own.
const keys = (body: unknown) => errorKeys(body).flatMap((k) => k.split('; '));
const CUSTOMERS: Role[] = ['first', 'nocard', 'debtor', 'cashcap'];
const CLEANERS: Role[] = ['applicant', 'cleaner3'];
const ROLES: { role: Exclude<AdminRole, 'admin'>; value: number; name: string }[] = [
  { role: 'manager', value: 2, name: 'Manager' },
  { role: 'support', value: 3, name: 'Support' },
  { role: 'accountant', value: 4, name: 'Accountant' },
];
// The App Service affinity cookies (ARRAffinity*) are the platform's, not the session's.
const sessionCookies = (headers: Record<string, string>) => (headers['set-cookie'] ?? '').split('\n').filter((c) => c && !c.startsWith('ARRAffinity'));
const rows = (body: unknown) => list(body, 'data');
const FAKE = '01AAAAAAAAAAAAAAAAAAAAAAAA';

test.describe('people: accounts', () => {
  test('the run\'s new customers and cleaners register; a cleaner signing in before confirmation gets no session', { tag: ['@E2E-AUTH-034'] }, async () => {
    test.skip(!!flag(run, 'people.registered'), `run ${run.runId} registered its people at ${flag(run, 'people.registered')}`);
    for (const role of CUSTOMERS) {
      const r = await call(await anonymous('customer'), 'POST', '/api/Auth/Register', {
        email: emailOf(run, role), password: run.password, firstName: 'E2E', lastName: role, language: 'en', countryId: run.countryId, termsAccepted: true,
      });
      expect(r.status, `${role}: ${JSON.stringify(r.body)}`).toBe(200);
    }
    for (const role of CLEANERS) {
      const r = await call(await anonymous('partner'), 'POST', '/api/Auth/RegisterEmployee', {
        email: emailOf(run, role), password: run.password, firstName: 'E2E', lastName: role, language: 'en', countryId: run.countryId,
      });
      expect(r.status, `${role}: ${JSON.stringify(r.body)}`).toBe(200);
    }
    setFlag(run, 'people.registered');
    // AUTH-034, before: the partner host answers an unconfirmed cleaner 200 with no session at all.
    const before = await call(await anonymous('partner'), 'POST', '/api/Auth/Login', { email: emailOf(run, 'applicant'), password: run.password, rememberMe: true });
    expect(before.status).toBe(200);
    expect(before.body).toMatchObject({ isEmailConfirmed: false });
    expect(str(before.body, 'csrfToken')).toBe('');
    expect(str(before.body, 'token')).toBe('');
    expect(sessionCookies(before.headers)).toEqual([]);
  });

  test('the Administrator is refused a duplicate e-mail and an 11-character password', { tag: ['@E2E-AUTH-055'] }, async () => {
    const a = await cachedAdmin();
    const body = (email: string, password: string) => ({
      email, password, firstName: 'E2E', lastName: 'Refused', phoneNumber: null, birthDate: null, preferredLanguageCode: 'en', role: 3,
    });
    // The published administrator's e-mail is a duplicate on every run, scratch or not; nothing is created either way.
    const duplicate = await call(a, 'POST', '/api/AdminUser/create', body(DEV_ADMIN.email, `${run.password}-t`));
    expect(keys(duplicate.body), JSON.stringify(duplicate.body)).toContain('admin_user.email_exists');
    // Never 'nobody' (the never-registered sign-in of XCUT-019), in case the refusal ever fails.
    const short = await call(a, 'POST', '/api/AdminUser/create', body(`e2e.${run.runId}.shortpassword@example.com`, 'Abcdefgh123'));
    expect(keys(short.body), JSON.stringify(short.body)).toContain('auth.admin_password_too_short');
  });

  test('the Administrator creates the Manager, Support and Accountant; each signs in flagged to change the password, changes it, and is listed', { tag: ['@E2E-AUTH-055'] }, async () => {
    test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run creates no role account');
    const a = await cachedAdmin();
    const temporary = `${run.password}-t`;
    for (const { role, value, name } of ROLES) {
      if (!run.ids?.[role]) {
        const created = await call(a, 'POST', '/api/AdminUser/create', {
          email: emailOf(run, role), password: temporary, firstName: 'E2E', lastName: name, phoneNumber: null, birthDate: null, preferredLanguageCode: 'en', role: value,
        });
        expect(created.status, `${role}: ${JSON.stringify(created.body)}`).toBe(200);
        run.ids = { ...run.ids, [role]: str(created.body, 'id') };
        saveRun(run);
      }
      if (flag(run, `${role}.passwordChanged`)) continue;
      const s: Session = await signIn('admin', emailOf(run, role), temporary);
      expect(s.login?.body).toMatchObject({ adminRole: name, mustChangePassword: true, hasAdminAccess: true });
      // The flag is a UI hint: the API serves the account at once, and every later sign-in uses the run password.
      const changed = await call(s, 'POST', '/api/AdminAuth/ChangePassword', { currentPassword: temporary, newPassword: run.password });
      expect(changed.status, `${role}: ${JSON.stringify(changed.body)}`).toBe(200);
      setFlag(run, `${role}.passwordChanged`);
    }
    for (const { role, name } of ROLES) {
      const s = await cachedAdminAs(role, run);
      expect(str(s.login?.body, 'adminRole')).toBe(name);
    }
    const listed = rows((await call(a, 'GET', `/api/AdminUser/get-paged?Offset=0&Limit=20&Filter.SearchTerm=e2e.${run.runId}`)).body);
    for (const { role, name } of ROLES) {
      const row = listed.find((u) => str(u, 'email') === emailOf(run, role));
      expect(row, `${role} in the admin user list`).toBeTruthy();
      expect(str(row, 'id')).toBe(run.ids?.[role]);
      expect(named(row, 'adminRole', ['', 'Administrator', 'Manager', 'Support', 'Accountant'])).toBe(name);
    }
  });

  test('Support is refused every payroll action (403); the Accountant is never refused by role', { tag: ['@E2E-PAYOUT-052'] }, async () => {
    test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run has no Support or Accountant');
    // Made-up ids: a call the role admits is refused by its validator (400/404) and changes nothing.
    const calls: [string, string, unknown?][] = [
      ['GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=5'],
      ['GET', '/api/AdminInvoice/get-paged?Offset=0&Limit=5'],
      ['PUT', '/api/AdminInvoice/approve', { invoiceId: FAKE, adminNotes: 'E2E' }],
      ['PUT', '/api/AdminInvoice/mark-paid', { invoiceId: FAKE, bankTransferNote: 'E2E', adminNotes: 'E2E' }],
      ['PUT', '/api/AdminInvoice/cancel', { invoiceId: FAKE, reason: 'E2E' }],
      ['PUT', '/api/AdminPayroll/update-invoice-amounts', { invoiceId: FAKE, bonusAmount: 0, deductionAmount: 0, adminNotes: 'E2E' }],
      ['PUT', '/api/AdminPayroll/dispute-invoice', { invoiceId: FAKE, adminNotes: 'E2E' }],
      ['POST', '/api/AdminPayroll/generate-invoice', { employeeId: FAKE, payPeriodId: FAKE }],
    ];
    const support = await cachedAdminAs('support', run);
    const accountant = await cachedAdminAs('accountant', run);
    for (const [method, path, data] of calls) {
      const s = await call(support, method, path, data);
      expect(s.status, `Support ${method} ${path}`).toBe(403);
      const r = await call(accountant, method, path, data);
      expect(r.status, `Accountant ${method} ${path}: ${r.raw.toString().slice(0, 200)}`).not.toBe(403);
      expect([200, 400, 404, 409], `Accountant ${method} ${path}: ${r.raw.toString().slice(0, 200)}`).toContain(r.status);
      if (method !== 'GET') expect(r.status, `Accountant ${method} ${path} with a made-up id`).not.toBe(200);
    }
  });

  test('exactly one pay period is open, three days long, the rest closed or paid; the run records it and its Day 2', { tag: ['@E2E-PAYOUT-013'] }, async () => {
    // The Accountant reads it on the real run; a scratch run has none, so the published administrator does.
    const reader = scratch() ? await cachedAdmin() : await cachedAdminAs('accountant', run);
    const periods = rows((await call(reader, 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=50')).body);
    const open = periods.filter((p) => str(p, 'status') === 'Open');
    expect(open, JSON.stringify(periods.map((p) => [str(p, 'startDate'), str(p, 'status')]))).toHaveLength(1);
    const [p] = open;
    expect(str(p, 'endDate')).toBe(dayAfter(dayAfter(str(p, 'startDate'))));
    for (const other of periods.filter((x) => x !== p)) expect(['Closed', 'Paid']).toContain(str(other, 'status'));
    if (!run.periodAtStart) {
      run.periodAtStart = { id: str(p, 'id'), startDate: str(p, 'startDate'), endDate: str(p, 'endDate') };
      // The night run closes a period on the night after its end date: Day 2 (BRIEF §6, recomputed here).
      run.day2 = dayAfter(str(p, 'endDate'));
      saveRun(run);
    }
    console.log(`run ${run.runId}: open period ${run.periodAtStart.startDate}..${run.periodAtStart.endDate} (planned 2026-11-16..18), Day 2 ${run.day2}`);
  });
});
