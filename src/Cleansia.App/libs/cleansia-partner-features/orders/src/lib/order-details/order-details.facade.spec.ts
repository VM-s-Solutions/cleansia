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
} from '@cleansia/partner-services';
import * as OrderActions from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Actions } from '@ngrx/effects';
import { Action, Store } from '@ngrx/store';
import { DialogService } from 'primeng/dynamicdialog';
import { EMPTY, Subject, of, throwError } from 'rxjs';
import { MarkCashCollectedDialogComponent } from '../components/mark-cash-collected-dialog';
import {
  WorkContractDialogComponent,
  WorkContractDialogMode,
  WorkContractDialogOutcome,
  WorkContractDialogResult,
} from '../components/work-contract-dialog';
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
  let dispatch: jest.Mock;
  let actions$: Subject<Action>;

  const createFacade = (): OrderDetailsFacade => {
    TestBed.configureTestingModule({
      providers: [
        OrderDetailsFacade,
        { provide: PartnerClient, useValue: { orderClient, employeeClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: dialogService },
        { provide: TranslateService, useValue: { instant: (k: string) => k, currentLang: 'cs' } },
        { provide: Store, useValue: { dispatch } },
        { provide: Actions, useValue: actions$ },
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
    dispatch = jest.fn();
    actions$ = new Subject<Action>();
  });

  // The shape CleansiaApiController.HandleFailure puts on the wire for a validation refusal.
  const refusal = (code: string): unknown => ({
    detail: 'A validation problem occurred.',
    errors: { OrderId: code },
  });

  const workContractDialogData = () => dialogService.open.mock.calls[0][1].data;

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
    let dialogClose$: Subject<WorkContractDialogResult | undefined>;

    beforeEach(() => {
      dialogClose$ = new Subject<WorkContractDialogResult | undefined>();
      dialogService.open.mockReturnValue({ onClose: dialogClose$ });
    });

    it('opens the contract dialog in take mode instead of taking outright', () => {
      const facade = createFacade();

      facade.takeOrder(ORDER_ID);

      expect(dialogService.open).toHaveBeenCalledTimes(1);
      expect(dialogService.open.mock.calls[0][0]).toBe(WorkContractDialogComponent);
      expect(workContractDialogData()).toEqual({
        mode: WorkContractDialogMode.Take,
        orderId: ORDER_ID,
      });
      expect(orderClient.takeOrder).not.toHaveBeenCalled();
      expect(facade.takeInFlight()).toBe(true);
    });

    it('confirms and re-reads the order exactly once when the dialog reports the take', () => {
      const facade = createFacade();

      facade.takeOrder(ORDER_ID);
      dialogClose$.next({ outcome: WorkContractDialogOutcome.Accepted });

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.orders.order_taken_success'
      );
      expect(orderClient.getById).toHaveBeenCalledTimes(1);
      expect(orderClient.getById).toHaveBeenCalledWith(ORDER_ID);
      expect(facade.takeInFlight()).toBe(false);
    });

    it('re-reads the order when the take was refused, so the button reflects the server', () => {
      const facade = createFacade();

      facade.takeOrder(ORDER_ID);
      dialogClose$.next({ outcome: WorkContractDialogOutcome.Refused });

      expect(orderClient.getById).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.loading()).toBe(false);
    });

    it('re-reads nothing when the dialog is dismissed', () => {
      const facade = createFacade();

      facade.takeOrder(ORDER_ID);
      dialogClose$.next(undefined);

      expect(orderClient.getById).not.toHaveBeenCalled();
      expect(facade.takeInFlight()).toBe(false);
    });

    it('ignores a second click while the dialog is open', () => {
      const facade = createFacade();

      facade.takeOrder(ORDER_ID);
      facade.takeOrder(ORDER_ID);

      expect(dialogService.open).toHaveBeenCalledTimes(1);
    });

    it('never opens the dialog without an order id', () => {
      const facade = createFacade();

      facade.takeOrder('');

      expect(dialogService.open).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'global.messages.orders.invalid_request'
      );
    });
  });

  describe('the standalone acceptance after an admin placement', () => {
    let dialogClose$: Subject<WorkContractDialogResult | undefined>;

    beforeEach(() => {
      dialogClose$ = new Subject<WorkContractDialogResult | undefined>();
      dialogService.open.mockReturnValue({ onClose: dialogClose$ });
    });

    it('opens the dialog in accept mode for the loaded order', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ orderStatusValue: OrderStatus.Confirmed }));

      facade.openAcceptWorkContractDialog();

      expect(dialogService.open.mock.calls[0][0]).toBe(WorkContractDialogComponent);
      expect(workContractDialogData()).toEqual({
        mode: WorkContractDialogMode.Accept,
        orderId: ORDER_ID,
      });
    });

    it('confirms and re-reads the order once the contract is accepted, so the line replaces the banner', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ orderStatusValue: OrderStatus.Confirmed }));

      facade.openAcceptWorkContractDialog();
      dialogClose$.next({ outcome: WorkContractDialogOutcome.Accepted });

      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.order_details.work_contract.accepted_success'
      );
      expect(orderClient.getById).toHaveBeenCalledTimes(1);
    });

    it('re-reads the order when the acceptance was refused', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ orderStatusValue: OrderStatus.Confirmed }));

      facade.openAcceptWorkContractDialog();
      dialogClose$.next({ outcome: WorkContractDialogOutcome.Refused });

      expect(orderClient.getById).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    });

    it('opens the dialog in accept mode when Start is refused for a missing acceptance', () => {
      orderClient.startOrder.mockReturnValue(
        throwError(() => refusal('contract.acceptance_required'))
      );
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ orderStatusValue: OrderStatus.Confirmed }));

      facade.startOrder(ORDER_ID);

      expect(workContractDialogData()).toEqual({
        mode: WorkContractDialogMode.Accept,
        orderId: ORDER_ID,
      });
      expect(facade.loading()).toBe(false);
    });

    it('opens nothing when Start is refused for another reason', () => {
      orderClient.startOrder.mockReturnValue(
        throwError(() => refusal('order.too_early_to_start'))
      );
      const facade = createFacade();
      facade.orderDetails.set(buildOrder({ orderStatusValue: OrderStatus.Confirmed }));

      facade.startOrder(ORDER_ID);

      expect(dialogService.open).not.toHaveBeenCalled();
      expect(facade.loading()).toBe(false);
    });

    it('opens the dialog in accept mode when Complete is refused for a missing acceptance', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      facade.currentEmployeeId.set(EMPLOYEE_ID);

      facade.completeOrder();
      actions$.next(
        OrderActions.completeOrderFailure({
          error: refusal('contract.acceptance_required') as never,
        })
      );

      expect(dispatch).toHaveBeenCalledTimes(1);
      expect(workContractDialogData()).toEqual({
        mode: WorkContractDialogMode.Accept,
        orderId: ORDER_ID,
      });
      expect(orderClient.getById).not.toHaveBeenCalled();
    });

    it('opens nothing when Complete is refused for another reason', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      facade.currentEmployeeId.set(EMPLOYEE_ID);

      facade.completeOrder();
      actions$.next(
        OrderActions.completeOrderFailure({
          error: refusal('order.after_photos.required') as never,
        })
      );

      expect(dialogService.open).not.toHaveBeenCalled();
    });

    it('re-reads the order when Complete succeeds', () => {
      const facade = createFacade();
      facade.orderDetails.set(buildOrder());
      facade.currentEmployeeId.set(EMPLOYEE_ID);

      facade.completeOrder();
      actions$.next(
        OrderActions.completeOrderSuccess({ orderId: ORDER_ID, orderStatus: 'Completed' })
      );

      expect(orderClient.getById).toHaveBeenCalledWith(ORDER_ID);
      expect(dialogService.open).not.toHaveBeenCalled();
    });
  });

  describe('reading an accepted contract', () => {
    it('opens the dialog in read mode keyed on the acceptance', () => {
      dialogService.open.mockReturnValue({ onClose: EMPTY });
      const facade = createFacade();

      facade.openReadWorkContractDialog('acc-1');

      expect(dialogService.open.mock.calls[0][0]).toBe(WorkContractDialogComponent);
      expect(workContractDialogData()).toEqual({
        mode: WorkContractDialogMode.Read,
        acceptanceId: 'acc-1',
      });
      expect(orderClient.getById).not.toHaveBeenCalled();
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
        amount: '1 250,00 Kč',
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

    it('never opens the dialog for an order without an id', () => {
      const facade = createFacade();
      facade.orderDetails.set(OrderItem.fromJS({ ...buildOrder().toJSON(), id: undefined }));
      facade.currentEmployeeId.set(EMPLOYEE_ID);

      facade.openMarkCashCollectedDialog();

      expect(dialogService.open).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'global.messages.orders.invalid_request'
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
