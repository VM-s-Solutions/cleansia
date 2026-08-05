import { TestBed } from '@angular/core/testing';
import {
  AddOrderNoteCommand,
  MarkCashCollectedCommand,
  MarkCashCollectedResponse,
  OrderItem,
  OrderStatus,
  PartnerClient,
  PaymentStatus,
  PaymentType,
  ReportOrderIssueCommand,
  StartOrderCommand,
  TakeOrderCommand,
  TakeOrderResponse,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Actions } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { DialogService } from 'primeng/dynamicdialog';
import { EMPTY, Subject, of, throwError } from 'rxjs';
import { MarkCashCollectedDialogComponent } from '../components/mark-cash-collected-dialog';
import { OrderDetailsFacade } from './order-details.facade';

const EMPLOYEE_ID = 'emp-1';
const ORDER_ID = 'ord-1';

interface OrderOverrides {
  orderStatusValue?: number;
  paymentStatusValue?: number;
  paymentTypeValue?: number;
  assignedEmployeeId?: string | null;
}

function buildOrder(overrides: OrderOverrides = {}): OrderItem {
  const {
    orderStatusValue = OrderStatus.InProgress,
    paymentStatusValue = PaymentStatus.Pending,
    paymentTypeValue = PaymentType.Cash,
    assignedEmployeeId = EMPLOYEE_ID,
  } = overrides;

  return OrderItem.fromJS({
    id: ORDER_ID,
    displayOrderNumber: 'CLS-1',
    totalPrice: 1250,
    currency: { name: 'Czech koruna', code: 'CZK', symbol: 'Kč' },
    orderStatus: { type: 'order_status', name: 'InProgress', value: orderStatusValue },
    paymentStatus: { type: 'payment_status', name: 'Pending', value: paymentStatusValue },
    paymentType: { type: 'payment_type', name: 'Cash', value: paymentTypeValue },
    assignedEmployees: assignedEmployeeId
      ? [{ employeeId: assignedEmployeeId, fullName: 'Jan Novak' }]
      : [],
  });
}

describe('OrderDetailsFacade', () => {
  let orderClient: {
    markCashCollected: jest.Mock;
    takeOrder: jest.Mock;
    startOrder: jest.Mock;
    reportIssue: jest.Mock;
    addNote: jest.Mock;
    getById: jest.Mock;
  };
  let employeeClient: { getCurrentEmployee: jest.Mock };
  let snackbar: {
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
    showApiError: jest.Mock;
  };
  let dialogService: { open: jest.Mock };

  const createFacade = (): OrderDetailsFacade => {
    TestBed.configureTestingModule({
      providers: [
        OrderDetailsFacade,
        { provide: PartnerClient, useValue: { orderClient, employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: dialogService },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Store, useValue: { dispatch: jest.fn() } },
        { provide: Actions, useValue: EMPTY },
      ],
    });

    return TestBed.inject(OrderDetailsFacade);
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    orderClient = {
      markCashCollected: jest.fn(),
      takeOrder: jest.fn(),
      startOrder: jest.fn(),
      reportIssue: jest.fn(),
      addNote: jest.fn(),
      getById: jest.fn().mockReturnValue(of(buildOrder())),
    };
    employeeClient = { getCurrentEmployee: jest.fn().mockReturnValue(of(null)) };
    snackbar = {
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
      showApiError: jest.fn(),
    };
    dialogService = { open: jest.fn() };
  });

  describe('markCashCollected', () => {
    it('refreshes the order and reports success when the call succeeds', () => {
      const facade = createFacade();
      orderClient.markCashCollected.mockReturnValue(
        of(
          MarkCashCollectedResponse.fromJS({
            orderId: ORDER_ID,
            paymentStatus: PaymentStatus.Paid,
          })
        )
      );

      facade.markCashCollected(ORDER_ID);

      expect(orderClient.markCashCollected).toHaveBeenCalledTimes(1);
      expect(orderClient.markCashCollected.mock.calls[0][0].orderId).toBe(ORDER_ID);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'global.messages.orders.cash_collected'
      );
      // Success path re-reads the order so the new payment status is rendered.
      expect(orderClient.getById).toHaveBeenCalledWith(ORDER_ID);
      expect(snackbar.showApiError).not.toHaveBeenCalled();
    });

    it('surfaces the API error and does NOT refresh when the call fails', () => {
      const facade = createFacade();
      const error = new Error('order.cash_already_collected');
      orderClient.markCashCollected.mockReturnValue(throwError(() => error));

      facade.markCashCollected(ORDER_ID);

      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'global.messages.orders.cash_collect_failed'
      );
      expect(orderClient.getById).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.loading()).toBe(false);
    });

    it('never calls the endpoint without an order id', () => {
      const facade = createFacade();

      facade.markCashCollected('');

      expect(orderClient.markCashCollected).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'global.messages.orders.invalid_request'
      );
    });
  });

  describe('takeOrder', () => {
    it('confirms and re-reads the order when the take succeeds', () => {
      const facade = createFacade();
      orderClient.takeOrder.mockReturnValue(
        of(TakeOrderResponse.fromJS({ orderId: ORDER_ID, employeeId: EMPLOYEE_ID }))
      );

      facade.takeOrder(ORDER_ID);

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.orders.order_taken_success'
      );
      expect(orderClient.getById).toHaveBeenCalledWith(ORDER_ID);
    });

    it('re-reads the order when the take is refused, so the button reflects the server', () => {
      const facade = createFacade();
      orderClient.takeOrder.mockReturnValue(
        throwError(() => new Error('order.no_available_spots'))
      );

      facade.takeOrder(ORDER_ID);

      expect(orderClient.getById).toHaveBeenCalledWith(ORDER_ID);
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.loading()).toBe(false);
    });

    it('ignores a second click while a take is still in flight', () => {
      const facade = createFacade();
      orderClient.takeOrder.mockReturnValue(new Subject<TakeOrderResponse>());

      facade.takeOrder(ORDER_ID);
      facade.takeOrder(ORDER_ID);

      expect(orderClient.takeOrder).toHaveBeenCalledTimes(1);
    });

    it('never calls the endpoint without an order id', () => {
      const facade = createFacade();

      facade.takeOrder('');

      expect(orderClient.takeOrder).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'global.messages.orders.invalid_request'
      );
    });
  });

  describe('openMarkCashCollectedDialog', () => {
    it('opens the custom confirmation dialog with the order total', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      facade.currentEmployeeId.set(EMPLOYEE_ID);
      dialogService.open.mockReturnValue({ onClose: EMPTY });

      facade.openMarkCashCollectedDialog();

      expect(dialogService.open).toHaveBeenCalledTimes(1);
      expect(dialogService.open.mock.calls[0][0]).toBe(
        MarkCashCollectedDialogComponent
      );
      expect(dialogService.open.mock.calls[0][1].data).toEqual({
        orderId: ORDER_ID,
        amount: '1,250.00 Kč',
      });
    });

    it('collects only after the dialog is confirmed', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      facade.currentEmployeeId.set(EMPLOYEE_ID);
      dialogService.open.mockReturnValue({ onClose: of({ confirmed: true }) });
      orderClient.markCashCollected.mockReturnValue(
        of(
          MarkCashCollectedResponse.fromJS({
            orderId: ORDER_ID,
            paymentStatus: PaymentStatus.Paid,
          })
        )
      );

      facade.openMarkCashCollectedDialog();

      expect(orderClient.markCashCollected).toHaveBeenCalledTimes(1);
    });

    it('does nothing when the dialog is dismissed', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      facade.currentEmployeeId.set(EMPLOYEE_ID);
      dialogService.open.mockReturnValue({ onClose: of(undefined) });

      facade.openMarkCashCollectedDialog();

      expect(orderClient.markCashCollected).not.toHaveBeenCalled();
    });

    it('offers collection on a CARD order — the backend reconciles against Stripe', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ paymentTypeValue: PaymentType.Card }));
      facade.currentEmployeeId.set(EMPLOYEE_ID);
      dialogService.open.mockReturnValue({ onClose: EMPTY });

      facade.openMarkCashCollectedDialog();

      expect(dialogService.open).toHaveBeenCalledTimes(1);
      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    });

    it('refuses an order that is already paid', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ paymentStatusValue: PaymentStatus.Paid }));
      facade.currentEmployeeId.set(EMPLOYEE_ID);

      facade.openMarkCashCollectedDialog();

      expect(dialogService.open).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.order_details.mark_cash_collected_gating_error'
      );
    });

    it('refuses an order that is not InProgress', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ orderStatusValue: OrderStatus.Confirmed }));
      facade.currentEmployeeId.set(EMPLOYEE_ID);

      facade.openMarkCashCollectedDialog();

      expect(dialogService.open).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.order_details.mark_cash_collected_gating_error'
      );
    });

    it('refuses an order the caller is not assigned to', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ assignedEmployeeId: 'someone-else' }));
      facade.currentEmployeeId.set(EMPLOYEE_ID);

      facade.openMarkCashCollectedDialog();

      expect(dialogService.open).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.order_details.mark_cash_collected_gating_error'
      );
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

    it('serializes a take with the order id', () => {
      const facade = createFacade();
      orderClient.takeOrder.mockReturnValue(
        of(TakeOrderResponse.fromJS({ orderId: ORDER_ID, employeeId: EMPLOYEE_ID }))
      );

      facade.takeOrder(ORDER_ID);

      const command: TakeOrderCommand = orderClient.takeOrder.mock.calls[0][0];
      expect(command).toBeInstanceOf(TakeOrderCommand);
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID });
    });

    it('serializes a cash collection with the order id', () => {
      const facade = createFacade();
      orderClient.markCashCollected.mockReturnValue(
        of(
          MarkCashCollectedResponse.fromJS({
            orderId: ORDER_ID,
            paymentStatus: PaymentStatus.Paid,
          })
        )
      );

      facade.markCashCollected(ORDER_ID);

      const command: MarkCashCollectedCommand =
        orderClient.markCashCollected.mock.calls[0][0];
      expect(command).toBeInstanceOf(MarkCashCollectedCommand);
      expect(command.toJSON()).toEqual({ orderId: ORDER_ID });
    });

    it('serializes a reported issue with the order id and the description', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      dialogService.open.mockReturnValue({
        onClose: of({ description: 'Front door was locked' }),
      });
      orderClient.reportIssue.mockReturnValue(of({}));

      facade.openReportIssueDialog();

      const command: ReportOrderIssueCommand =
        orderClient.reportIssue.mock.calls[0][0];
      expect(command).toBeInstanceOf(ReportOrderIssueCommand);
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        description: 'Front door was locked',
      });
    });

    it('serializes an added note with the order id and the content', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      dialogService.open.mockReturnValue({
        onClose: of({ content: 'Customer asked us to start upstairs' }),
      });
      orderClient.addNote.mockReturnValue(of({}));

      facade.openAddNoteDialog();

      const command: AddOrderNoteCommand = orderClient.addNote.mock.calls[0][0];
      expect(command).toBeInstanceOf(AddOrderNoteCommand);
      expect(command.toJSON()).toEqual({
        orderId: ORDER_ID,
        content: 'Customer asked us to start upstairs',
      });
    });
  });
});
