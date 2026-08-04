import { TestBed } from '@angular/core/testing';
import {
  OrderStatus,
  PartnerClient,
  TakeOrderResponse,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { Actions } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { EMPTY, Subject, of, throwError } from 'rxjs';
import { OrdersFacade } from './orders.facade';

const EMPLOYEE_ID = 'emp-1';
const ORDER_ID = 'ord-1';

function loadedListKeys(dispatch: jest.Mock): string[] {
  return dispatch.mock.calls
    .map(([action]) => action)
    .filter((action) => action.type === OrderActions.loadOrderPaged.type)
    .map((action) => action.listKey);
}

describe('OrdersFacade — take order', () => {
  let orderClient: { takeOrder: jest.Mock; startOrder: jest.Mock };
  let employeeClient: { getCurrentEmployee: jest.Mock };
  let snackbar: {
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };
  let dispatch: jest.Mock;

  const createFacade = (): OrdersFacade => {
    TestBed.configureTestingModule({
      providers: [
        OrdersFacade,
        { provide: PartnerClient, useValue: { orderClient, employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { open: jest.fn() } },
        { provide: TranslateService, useValue: { instant: (k: string) => k, onLangChange: EMPTY } },
        { provide: Store, useValue: { dispatch, select: () => EMPTY } },
        { provide: Actions, useValue: EMPTY },
      ],
    });

    const facade = TestBed.inject(OrdersFacade);
    dispatch.mockClear();
    return facade;
  };

  beforeEach(() => {
    jest.useFakeTimers();
    TestBed.resetTestingModule();
    dispatch = jest.fn();
    orderClient = { takeOrder: jest.fn(), startOrder: jest.fn() };
    employeeClient = {
      getCurrentEmployee: jest.fn().mockReturnValue(of({ id: EMPLOYEE_ID })),
    };
    snackbar = {
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('confirms and reloads both lists when the take succeeds', () => {
    const facade = createFacade();
    orderClient.takeOrder.mockReturnValue(
      of(TakeOrderResponse.fromJS({ orderId: ORDER_ID, employeeId: EMPLOYEE_ID }))
    );

    facade.takeOrder(ORDER_ID);

    expect(orderClient.takeOrder).toHaveBeenCalledTimes(1);
    expect(orderClient.takeOrder.mock.calls[0][0].orderId).toBe(ORDER_ID);
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.orders.order_taken_success'
    );
    expect(loadedListKeys(dispatch)).toEqual(
      expect.arrayContaining(['available', 'my'])
    );
  });

  it('reloads both lists when the take is refused, so the row cannot be clicked again', () => {
    const facade = createFacade();
    orderClient.takeOrder.mockReturnValue(
      throwError(() => new Error('order.no_available_spots'))
    );

    facade.takeOrder(ORDER_ID);

    expect(loadedListKeys(dispatch)).toEqual(
      expect.arrayContaining(['available', 'my'])
    );
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
  });

  it('clears the in-flight marker on refusal so a later take is still possible', () => {
    const facade = createFacade();
    orderClient.takeOrder.mockReturnValue(
      throwError(() => new Error('order.weekly_limit_reached'))
    );

    facade.takeOrder(ORDER_ID);

    expect(facade.takeInFlightOrderId()).toBeNull();

    orderClient.takeOrder.mockReturnValue(
      of(TakeOrderResponse.fromJS({ orderId: ORDER_ID, employeeId: EMPLOYEE_ID }))
    );
    facade.takeOrder(ORDER_ID);

    expect(orderClient.takeOrder).toHaveBeenCalledTimes(2);
  });

  it('ignores a second click while a take is still in flight', () => {
    const facade = createFacade();
    orderClient.takeOrder.mockReturnValue(new Subject<TakeOrderResponse>());

    facade.takeOrder(ORDER_ID);
    facade.takeOrder(ORDER_ID);

    expect(orderClient.takeOrder).toHaveBeenCalledTimes(1);
    expect(facade.takeInFlightOrderId()).toBe(ORDER_ID);
  });

  it('marks only the row being taken as in flight', () => {
    const facade = createFacade();
    orderClient.takeOrder.mockReturnValue(new Subject<TakeOrderResponse>());

    facade.takeOrder(ORDER_ID);

    expect(facade.isTakeInFlight(ORDER_ID)).toBe(true);
    expect(facade.isTakeInFlight('other-order')).toBe(false);
  });

  it('never calls the endpoint when the employee is unknown', () => {
    employeeClient.getCurrentEmployee.mockReturnValue(of(null));
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);

    expect(orderClient.takeOrder).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'pages.orders.employee_not_found'
    );
  });

  describe('available list query', () => {
    function availableStatuses(): number[] {
      const call = dispatch.mock.calls.find(
        ([action]) =>
          action.type === OrderActions.loadOrderPaged.type &&
          action.listKey === 'available'
      );
      return call?.[0].filter.orderStatuses as number[];
    }

    it('asks only for the statuses the server can still offer', () => {
      const facade = createFacade();

      facade.loadAvailableOrders();

      expect(availableStatuses()).toEqual([
        OrderStatus.New,
        OrderStatus.Confirmed,
      ]);
    });

    it('does not ask for the dead Pending status', () => {
      const facade = createFacade();

      facade.loadAvailableOrders();

      expect(availableStatuses()).not.toContain(OrderStatus.Pending);
    });
  });
});
