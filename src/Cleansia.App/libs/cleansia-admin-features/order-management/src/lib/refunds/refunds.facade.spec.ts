import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, Router } from '@angular/router';
import {
  AdminRefundClient,
  PagedDataOfRefundListItem,
  RecordRefundRepaymentResponse,
  RefundListItem,
  RefundRepaymentMethod,
  RefundState,
  RetryRefundResponse,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, Subject } from 'rxjs';
import { RefundsFacade } from './refunds.facade';

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
    canRetry: state === RefundState.NotSent,
    ...overrides,
  });
}

const page = (data: RefundListItem[]) =>
  PagedDataOfRefundListItem.fromJS({ pageNumber: 1, pageSize: 20, total: data.length, data });

describe('RefundsFacade', () => {
  let getPaged: jest.Mock;
  let retry: jest.Mock;
  let recordRepayment: jest.Mock;
  let navigate: jest.Mock;
  let confirmTranslated: jest.Mock;
  let showSuccessTranslated: jest.Mock;

  function create(query: Record<string, string> = {}): RefundsFacade {
    TestBed.configureTestingModule({
      providers: [
        RefundsFacade,
        { provide: AdminRefundClient, useValue: { getPaged, retry, recordRepayment } },
        { provide: DialogService, useValue: { confirmTranslated } },
        { provide: SnackbarService, useValue: { showSuccessTranslated } },
        { provide: Router, useValue: { navigate } },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap(query) } } },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en', onLangChange: new Subject() },
        },
      ],
    });
    return TestBed.inject(RefundsFacade);
  }

  beforeEach(() => {
    getPaged = jest.fn().mockReturnValue(of(page([refund(RefundState.NotSent)])));
    retry = jest.fn().mockReturnValue(of(RetryRefundResponse.fromJS({ refundId: 'refund-1' })));
    recordRepayment = jest.fn().mockReturnValue(of(RecordRefundRepaymentResponse.fromJS({ refundId: 'refund-1' })));
    navigate = jest.fn();
    showSuccessTranslated = jest.fn();
    confirmTranslated = jest.fn().mockReturnValue(of(true));
  });

  it('opens on Needs action: Not sent, Failed and Bounced, first page, newest first', () => {
    const facade = create();

    facade.init();

    expect(getPaged).toHaveBeenCalledWith(
      [RefundState.NotSent, RefundState.Failed, RefundState.Bounced],
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      0,
      20
    );
    expect(facade.refunds()).toHaveLength(1);
    expect(facade.initialLoading()).toBe(false);
  });

  it('goes back to page 1 when the state changes', () => {
    const facade = create();
    facade.init();
    facade.onPageChange({ first: 40, rows: 20 } as never);

    facade.selectState(RefundState.Repaid);

    expect(getPaged).toHaveBeenLastCalledWith([RefundState.Repaid], undefined, undefined, undefined, undefined, undefined, 0, 20);
  });

  it('filters to the customer or the order named in the address, and clearing it shows all', () => {
    const forCustomer = create({ userId: 'user-1' });
    forCustomer.init();
    expect(getPaged.mock.calls[0][1]).toBe('user-1');

    TestBed.resetTestingModule();
    const forOrder = create({ orderId: 'order-9' });
    forOrder.init();
    expect(getPaged.mock.calls[1][2]).toBe('order-9');

    forOrder.clearScope();
    expect(getPaged.mock.calls[2][2]).toBeUndefined();
    expect(navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: {} }));
  });

  it('retries once after the confirm, holds the busy flag while it runs, and reloads after', () => {
    const facade = create();
    facade.init();
    const pending = new Subject<RetryRefundResponse>();
    retry.mockReturnValue(pending);

    facade.retry(refund(RefundState.NotSent));

    expect(retry).toHaveBeenCalledTimes(1);
    expect(retry.mock.calls[0][0]).toEqual(expect.objectContaining({ orderId: 'order-1', refundId: 'refund-1' }));
    expect(facade.submitting()).toBe(true);
    pending.next(RetryRefundResponse.fromJS({ refundId: 'refund-1' }));
    pending.complete();
    expect(facade.submitting()).toBe(false);
    expect(getPaged).toHaveBeenCalledTimes(2);
  });

  it('does not retry when the confirm is declined', () => {
    confirmTranslated.mockReturnValue(of(false));
    const facade = create();

    facade.retry(refund(RefundState.NotSent));

    expect(retry).not.toHaveBeenCalled();
  });

  it('records a transfer once with its reference, holds the busy flag, closes the panel and reloads', () => {
    const facade = create();
    facade.init();
    const pending = new Subject<RecordRefundRepaymentResponse>();
    recordRepayment.mockReturnValue(pending);
    facade.startRepayment(refund(RefundState.Failed));
    expect(facade.canRecordTransfer()).toBe(false);

    facade.setReference('  VS-2026-11 ');
    facade.recordTransfer();

    expect(recordRepayment).toHaveBeenCalledTimes(1);
    expect(recordRepayment.mock.calls[0][0]).toEqual(
      expect.objectContaining({ refundId: 'refund-1', method: RefundRepaymentMethod.BankTransfer, reference: 'VS-2026-11' })
    );
    expect(facade.submitting()).toBe(true);
    pending.next(RecordRefundRepaymentResponse.fromJS({ refundId: 'refund-1' }));
    pending.complete();
    expect(facade.repaying()).toBeNull();
    expect(getPaged).toHaveBeenCalledTimes(2);
  });

  it('allows credit only for a customer with an account once the admin ticks that they chose it', () => {
    const facade = create();

    facade.startRepayment(refund(RefundState.Bounced, { hasAccount: false }));
    facade.setCreditChosen(true);
    expect(facade.canRecordCredit()).toBe(false);

    facade.startRepayment(refund(RefundState.Bounced));
    expect(facade.canRecordCredit()).toBe(false);
    facade.setCreditChosen(true);
    expect(facade.canRecordCredit()).toBe(true);

    facade.recordCredit();
    expect(recordRepayment.mock.calls[0][0]).toEqual(
      expect.objectContaining({ method: RefundRepaymentMethod.Credit, reference: undefined })
    );
  });

  it('opens the order of a row and the dispute of a dispute refund', () => {
    const facade = create();

    facade.viewOrder(refund(RefundState.Failed));
    facade.openDispute(refund(RefundState.NotSent, { disputeId: 'dispute-1' }));

    expect(navigate).toHaveBeenCalledWith([CleansiaAdminRoute.ORDER_MANAGEMENT, 'order-1']);
    expect(navigate).toHaveBeenCalledWith([CleansiaAdminRoute.DISPUTE_MANAGEMENT, 'dispute-1']);
  });

  it('copies a Stripe refund id to the clipboard and says so', () => {
    const writeText = jest.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    const facade = create();

    facade.copyStripeRefundId('re_123');

    expect(writeText).toHaveBeenCalledWith('re_123');
    expect(showSuccessTranslated).toHaveBeenCalledWith('pages.refunds.stripe_refund_copied');
  });
});
