import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { RefundListItem, RefundRepaymentMethod, RefundState } from '@cleansia/admin-services';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import {
  getRefundsTableDefinition,
  NEEDS_ACTION,
  REFUND_ORIGIN_LABEL_KEYS,
  REFUND_REPAYMENT_METHOD_LABEL_KEYS,
  REFUND_STATE_LABEL_KEYS,
  refundStateHint,
  statesFor,
} from './refunds.models';

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

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'];
const bundle = (locale: string): Record<string, unknown> =>
  JSON.parse(
    readFileSync(join(findSolutionDir(), `Cleansia.App/apps/cleansia-admin.app/src/assets/i18n/${locale}.json`), 'utf-8')
  );
const lookup = (tree: Record<string, unknown>, key: string): unknown =>
  key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], tree);

function refund(state: RefundState, overrides: Record<string, unknown> = {}): RefundListItem {
  return RefundListItem.fromJS({
    id: 'refund-1',
    orderId: 'order-1',
    displayOrderNumber: 'CL-1001',
    customerName: 'Jana Nováková',
    hasAccount: true,
    amount: 400,
    currencyCode: 'CZK',
    origin: { value: 4, name: 'PartialRefund' },
    state: { value: state, name: RefundState[state] },
    canRetry: false,
    ...overrides,
  });
}

describe('refunds models', () => {
  const translate = {
    instant: (key: string, params?: Record<string, unknown>) => (params ? `${key}:${JSON.stringify(params)}` : key),
    currentLang: 'cs',
  } as unknown as TranslateService;

  function action(tooltip: string, allowed: string[]) {
    const permissions = { hasPolicy: (policy: string) => allowed.includes(policy) } as unknown as PermissionService;
    return getRefundsTableDefinition(
      { onRetry: jest.fn(), onOpenDispute: jest.fn(), onRecordRepayment: jest.fn(), isBusy: () => false },
      translate,
      permissions,
      undefined
    ).actions.find((a) => a.tooltip === tooltip)!;
  }

  it('has a label for every state, origin and repayment method in all five locales', () => {
    const keys = [
      ...Object.values(REFUND_STATE_LABEL_KEYS),
      ...Object.values(REFUND_ORIGIN_LABEL_KEYS),
      ...Object.values(REFUND_REPAYMENT_METHOD_LABEL_KEYS),
    ];
    const missing = LOCALES.flatMap((locale) => {
      const tree = bundle(locale);
      return keys.filter((key) => typeof lookup(tree, key) !== 'string').map((key) => `${locale}: ${key}`);
    });

    expect(keys).toHaveLength(12);
    expect(missing).toEqual([]);
  });

  it('shows Retry only on a row the server marks retryable and only to a role that may issue refunds', () => {
    const permitted = action('pages.refunds.actions.retry', [Policy.CanIssueRefund]);
    const refused = action('pages.refunds.actions.retry', []);

    expect(permitted.visible?.(refund(RefundState.NotSent, { canRetry: true }))).toBe(true);
    expect(permitted.visible?.(refund(RefundState.NotSent, { canRetry: false }))).toBe(false);
    expect(refused.visible?.(refund(RefundState.NotSent, { canRetry: true }))).toBe(false);
  });

  it('shows Open dispute only on a dispute refund that was not sent', () => {
    const openDispute = action('pages.refunds.actions.open_dispute', []);

    expect(openDispute.visible?.(refund(RefundState.NotSent, { disputeId: 'dispute-1' }))).toBe(true);
    expect(openDispute.visible?.(refund(RefundState.Failed, { disputeId: 'dispute-1' }))).toBe(false);
    expect(openDispute.visible?.(refund(RefundState.NotSent))).toBe(false);
  });

  it('offers Record repayment on Failed and Bounced rows only', () => {
    const record = action('pages.refunds.actions.record_repayment', [Policy.CanIssueRefund]);

    expect(record.visible?.(refund(RefundState.Failed))).toBe(true);
    expect(record.visible?.(refund(RefundState.Bounced))).toBe(true);
    for (const state of [RefundState.NotSent, RefundState.Refunded, RefundState.Repaid, RefundState.Closed]) {
      expect(record.visible?.(refund(state))).toBe(false);
    }
    expect(action('pages.refunds.actions.record_repayment', []).visible?.(refund(RefundState.Failed))).toBe(false);
  });

  it('gives each state the one hint that says who acts next or how it was settled', () => {
    expect(
      refundStateHint(refund(RefundState.NotSent, { canRetry: true, origin: { value: 1, name: 'Cancellation' } }), translate)
    ).toBe('pages.refunds.hints.retried_hourly');
    expect(refundStateHint(refund(RefundState.NotSent, { canRetry: true }), translate)).toBe(
      'pages.refunds.hints.waiting_for_admin'
    );
    expect(refundStateHint(refund(RefundState.Bounced), translate)).toBe('pages.refunds.hints.waiting_for_admin');
    expect(
      refundStateHint(
        refund(RefundState.Repaid, {
          repaymentMethod: { value: RefundRepaymentMethod.BankTransfer, name: 'BankTransfer' },
          repaymentReference: 'VS-42',
        }),
        translate
      )
    ).toBe('pages.refunds.hints.repaid_transfer:{"reference":"VS-42"}');
    expect(
      refundStateHint(
        refund(RefundState.Repaid, { repaymentMethod: { value: RefundRepaymentMethod.Credit, name: 'Credit' } }),
        translate
      )
    ).toBe('pages.refunds.hints.repaid_credit');
    expect(refundStateHint(refund(RefundState.Refunded), translate)).toBe('');
  });

  it('reads Needs action as Not sent, Failed and Bounced, and no filter as every state', () => {
    expect(statesFor(NEEDS_ACTION)).toEqual([RefundState.NotSent, RefundState.Failed, RefundState.Bounced]);
    expect(statesFor(RefundState.Repaid)).toEqual([RefundState.Repaid]);
    expect(statesFor(null)).toBeUndefined();
  });
});
