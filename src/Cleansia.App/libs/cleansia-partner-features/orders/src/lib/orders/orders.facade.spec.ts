import { TestBed } from '@angular/core/testing';
import {
  OrderStatus,
  PartnerClient,
  StartOrderCommand,
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { Actions } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { EMPTY, Subject, of } from 'rxjs';
import {
  WorkContractDialogComponent,
  WorkContractDialogMode,
  WorkContractDialogOutcome,
  WorkContractDialogResult,
} from '../components/work-contract-dialog';
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
  let dialogService: { open: jest.Mock };
  let dialogClose$: Subject<WorkContractDialogResult | undefined>;

  const createFacade = (): OrdersFacade => {
    TestBed.configureTestingModule({
      providers: [
        OrdersFacade,
        { provide: PartnerClient, useValue: { orderClient, employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: dialogService },
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
    dialogClose$ = new Subject<WorkContractDialogResult | undefined>();
    dialogService = { open: jest.fn().mockReturnValue({ onClose: dialogClose$ }) };
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it('opens the contract dialog in take mode for the clicked order', () => {
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);

    expect(dialogService.open).toHaveBeenCalledTimes(1);
    expect(dialogService.open.mock.calls[0][0]).toBe(WorkContractDialogComponent);
    expect(dialogService.open.mock.calls[0][1].data).toEqual({
      mode: WorkContractDialogMode.Take,
      orderId: ORDER_ID,
    });
    expect(orderClient.takeOrder).not.toHaveBeenCalled();
  });

  it('confirms and reloads both lists exactly once when the dialog reports the take', () => {
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);
    dialogClose$.next({ outcome: WorkContractDialogOutcome.Accepted });

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.orders.order_taken_success'
    );
    expect(loadedListKeys(dispatch)).toEqual(['available', 'my']);
  });

  it('reloads both lists once when the take was refused, so the row cannot be clicked again', () => {
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);
    dialogClose$.next({ outcome: WorkContractDialogOutcome.Refused });

    expect(loadedListKeys(dispatch)).toEqual(['available', 'my']);
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
  });

  it('reloads nothing when the dialog is dismissed', () => {
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);
    dialogClose$.next(undefined);

    expect(loadedListKeys(dispatch)).toEqual([]);
    expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
  });

  it('clears the in-flight marker when the dialog closes so a later take is still possible', () => {
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);
    dialogClose$.next({ outcome: WorkContractDialogOutcome.Refused });

    expect(facade.takeInFlightOrderId()).toBeNull();

    facade.takeOrder(ORDER_ID);

    expect(dialogService.open).toHaveBeenCalledTimes(2);
  });

  it('ignores a second click while the dialog is open', () => {
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);
    facade.takeOrder(ORDER_ID);

    expect(dialogService.open).toHaveBeenCalledTimes(1);
    expect(facade.takeInFlightOrderId()).toBe(ORDER_ID);
  });

  it('marks only the row being taken as in flight', () => {
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);

    expect(facade.isTakeInFlight(ORDER_ID)).toBe(true);
    expect(facade.isTakeInFlight('other-order')).toBe(false);
  });

  it('never opens the dialog when the employee is unknown', () => {
    employeeClient.getCurrentEmployee.mockReturnValue(of(null));
    const facade = createFacade();

    facade.takeOrder(ORDER_ID);

    expect(dialogService.open).not.toHaveBeenCalled();
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

      // Mirrors OrderAvailability.OfferableStatuses: started is not over, and the
      // hasAvailableSpots term is what keeps a fully crewed job off the list.
      expect(availableStatuses()).toEqual([
        OrderStatus.New,
        OrderStatus.Confirmed,
        OrderStatus.OnTheWay,
        OrderStatus.InProgress,
      ]);
    });

    it('does not ask for the dead Pending status', () => {
      const facade = createFacade();

      facade.loadAvailableOrders();

      expect(availableStatuses()).not.toContain(OrderStatus.Pending);
    });
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a start with the order id', () => {
      const facade = createFacade();
      orderClient.startOrder.mockReturnValue(of({}));

      facade.startOrder(ORDER_ID);

      const command: StartOrderCommand = orderClient.startOrder.mock.calls[0][0];
      expect(command).toBeInstanceOf(StartOrderCommand);
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID });
    });
  });
});
