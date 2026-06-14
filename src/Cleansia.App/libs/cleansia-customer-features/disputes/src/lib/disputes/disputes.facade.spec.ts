import { TestBed } from '@angular/core/testing';
import {
  CustomerClient,
  DisputeDetails,
  DisputeListItem,
} from '@cleansia/customer-services';
import {
  CUSTOMER_DISPUTE_FEATURE_KEY,
  loadCustomerDisputeDetail,
  loadCustomerDisputes,
} from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { DisputesFacade } from './disputes.facade';
import { CustomerDisputeStatus } from './disputes.models';

describe('DisputesFacade', () => {
  let facade: DisputesFacade;
  let store: MockStore;
  let dispatchSpy: jest.SpyInstance;
  let disputeClient: {
    create: jest.Mock;
    getById: jest.Mock;
    getPaged: jest.Mock;
    addMessage: jest.Mock;
    uploadEvidence: jest.Mock;
  };
  let snackbar: {
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
    showSuccess: jest.Mock;
    showError: jest.Mock;
    showApiError: jest.Mock;
  };

  const dispute = DisputeListItem.fromJS({
    id: 'dispute-1',
    displayOrderNumber: 'ORD-1',
    status: { type: 'DisputeStatus', name: 'Pending', value: 1 },
    reason: { type: 'DisputeReason', name: 'QualityIssue', value: 1 },
    createdOn: '2026-06-01T10:00:00Z',
  });

  const detailWithStaffReply = DisputeDetails.fromJS({
    id: 'dispute-1',
    status: { type: 'DisputeStatus', name: 'Pending', value: 1 },
    reason: { type: 'DisputeReason', name: 'QualityIssue', value: 1 },
    messages: [
      {
        id: 'm1',
        message: 'staff reply',
        isStaffMessage: true,
        createdOn: '2026-06-09T10:00:00Z',
      },
    ],
  });

  const initialState = {
    [CUSTOMER_DISPUTE_FEATURE_KEY]: {
      disputes: [],
      totalRecords: 0,
      loading: {},
    },
  };

  const validFile = (overrides?: Partial<File>): File =>
    ({
      name: 'photo.png',
      type: 'image/png',
      size: 1024,
      ...overrides,
    } as File);

  beforeEach(() => {
    localStorage.clear();
    disputeClient = {
      create: jest.fn(),
      getById: jest.fn().mockReturnValue(of(detailWithStaffReply)),
      getPaged: jest.fn(),
      addMessage: jest.fn(),
      uploadEvidence: jest.fn(),
    };
    snackbar = {
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccess: jest.fn(),
      showError: jest.fn(),
      showApiError: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        DisputesFacade,
        provideMockStore({ initialState }),
        {
          provide: CustomerClient,
          useValue: { disputeClient },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    store = TestBed.inject(MockStore);
    dispatchSpy = jest.spyOn(store, 'dispatch');
    facade = TestBed.inject(DisputesFacade);
  });

  describe('status filter (D-10)', () => {
    it('dispatches without statuses when no filter is set', () => {
      facade.loadDisputes(0, 10);

      expect(dispatchSpy).toHaveBeenCalledWith(
        loadCustomerDisputes({ offset: 0, limit: 10, statuses: undefined })
      );
    });

    it('carries the selected status into the load action', () => {
      facade.setStatusFilter(CustomerDisputeStatus.WaitingForResponse);
      facade.loadDisputes(0, 10);

      expect(dispatchSpy).toHaveBeenCalledWith(
        loadCustomerDisputes({
          offset: 0,
          limit: 10,
          statuses: [CustomerDisputeStatus.WaitingForResponse],
        })
      );
    });

    it('restores the unfiltered load when the filter is cleared', () => {
      facade.setStatusFilter(CustomerDisputeStatus.Resolved);
      facade.setStatusFilter(null);
      facade.loadDisputes(0, 10);

      expect(dispatchSpy).toHaveBeenLastCalledWith(
        loadCustomerDisputes({ offset: 0, limit: 10, statuses: undefined })
      );
    });
  });

  describe('evidence upload (D-04)', () => {
    it('rejects an out-of-whitelist file before any client call', () => {
      facade.uploadEvidence(
        'dispute-1',
        validFile({ type: 'application/zip' })
      );

      expect(disputeClient.uploadEvidence).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.disputes.evidence.invalid_type'
      );
    });

    it('rejects an oversize file before any client call', () => {
      facade.uploadEvidence(
        'dispute-1',
        validFile({ size: 11 * 1024 * 1024 })
      );

      expect(disputeClient.uploadEvidence).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.disputes.evidence.too_large'
      );
    });

    it('uploads a valid file and re-loads the detail on success', () => {
      disputeClient.uploadEvidence.mockReturnValue(
        of({ evidenceId: 'e1', fileName: 'photo.png' })
      );
      const onSuccess = jest.fn();
      const file = validFile();

      facade.uploadEvidence('dispute-1', file, onSuccess);

      expect(disputeClient.uploadEvidence).toHaveBeenCalledWith('dispute-1', {
        data: file,
        fileName: 'photo.png',
      });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.disputes.evidence.upload_success'
      );
      expect(onSuccess).toHaveBeenCalledTimes(1);
      expect(dispatchSpy).toHaveBeenCalledWith(
        loadCustomerDisputeDetail({ disputeId: 'dispute-1' })
      );
      expect(facade.uploadingEvidence()).toBe(false);
    });

    it('maps a backend error code to its api.* key on failure', () => {
      disputeClient.uploadEvidence.mockReturnValue(
        throwError(() => ({ result: { detail: 'file.invalid_file_type' } }))
      );

      facade.uploadEvidence('dispute-1', validFile());

      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'api.file.invalid_file_type'
      );
      expect(facade.uploadingEvidence()).toBe(false);
    });

    it('falls back to the generic upload error for unknown codes', () => {
      disputeClient.uploadEvidence.mockReturnValue(
        throwError(() => ({ result: { detail: 'something.else' } }))
      );

      facade.uploadEvidence('dispute-1', validFile());

      expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
        'pages.disputes.evidence.upload_error'
      );
    });
  });

  const anyErrorSnackbarShown = (): boolean =>
    snackbar.showError.mock.calls.length > 0 ||
    snackbar.showApiError.mock.calls.length > 0 ||
    snackbar.showErrorTranslated.mock.calls.length > 0;

  const anySuccessSnackbarShown = (): boolean =>
    snackbar.showSuccess.mock.calls.length > 0 ||
    snackbar.showSuccessTranslated.mock.calls.length > 0;

  describe('createDispute', () => {
    it('shows a success snackbar and invokes the callback on success', () => {
      disputeClient.create.mockReturnValue(of('new-dispute-id'));
      const onSuccess = jest.fn();

      facade.createDispute('order-1', 1, 'description text', onSuccess);

      expect(disputeClient.create).toHaveBeenCalledTimes(1);
      const command = disputeClient.create.mock.calls[0][0];
      expect(command.orderId).toBe('order-1');
      expect(command.reason).toBe(1);
      expect(command.description).toBe('description text');
      expect(anySuccessSnackbarShown()).toBe(true);
      expect(onSuccess).toHaveBeenCalledTimes(1);
    });

    it('surfaces an error snackbar and skips the callback on failure', () => {
      disputeClient.create.mockReturnValue(
        throwError(() => ({ result: { detail: 'dispute.order_already_disputed' } }))
      );
      const onSuccess = jest.fn();

      facade.createDispute('order-1', 1, 'description text', onSuccess);

      expect(anyErrorSnackbarShown()).toBe(true);
      expect(anySuccessSnackbarShown()).toBe(false);
      expect(onSuccess).not.toHaveBeenCalled();
    });
  });

  describe('sendMessage', () => {
    it('toggles sendingMessage, reloads the detail, and invokes the callback on success', () => {
      disputeClient.addMessage.mockReturnValue(of(undefined));
      const onSuccess = jest.fn();

      facade.sendMessage('dispute-1', 'hello there', onSuccess);

      expect(disputeClient.addMessage).toHaveBeenCalledTimes(1);
      const command = disputeClient.addMessage.mock.calls[0][0];
      expect(command.disputeId).toBe('dispute-1');
      expect(command.message).toBe('hello there');
      expect(command.isStaffMessage).toBe(false);
      expect(onSuccess).toHaveBeenCalledTimes(1);
      expect(dispatchSpy).toHaveBeenCalledWith(
        loadCustomerDisputeDetail({ disputeId: 'dispute-1' })
      );
      expect(facade.sendingMessage()).toBe(false);
    });

    it('surfaces an error snackbar and resets sendingMessage on failure', () => {
      disputeClient.addMessage.mockReturnValue(
        throwError(() => ({ result: { detail: 'dispute.not_owned_by_user' } }))
      );
      const onSuccess = jest.fn();

      facade.sendMessage('dispute-1', 'hello there', onSuccess);

      expect(anyErrorSnackbarShown()).toBe(true);
      expect(onSuccess).not.toHaveBeenCalled();
      expect(facade.sendingMessage()).toBe(false);
    });
  });

  describe('unread staff replies (D-10)', () => {
    it('flags a dispute whose newest staff message was never viewed', () => {
      store.setState({
        [CUSTOMER_DISPUTE_FEATURE_KEY]: {
          disputes: [dispute],
          totalRecords: 1,
          loading: {},
        },
      });

      expect(disputeClient.getById).toHaveBeenCalledWith('dispute-1');
      expect(facade.unreadDisputeIds().has('dispute-1')).toBe(true);
    });

    it('clears the badge once the dispute is viewed', () => {
      store.setState({
        [CUSTOMER_DISPUTE_FEATURE_KEY]: {
          disputes: [dispute],
          totalRecords: 1,
          loading: {},
        },
      });

      facade.markViewed('dispute-1');

      expect(facade.unreadDisputeIds().has('dispute-1')).toBe(false);
    });

    it('flags again when a newer staff message arrives after the view', () => {
      store.setState({
        [CUSTOMER_DISPUTE_FEATURE_KEY]: {
          disputes: [dispute],
          totalRecords: 1,
          loading: {},
        },
      });
      facade.markViewed('dispute-1');

      disputeClient.getById.mockReturnValue(
        of(
          DisputeDetails.fromJS({
            ...detailWithStaffReply.toJSON(),
            messages: [
              {
                id: 'm2',
                message: 'newer staff reply',
                isStaffMessage: true,
                createdOn: new Date(Date.now() + 60_000).toISOString(),
              },
            ],
          })
        )
      );
      store.setState({
        [CUSTOMER_DISPUTE_FEATURE_KEY]: {
          disputes: [DisputeListItem.fromJS(dispute.toJSON())],
          totalRecords: 1,
          loading: {},
        },
      });

      expect(facade.unreadDisputeIds().has('dispute-1')).toBe(true);
    });

    it('never flags a dispute without staff messages', () => {
      disputeClient.getById.mockReturnValue(
        of(
          DisputeDetails.fromJS({
            id: 'dispute-1',
            status: { type: 'DisputeStatus', name: 'Pending', value: 1 },
            messages: [],
          })
        )
      );
      store.setState({
        [CUSTOMER_DISPUTE_FEATURE_KEY]: {
          disputes: [dispute],
          totalRecords: 1,
          loading: {},
        },
      });

      expect(facade.unreadDisputeIds().has('dispute-1')).toBe(false);
    });
  });
});
