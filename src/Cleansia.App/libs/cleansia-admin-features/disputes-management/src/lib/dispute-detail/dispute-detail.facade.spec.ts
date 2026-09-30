import { TestBed } from '@angular/core/testing';
import {
  AddDisputeMessageCommand,
  AdminDisputeClient,
  AdminOrderClient,
  DisputeDetails,
  DisputeSettlementPreference,
  DisputeStatus,
  OrderItem,
  ResolveDisputeCleanerCharge,
  ResolveDisputeCommand,
  UpdateDisputeStatusCommand,
} from '@cleansia/admin-services';
import { PermissionService, Policy, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { DisputeDetailFacade } from './dispute-detail.facade';

describe('DisputeDetailFacade', () => {
  let facade: DisputeDetailFacade;
  let disputeClient: {
    details: jest.Mock;
    resolve: jest.Mock;
    updateStatus: jest.Mock;
    addMessage: jest.Mock;
  };
  let orderClient: { details: jest.Mock };
  let grantedPolicies: Set<string>;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const details = DisputeDetails.fromJS({
    id: 'dispute-1',
    orderId: 'order-1',
    displayOrderNumber: 'ORD-1',
    status: { type: 'DisputeStatus', name: 'Pending', value: DisputeStatus.Pending },
    messages: [],
  });

  beforeEach(() => {
    disputeClient = {
      details: jest.fn(),
      resolve: jest.fn(),
      updateStatus: jest.fn(),
      addMessage: jest.fn(),
    };
    orderClient = { details: jest.fn().mockReturnValue(of(OrderItem.fromJS({ assignedEmployees: [] }))) };
    grantedPolicies = new Set([Policy.CanResolveDispute, Policy.CanViewOrderDetailAdmin]);
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        DisputeDetailFacade,
        { provide: AdminDisputeClient, useValue: disputeClient },
        { provide: AdminOrderClient, useValue: orderClient },
        { provide: PermissionService, useValue: { hasPolicy: (p: string) => grantedPolicies.has(p) } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k, currentLang: 'cs' } },
      ],
    });

    facade = TestBed.inject(DisputeDetailFacade);
  });

  it('loads the dispute details', () => {
    disputeClient.details.mockReturnValue(of(details));

    facade.loadDispute('dispute-1');

    expect(disputeClient.details).toHaveBeenCalledWith('dispute-1');
    expect(facade.dispute()?.id).toBe('dispute-1');
    expect(facade.loading()).toBe(false);
    expect(facade.hasError()).toBe(false);
  });

  it('sets the error flag when loading fails', () => {
    disputeClient.details.mockReturnValue(throwError(() => new Error('x')));

    facade.loadDispute('dispute-1');

    expect(facade.hasError()).toBe(true);
    expect(facade.dispute()).toBeNull();
    expect(facade.loading()).toBe(false);
  });

  it('builds a ResolveDisputeCommand with refund amount and trimmed notes', () => {
    disputeClient.resolve.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    facade.resolve('dispute-1', 250, '  refund issued  ');

    expect(disputeClient.resolve).toHaveBeenCalledTimes(1);
    const command: ResolveDisputeCommand = disputeClient.resolve.mock.calls[0][0];
    expect(command).toBeInstanceOf(ResolveDisputeCommand);
    expect(command.toJSON()).toEqual({
      disputeId: 'dispute-1',
      refundAmount: 250,
      resolutionNotes: 'refund issued',
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.disputes_management.resolve.submitted'
    );
  });

  it('omits a null refund amount and empty notes from the resolve command', () => {
    disputeClient.resolve.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    facade.resolve('dispute-1', null, '   ');

    const command: ResolveDisputeCommand = disputeClient.resolve.mock.calls[0][0];
    expect(command.toJSON()).toEqual({
      disputeId: 'dispute-1',
      refundAmount: undefined,
      resolutionNotes: undefined,
    });
  });

  it('clears the resolving flag after a successful resolve', () => {
    disputeClient.resolve.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    expect(facade.resolving()).toBe(false);
    facade.resolve('dispute-1', 100, 'notes');
    expect(facade.resolving()).toBe(false);
  });

  it('leaves the dispute.already_resolved refusal to the interceptor toast on resolve failure', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ result: { detail: 'dispute.already_resolved' } }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.resolving()).toBe(false);
  });

  it('leaves a title-only refusal to the interceptor toast on resolve failure', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ result: { title: 'dispute.already_resolved' } }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves the refund.failed refusal to the interceptor toast on resolve failure', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ response: JSON.stringify({ detail: 'refund.failed' }) }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves an unknown refusal to the interceptor toast', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('builds an UpdateDisputeStatusCommand with the new status', () => {
    disputeClient.updateStatus.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    facade.updateStatus('dispute-1', DisputeStatus.UnderReview);

    const command: UpdateDisputeStatusCommand =
      disputeClient.updateStatus.mock.calls[0][0];
    expect(command).toBeInstanceOf(UpdateDisputeStatusCommand);
    expect(command.toJSON()).toEqual({
      disputeId: 'dispute-1',
      newStatus: DisputeStatus.UnderReview,
    });
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.disputes_management.status_update.success'
    );
  });

  it('leaves the dispute.invalid_status_transition refusal to the interceptor toast on update-status failure', () => {
    disputeClient.updateStatus.mockReturnValue(
      throwError(() => ({
        result: { detail: 'dispute.invalid_status_transition' },
      }))
    );

    facade.updateStatus('dispute-1', DisputeStatus.Closed);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.updatingStatus()).toBe(false);
  });

  it('builds a staff AddDisputeMessageCommand and resets on success', () => {
    disputeClient.addMessage.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));
    const onSuccess = jest.fn();

    facade.addMessage('dispute-1', '  hello team  ', onSuccess);

    const command: AddDisputeMessageCommand =
      disputeClient.addMessage.mock.calls[0][0];
    expect(command).toBeInstanceOf(AddDisputeMessageCommand);
    expect(command.toJSON()).toEqual({
      disputeId: 'dispute-1',
      message: 'hello team',
      isStaffMessage: true,
    });
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.disputes_management.message.sent'
    );
  });

  it('does not call addMessage for a blank message', () => {
    facade.addMessage('dispute-1', '   ', jest.fn());
    expect(disputeClient.addMessage).not.toHaveBeenCalled();
  });

  it('reports a terminal dispute as terminal', () => {
    const resolved = DisputeDetails.fromJS({
      id: 'dispute-1',
      status: { type: 'DisputeStatus', name: 'Resolved', value: DisputeStatus.Resolved },
    });
    disputeClient.details.mockReturnValue(of(resolved));

    facade.loadDispute('dispute-1');

    expect(facade.isTerminal()).toBe(true);
  });

  it('labels the refund with the currency the dispute carries, in the language of the session', () => {
    facade.dispute.set(DisputeDetails.fromJS({ refundAmount: 1250, currency: { code: 'CZK', symbol: 'Kč' } }));

    expect(facade.refundAmountLabel()).toBe('1 250,00 Kč');

    facade.dispute.set(DisputeDetails.fromJS({}));

    expect(facade.refundAmountLabel()).toBe('');
  });

  // The card leg is clamped to what the card can still give back, so what moved can fall short of
  // the request; the admin reads both.
  it('labels what the resolution moved beside what it asked for', () => {
    facade.dispute.set(
      DisputeDetails.fromJS({
        refundAmount: 1250,
        cardRefundedAmount: 1000,
        creditReturnedAmount: 150,
        currency: { code: 'CZK', symbol: 'Kč' },
      })
    );

    expect(facade.refundAmountLabel()).toBe('1 250,00 Kč');
    expect(facade.cardRefundedLabel()).toBe('1 000,00 Kč');
    expect(facade.creditReturnedLabel()).toBe('150,00 Kč');
  });

  it('labels a leg that moved nothing as zero, and one never recorded as nothing', () => {
    facade.dispute.set(
      DisputeDetails.fromJS({
        refundAmount: 500,
        cardRefundedAmount: 500,
        creditReturnedAmount: 0,
        currency: { code: 'CZK', symbol: 'Kč' },
      })
    );
    expect(facade.creditReturnedLabel()).toBe('0,00 Kč');

    facade.dispute.set(DisputeDetails.fromJS({ refundAmount: 500, currency: { code: 'CZK' } }));
    expect(facade.cardRefundedLabel()).toBe('');
    expect(facade.creditReturnedLabel()).toBe('');
  });

  it('reads the settlement the customer chose off the dispute', () => {
    facade.dispute.set(DisputeDetails.fromJS({ settlementPreference: DisputeSettlementPreference.Credit }));
    expect(facade.settlesInCredit()).toBe(true);

    facade.dispute.set(DisputeDetails.fromJS({ settlementPreference: DisputeSettlementPreference.CardRefund }));
    expect(facade.settlesInCredit()).toBe(false);
  });

  it('loads the crew of the disputed order for an open dispute', () => {
    disputeClient.details.mockReturnValue(of(details));
    orderClient.details.mockReturnValue(
      of(
        OrderItem.fromJS({
          assignedEmployees: [
            { id: 'seat-1', employeeId: 'emp-1', fullName: 'Jana Nová' },
            { id: 'seat-2', employeeId: 'emp-2', fullName: 'Petr Malý' },
          ],
        })
      )
    );

    facade.loadDispute('dispute-1');

    expect(orderClient.details).toHaveBeenCalledWith('order-1');
    expect(facade.crew().map((employee) => employee.employeeId)).toEqual(['emp-1', 'emp-2']);
  });

  it('does not load the crew of a settled dispute', () => {
    disputeClient.details.mockReturnValue(
      of(
        DisputeDetails.fromJS({
          id: 'dispute-1',
          orderId: 'order-1',
          status: { type: 'DisputeStatus', name: 'Resolved', value: DisputeStatus.Resolved },
        })
      )
    );

    facade.loadDispute('dispute-1');

    expect(orderClient.details).not.toHaveBeenCalled();
    expect(facade.crew()).toEqual([]);
  });

  it.each([Policy.CanResolveDispute, Policy.CanViewOrderDetailAdmin])(
    'does not load the crew without %s',
    (policy) => {
      grantedPolicies.delete(policy);
      disputeClient.details.mockReturnValue(of(details));

      facade.loadDispute('dispute-1');

      expect(orderClient.details).not.toHaveBeenCalled();
    }
  );

  it('leaves the crew empty when the order cannot be read', () => {
    disputeClient.details.mockReturnValue(of(details));
    orderClient.details.mockReturnValue(throwError(() => new Error('x')));

    facade.loadDispute('dispute-1');

    expect(facade.crew()).toEqual([]);
    expect(facade.dispute()?.id).toBe('dispute-1');
  });

  it('sends a charge to the cleaner with the resolution, reason trimmed', () => {
    disputeClient.resolve.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    facade.resolve('dispute-1', 500, 'partly refunded', {
      employeeId: 'emp-1',
      amount: 150,
      reason: '  skipped the bathroom  ',
    });

    const command: ResolveDisputeCommand = disputeClient.resolve.mock.calls[0][0];
    expect(command.chargeToCleaner).toBeInstanceOf(ResolveDisputeCleanerCharge);
    expect(command.toJSON()).toEqual({
      disputeId: 'dispute-1',
      refundAmount: 500,
      resolutionNotes: 'partly refunded',
      chargeToCleaner: { employeeId: 'emp-1', amount: 150, reason: 'skipped the bathroom' },
    });
  });

  it('sends no charge to a cleaner unless one is asked for', () => {
    disputeClient.resolve.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    facade.resolve('dispute-1', 500, 'refunded', null);

    const command: ResolveDisputeCommand = disputeClient.resolve.mock.calls[0][0];
    expect(command.chargeToCleaner).toBeUndefined();
  });

  it('leaves the dispute.cleaner_charge_not_chargeable refusal to the interceptor toast', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ result: { detail: 'dispute.cleaner_charge_not_chargeable' } }))
    );

    facade.resolve('dispute-1', 0, 'notes', { employeeId: 'emp-1', amount: 9999, reason: 'fault' });

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.resolving()).toBe(false);
  });
});
