import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  CreateReceivablePayLinkResponse,
  CustomerClient,
  MyReceivableDto,
} from '@cleansia/customer-services';
import { of, Subject, throwError } from 'rxjs';
import { AmountDueFacade } from './amount-due.facade';

describe('AmountDueFacade', () => {
  let facade: AmountDueFacade;
  let receivableClient: { getMine: jest.Mock; createPayLink: jest.Mock };

  const lateCancellation = MyReceivableDto.fromJS({
    id: 'rcv-1',
    orderId: 'ord-1',
    displayOrderNumber: 'CL-1001',
    kind: { value: 1, name: 'CashCancellationFee' },
    amount: 450,
    currencyCode: 'CZK',
    createdOn: '2026-09-28T10:00:00Z',
  });

  const lockout = MyReceivableDto.fromJS({
    id: 'rcv-2',
    orderId: 'ord-2',
    displayOrderNumber: 'CL-1002',
    kind: { value: 2, name: 'Lockout' },
    amount: 1200,
    currencyCode: 'EUR',
    createdOn: '2026-09-28T11:00:00Z',
  });

  function payLink(checkoutUrl: string): CreateReceivablePayLinkResponse {
    return CreateReceivablePayLinkResponse.fromJS({ receivableId: 'rcv-1', checkoutUrl });
  }

  function configure(platform: 'server' | 'browser' = 'server'): void {
    const { origin, pathname } = window.location;
    receivableClient = {
      getMine: jest.fn().mockReturnValue(of([lateCancellation, lockout])),
      createPayLink: jest.fn().mockReturnValue(of(payLink(`${origin}${pathname}#pay-rcv-1`))),
    };

    TestBed.configureTestingModule({
      providers: [
        AmountDueFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { receivableClient } },
      ],
    });

    facade = TestBed.inject(AmountDueFacade);
  }

  beforeEach(() => configure());

  describe('what is owed', () => {
    it('lists every open amount with its kind, order, amount and currency', () => {
      facade.init(null);

      expect(facade.rows()).toEqual([
        {
          id: 'rcv-1',
          orderId: 'ord-1',
          displayOrderNumber: 'CL-1001',
          kindLabelKey: 'pages.amount_due.kind.cash_cancellation_fee',
          amount: 450,
          currencyCode: 'CZK',
        },
        {
          id: 'rcv-2',
          orderId: 'ord-2',
          displayOrderNumber: 'CL-1002',
          kindLabelKey: 'pages.amount_due.kind.lockout',
          amount: 1200,
          currencyCode: 'EUR',
        },
      ]);
      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('names the two other kinds, and falls back to the plain title for a kind this build does not know', () => {
      receivableClient.getMine.mockReturnValue(
        of([
          MyReceivableDto.fromJS({ ...lateCancellation.toJSON(), id: 'a', kind: { value: 3 } }),
          MyReceivableDto.fromJS({ ...lateCancellation.toJSON(), id: 'b', kind: { value: 4 } }),
          MyReceivableDto.fromJS({ ...lateCancellation.toJSON(), id: 'c', kind: { value: 99 } }),
        ]),
      );

      facade.init(null);

      expect(facade.rows().map((row) => row.kindLabelKey)).toEqual([
        'pages.amount_due.kind.unpaid_cash',
        'pages.amount_due.kind.top_up',
        'pages.amount_due.title',
      ]);
    });

    it("shows only the order's own amounts on that order's page", () => {
      facade.init('ord-2');

      expect(facade.rows().map((row) => row.id)).toEqual(['rcv-2']);
    });

    it('is loading until the amounts arrive', () => {
      const pending = new Subject<MyReceivableDto[]>();
      receivableClient.getMine.mockReturnValue(pending);

      facade.init(null);

      expect(facade.loading()).toBe(true);
      pending.next([]);
      pending.complete();
      expect(facade.loading()).toBe(false);
      expect(facade.rows()).toEqual([]);
    });

    it('says the read failed rather than claiming nothing is owed', () => {
      receivableClient.getMine.mockReturnValue(throwError(() => new Error('offline')));

      facade.init(null);

      expect(facade.hasError()).toBe(true);
      expect(facade.rows()).toEqual([]);
      expect(facade.loading()).toBe(false);
    });

    it('keeps the order filter on a retry that succeeds', () => {
      receivableClient.getMine.mockReturnValueOnce(throwError(() => new Error('offline')));
      facade.init('ord-1');

      facade.load();

      expect(facade.hasError()).toBe(false);
      expect(facade.rows().map((row) => row.id)).toEqual(['rcv-1']);
    });
  });

  describe('paying', () => {
    it('opens the pay link for the amount and hands the browser to it after the caller is told', () => {
      TestBed.resetTestingModule();
      configure('browser');
      facade.init(null);
      const beforeLeaving = jest.fn();

      facade.pay('rcv-1', beforeLeaving);

      expect(receivableClient.createPayLink).toHaveBeenCalledWith('rcv-1');
      expect(beforeLeaving).toHaveBeenCalledTimes(1);
      expect(window.location.hash).toBe('#pay-rcv-1');
      expect(facade.payingId()).toBe('rcv-1');
    });

    it('stays put and re-reads what is owed when the server refuses, leaving the refusal to the interceptor', () => {
      facade.init(null);
      receivableClient.createPayLink.mockReturnValue(
        throwError(() => ({ errors: { ReceivableId: 'receivable.not_open' } })),
      );
      receivableClient.getMine.mockClear();
      receivableClient.getMine.mockReturnValue(of([lockout]));
      const beforeLeaving = jest.fn();

      facade.pay('rcv-1', beforeLeaving);

      expect(beforeLeaving).not.toHaveBeenCalled();
      expect(facade.payingId()).toBeNull();
      expect(receivableClient.getMine).toHaveBeenCalledTimes(1);
      expect(facade.rows().map((row) => row.id)).toEqual(['rcv-2']);
    });

    it('opens one pay link while the first is still being made', () => {
      facade.init(null);
      receivableClient.createPayLink.mockReturnValue(new Subject<CreateReceivablePayLinkResponse>());

      facade.pay('rcv-1', jest.fn());
      facade.pay('rcv-2', jest.fn());

      expect(receivableClient.createPayLink).toHaveBeenCalledTimes(1);
      expect(facade.payingId()).toBe('rcv-1');
    });
  });
});
