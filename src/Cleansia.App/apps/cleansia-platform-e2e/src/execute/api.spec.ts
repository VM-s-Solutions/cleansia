import { expect, test } from '@playwright/test';
import { call, DEV_ADMIN, errorKeys, list, num, png, poll, Session, signIn, str } from '../support/dev';
import { emailOf, loadRun, RunOrder, saveRun } from '../support/run';
import { startOpensAt } from '../support/slots';

// Stage 'execute': the cleaner works the two jobs taken in 'book', on the partner mobile host, each only once its
// start window (the slot less 60 minutes) is open. Before that a job's test skips and says when it opens.
const run = loadRun();

function ready(key: 'cash' | 'card'): RunOrder {
  const o = run.orders[key];
  test.skip(!o?.taken, `the ${key} job was not taken in the book stage of run ${run.runId}`);
  test.skip(!!o.completed, `the ${key} job was completed by an earlier execute run`);
  const opens = startOpensAt(new Date(o.slot));
  test.skip(Date.now() < opens.getTime(), `the ${key} job (slot ${o.slot}) can be started from ${opens.toISOString()}; run execute again then`);
  return o;
}

const cleanerMobile = () => signIn('partner-mobile', emailOf(run, 'cleaner'), run.password);
const receipt = (s: Session, id: string) => call(s, 'GET', `/api/Order/DownloadReceipt?OrderId=${id}`);
const photo = (s: Session, id: string) =>
  call(s, 'POST', '/api/Order/UploadPhoto', { orderId: id, photoType: 2, fileName: 'after.png', contentType: 'image/png', fileData: png(), notes: `E2E run ${run.runId}` });

async function startAndRefuseEarlyComplete(s: Session, o: RunOrder) {
  const start = await call(s, 'POST', '/api/Order/StartOrder', { orderId: o.id });
  expect(start.status, JSON.stringify(start.body)).toBe(200);
  expect(num(start.body, 'newStatus')).toBe(4);
  const noPhoto = await call(s, 'POST', '/api/Order/CompleteOrder', { orderId: o.id, completionNotes: 'no after photo yet' });
  expect(errorKeys(noPhoto.body), JSON.stringify(noPhoto.body)).toContain('order.after_photos.required');
  const after = await photo(s, o.id);
  expect(after.status, JSON.stringify(after.body)).toBe(200);
}

async function completeAndReadBack(s: Session, o: RunOrder) {
  const done = await call(s, 'POST', '/api/Order/CompleteOrder', { orderId: o.id, completionNotes: `E2E run ${run.runId}` });
  expect(done.status, JSON.stringify(done.body)).toBe(200);
  expect(num(done.body, 'newStatus')).toBe(5);
  o.completed = true;
  saveRun(run);
  const admin = await signIn('admin', DEV_ADMIN.email, DEV_ADMIN.password);
  const detail = (await call(admin, 'GET', `/api/AdminOrder/details/${o.id}`)).body;
  expect(str(detail, 'orderStatus.name') || str(detail, 'status.name')).toBe('Completed');
  expect(str(detail, 'paymentStatus.name')).toBe('Paid');
  const customer = await signIn('customer-mobile', emailOf(run, 'customer'), run.password);
  const pdf = await poll(() => receipt(customer, o.id), (r) => r.status === 200, 90_000);
  expect(pdf.headers['content-type']).toContain('application/pdf');
  // The pay row is calculated off the completion's path (a queue), seconds later, in the period open then.
  expect(await poll(() => openPeriodPays(s), (ids) => ids.includes(o.id))).toContain(o.id);
}

/** The order ids of the cleaner's pay rows in the period open now. */
async function openPeriodPays(s: Session): Promise<string[]> {
  const admin = await signIn('admin', DEV_ADMIN.email, DEV_ADMIN.password);
  const periods = list((await call(admin, 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body, 'data');
  const open = periods.find((p) => str(p, 'status') === 'Open' || str(p, 'status.name') === 'Open');
  const pays = (await call(s, 'GET', `/api/EmployeePayroll/GetPeriodPays?EmployeeId=${run.employeeId}&PayPeriodId=${str(open, 'id')}`)).body;
  return list(pays, 'orderPays').map((r) => str(r, 'orderId'));
}

test.describe('execute', () => {
  test('the cash job: start, after photo, cash recorded, complete, receipt and cash held', { tag: ['@E2E-EXEC-002', '@E2E-EXEC-015', '@E2E-EXEC-026', '@E2E-EXEC-036', '@E2E-EXEC-038', '@E2E-EXEC-039', '@E2E-EXEC-049', '@E2E-PAY-034', '@E2E-PAYOUT-001', '@E2E-PAYOUT-054'] }, async () => {
    const o = ready('cash');
    const s = await cleanerMobile();
    await startAndRefuseEarlyComplete(s, o);
    const unpaid = await call(s, 'POST', '/api/Order/CompleteOrder', { orderId: o.id, completionNotes: 'cash not recorded yet' });
    expect(errorKeys(unpaid.body), JSON.stringify(unpaid.body)).toContain('order.cash_not_collected');
    const cash = await call(s, 'POST', '/api/Order/MarkCashCollected', { orderId: o.id });
    expect(cash.status, JSON.stringify(cash.body)).toBe(200);
    expect(num(cash.body, 'paymentStatus')).toBe(2);
    const customer = await signIn('customer-mobile', emailOf(run, 'customer'), run.password);
    expect(errorKeys((await receipt(customer, o.id)).body), 'no cash receipt before completion').toContain('receipt.not_found');
    await completeAndReadBack(s, o);
    const held = list((await call(s, 'GET', '/api/EmployeePayroll/GetCashHeld')).body, '').find((h) => str(h, 'currencyCode') === 'CZK');
    expect(num(held, 'amount')).toBeGreaterThanOrEqual(o.total);
    run.cashHeldAfterExecute = num(held, 'amount');
    saveRun(run);
    const admin = await signIn('admin', DEV_ADMIN.email, DEV_ADMIN.password);
    expect(JSON.stringify((await call(admin, 'GET', '/api/AdminCashHeld/get-all')).body)).toContain(run.employeeId ?? '-');
  });

  test('the card job: start, after photo, complete, both axes', { tag: ['@E2E-EXEC-002', '@E2E-EXEC-015', '@E2E-EXEC-037', '@E2E-EXEC-038', '@E2E-EXEC-050', '@E2E-PAYOUT-001'] }, async () => {
    const o = ready('card');
    const s = await cleanerMobile();
    await startAndRefuseEarlyComplete(s, o);
    await completeAndReadBack(s, o);
  });
});
