import { TestBed } from '@angular/core/testing';
import {
  AdminDisputeClient,
  DisputeDetails,
  DisputeStatus,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
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
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

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
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        DisputeDetailFacade,
        { provide: AdminDisputeClient, useValue: disputeClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
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
    const command = disputeClient.resolve.mock.calls[0][0];
    expect(command.disputeId).toBe('dispute-1');
    expect(command.refundAmount).toBe(250);
    expect(command.resolutionNotes).toBe('refund issued');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.disputes_management.resolve.submitted'
    );
  });

  it('omits a null refund amount and empty notes from the resolve command', () => {
    disputeClient.resolve.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    facade.resolve('dispute-1', null, '   ');

    const command = disputeClient.resolve.mock.calls[0][0];
    expect(command.refundAmount).toBeUndefined();
    expect(command.resolutionNotes).toBeUndefined();
  });

  it('clears the resolving flag after a successful resolve', () => {
    disputeClient.resolve.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    expect(facade.resolving()).toBe(false);
    facade.resolve('dispute-1', 100, 'notes');
    expect(facade.resolving()).toBe(false);
  });

  it('maps dispute.already_resolved to its translation key on resolve failure', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ result: { detail: 'dispute.already_resolved' } }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.dispute.already_resolved'
    );
    expect(facade.resolving()).toBe(false);
  });

  it('falls back to result.title when detail is absent on resolve failure', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ result: { title: 'dispute.already_resolved' } }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.dispute.already_resolved'
    );
  });

  it('maps refund.failed to its translation key on resolve failure', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ response: JSON.stringify({ detail: 'refund.failed' }) }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showError).toHaveBeenCalledWith('errors.refund.failed');
  });

  it('falls back to the generic dispute error for unknown codes', () => {
    disputeClient.resolve.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.resolve('dispute-1', 100, 'notes');

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.dispute.action_failed'
    );
  });

  it('builds an UpdateDisputeStatusCommand with the new status', () => {
    disputeClient.updateStatus.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));

    facade.updateStatus('dispute-1', DisputeStatus.UnderReview);

    const command = disputeClient.updateStatus.mock.calls[0][0];
    expect(command.disputeId).toBe('dispute-1');
    expect(command.newStatus).toBe(DisputeStatus.UnderReview);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.disputes_management.status_update.success'
    );
  });

  it('maps dispute.invalid_status_transition on update-status failure', () => {
    disputeClient.updateStatus.mockReturnValue(
      throwError(() => ({
        result: { detail: 'dispute.invalid_status_transition' },
      }))
    );

    facade.updateStatus('dispute-1', DisputeStatus.Closed);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.dispute.invalid_status_transition'
    );
    expect(facade.updatingStatus()).toBe(false);
  });

  it('builds a staff AddDisputeMessageCommand and resets on success', () => {
    disputeClient.addMessage.mockReturnValue(of(undefined));
    disputeClient.details.mockReturnValue(of(details));
    const onSuccess = jest.fn();

    facade.addMessage('dispute-1', '  hello team  ', onSuccess);

    const command = disputeClient.addMessage.mock.calls[0][0];
    expect(command.disputeId).toBe('dispute-1');
    expect(command.message).toBe('hello team');
    expect(command.isStaffMessage).toBe(true);
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
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
});
