import { TestBed } from '@angular/core/testing';
import {
  AdminGdprClient,
  ConsentType,
  GdprExportDto,
  GdprRequestDto,
  GdprRequestStatus,
  PagedDataOfGdprRequestDto,
  RequestsClient,
  UserConsentDto,
} from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { DataProtectionFacade } from './data-protection.facade';

describe('DataProtectionFacade', () => {
  let facade: DataProtectionFacade;
  let gdprClient: {
    requests: jest.Mock;
    consents: jest.Mock;
    export: jest.Mock;
    deleteAccount: jest.Mock;
  };
  let requestsClient: { retryDeletion: jest.Mock };
  let confirmMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock; showSuccessTranslated: jest.Mock;
    showError: jest.Mock; showErrorTranslated: jest.Mock;
    showApiError: jest.Mock;
  };

  const requestRows = [
    GdprRequestDto.fromJS({
      id: 'req-1',
      userId: 'user-1',
      requestType: 'Export',
      status: GdprRequestStatus.Completed,
    }),
  ];

  const pagedRequests = (rows: GdprRequestDto[], total = rows.length) =>
    PagedDataOfGdprRequestDto.fromJS({
      data: rows,
      total,
      pageNumber: 1,
      pageSize: 20,
    });

  const consentRows = [
    UserConsentDto.fromJS({
      id: 'con-1',
      consentType: ConsentType.MarketingEmails,
      isGranted: true,
    }),
  ];

  beforeEach(() => {
    gdprClient = {
      requests: jest.fn(),
      consents: jest.fn(),
      export: jest.fn(),
      deleteAccount: jest.fn(),
    };
    requestsClient = { retryDeletion: jest.fn() };
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = {
      showSuccess: jest.fn(), showSuccessTranslated: jest.fn(),
      showError: jest.fn(), showErrorTranslated: jest.fn(),
      showApiError: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        DataProtectionFacade,
        { provide: AdminGdprClient, useValue: gdprClient },
        { provide: RequestsClient, useValue: requestsClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(DataProtectionFacade);
  });

  describe('requests list', () => {
    it('loads the first page by default and stores the rows and total', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.loadRequests();

      expect(gdprClient.requests).toHaveBeenCalledWith(
        undefined,
        undefined,
        0,
        20
      );
      expect(facade.requests().length).toBe(1);
      expect(facade.totalRecords()).toBe(1);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('passes the offset/limit from the page event straight through', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.onPageChange(40, 20);

      expect(gdprClient.requests).toHaveBeenCalledWith(
        undefined,
        undefined,
        40,
        20
      );
    });

    it('opens on every status', () => {
      expect(facade.status()).toBeNull();
    });

    it('passes the chosen status to the server and restarts from the first page', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));
      facade.onPageChange(40, 20);

      facade.selectStatus(GdprRequestStatus.Failed);

      expect(facade.status()).toBe(GdprRequestStatus.Failed);
      expect(gdprClient.requests).toHaveBeenLastCalledWith(
        GdprRequestStatus.Failed,
        undefined,
        0,
        20
      );
    });

    it('keeps the chosen status across page changes', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));
      facade.selectStatus(GdprRequestStatus.Processing);

      facade.onPageChange(20, 20);

      expect(gdprClient.requests).toHaveBeenLastCalledWith(
        GdprRequestStatus.Processing,
        undefined,
        20,
        20
      );
    });

    it('passes the zero-valued Pending status rather than dropping it as falsy', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.selectStatus(GdprRequestStatus.Pending);

      expect(facade.status()).toBe(GdprRequestStatus.Pending);
      expect(gdprClient.requests).toHaveBeenLastCalledWith(
        GdprRequestStatus.Pending,
        undefined,
        0,
        20
      );
    });

    it('drops a response the filter has already moved past, so the newer rows stay', () => {
      const older = new Subject<PagedDataOfGdprRequestDto>();
      const newer = new Subject<PagedDataOfGdprRequestDto>();
      gdprClient.requests
        .mockReturnValueOnce(older.asObservable())
        .mockReturnValueOnce(newer.asObservable());
      const failedRow = GdprRequestDto.fromJS({
        id: 'req-failed',
        status: GdprRequestStatus.Failed,
      });
      const staleRow = GdprRequestDto.fromJS({
        id: 'req-stale',
        status: GdprRequestStatus.Completed,
      });

      facade.loadRequests();
      facade.selectStatus(GdprRequestStatus.Failed);
      newer.next(pagedRequests([failedRow], 1));
      newer.complete();
      older.next(pagedRequests([staleRow], 7));
      older.complete();

      expect(facade.requests()).toEqual([failedRow]);
      expect(facade.totalRecords()).toBe(1);
    });

    it('drops a response the page has already moved past, so the newer rows stay', () => {
      const older = new Subject<PagedDataOfGdprRequestDto>();
      const newer = new Subject<PagedDataOfGdprRequestDto>();
      gdprClient.requests
        .mockReturnValueOnce(older.asObservable())
        .mockReturnValueOnce(newer.asObservable());
      const secondPageRow = GdprRequestDto.fromJS({ id: 'req-page-2' });
      const staleRow = GdprRequestDto.fromJS({ id: 'req-stale' });

      facade.loadRequests();
      facade.onPageChange(20, 20);
      newer.next(pagedRequests([secondPageRow], 21));
      newer.complete();
      older.next(pagedRequests([staleRow], 21));
      older.complete();

      expect(facade.requests()).toEqual([secondPageRow]);
    });

    it('clearing the status filter asks for every status again', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));
      facade.selectStatus(GdprRequestStatus.Failed);

      facade.selectStatus(null);

      expect(facade.status()).toBeNull();
      expect(gdprClient.requests).toHaveBeenLastCalledWith(
        undefined,
        undefined,
        0,
        20
      );
    });

    it('uses the server-reported total record count', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 57)));

      facade.loadRequests();

      expect(facade.totalRecords()).toBe(57);
    });

    it('sets the error flag and surfaces the API error on failure', () => {
      const error = new Error('forbidden');
      gdprClient.requests.mockReturnValue(throwError(() => error));

      facade.loadRequests();

      expect(facade.hasError()).toBe(true);
      expect(facade.loading()).toBe(false);
      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.data_protection.requests.load_error'
      );
    });
  });

  describe('consents viewer', () => {
    it('loads consents for the given user', () => {
      gdprClient.consents.mockReturnValue(of(consentRows));

      facade.loadConsents('  user-1  ');

      expect(gdprClient.consents).toHaveBeenCalledWith('user-1');
      expect(facade.consents().length).toBe(1);
      expect(facade.consentsUserId()).toBe('user-1');
      expect(facade.consentsLoading()).toBe(false);
    });

    it('does not call the client for a blank user id', () => {
      facade.loadConsents('   ');
      expect(gdprClient.consents).not.toHaveBeenCalled();
    });

    it('surfaces the API error and keeps the view alive on failure', () => {
      const error = new Error('403');
      gdprClient.consents.mockReturnValue(throwError(() => error));

      facade.loadConsents('user-1');

      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.data_protection.consents.load_error'
      );
      expect(facade.consentsLoading()).toBe(false);
    });
  });

  describe('export (DSAR)', () => {
    it('downloads the export, shows success and refreshes the audit list', () => {
      gdprClient.export.mockReturnValue(
        of(GdprExportDto.fromJS({ userId: 'user-1' }))
      );
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));
      const download = jest
        .spyOn(
          facade as unknown as { downloadJson: (d: unknown, n: string) => void },
          'downloadJson'
        )
        .mockImplementation(() => undefined);

      facade.exportUserData('user-1');

      expect(gdprClient.export).toHaveBeenCalledWith('user-1');
      expect(download).toHaveBeenCalledWith(
        expect.anything(),
        'user-data-export-user-1.json'
      );
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.data_protection.export.success'
      );
      expect(gdprClient.requests).toHaveBeenCalledTimes(1);
      expect(facade.exporting()).toBe(false);
    });

    it('surfaces the API error and downloads nothing on failure', () => {
      const error = new Error('403');
      gdprClient.export.mockReturnValue(throwError(() => error));
      const download = jest.spyOn(
        facade as unknown as { downloadJson: (d: unknown, n: string) => void },
        'downloadJson'
      );

      facade.exportUserData('user-1');

      expect(download).not.toHaveBeenCalled();
      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.data_protection.export.error'
      );
      expect(facade.exporting()).toBe(false);
    });
  });

  describe('erasure', () => {
    it('erases the account, shows success and refreshes the audit list', () => {
      gdprClient.deleteAccount.mockReturnValue(of(undefined));
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.eraseUserAccount('user-1');

      expect(gdprClient.deleteAccount).toHaveBeenCalledWith('user-1');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.data_protection.erase.success'
      );
      expect(gdprClient.requests).toHaveBeenCalledTimes(1);
      expect(facade.erasing()).toBe(false);
    });

    it('surfaces the API error on a blocked erasure', () => {
      const error = new Error('blocked');
      gdprClient.deleteAccount.mockReturnValue(throwError(() => error));

      facade.eraseUserAccount('user-1');

      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.data_protection.erase.error'
      );
      expect(facade.erasing()).toBe(false);
    });

    it('ignores a second erase while one is in flight', () => {
      facade.erasing.set(true);
      facade.eraseUserAccount('user-1');
      expect(gdprClient.deleteAccount).not.toHaveBeenCalled();
    });

    it('fulfils a deletion request through the same red confirmation and the same erase', () => {
      gdprClient.deleteAccount.mockReturnValue(of(undefined));
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.fulfilDeletionRequest('user-1');

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.data_protection.erase.confirm_message',
        'pages.data_protection.requests.fulfil_title',
        { userId: 'user-1' },
        { danger: true, acceptLabelKey: 'pages.data_protection.erase.confirm_yes' }
      );
      expect(gdprClient.deleteAccount).toHaveBeenCalledWith('user-1');
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.eraseUserAccount('user-1');
      facade.fulfilDeletionRequest('user-1');

      expect(confirmMock).toHaveBeenCalledTimes(2);
      expect(gdprClient.deleteAccount).not.toHaveBeenCalled();
      expect(facade.erasing()).toBe(false);
    });
  });

  describe('deletion retry', () => {
    it('retries the request, shows success and refreshes the list', () => {
      requestsClient.retryDeletion.mockReturnValue(of(undefined));
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.retryDeletion(GdprRequestDto.fromJS({ id: 'req-1', userId: 'user-1' }));

      expect(requestsClient.retryDeletion).toHaveBeenCalledWith('req-1');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.data_protection.requests.retry_success'
      );
      expect(gdprClient.requests).toHaveBeenCalledTimes(1);
      expect(facade.retrying()).toBe(false);
    });

    it('surfaces the refusal and still re-reads the list, so a row the job already completed leaves the screen', () => {
      const error = new Error('gdpr.request_not_retryable');
      requestsClient.retryDeletion.mockReturnValue(throwError(() => error));
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.retryDeletion(GdprRequestDto.fromJS({ id: 'req-1', userId: 'user-1' }));

      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.data_protection.requests.retry_error'
      );
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(gdprClient.requests).toHaveBeenCalledTimes(1);
      expect(facade.retrying()).toBe(false);
    });

    it('reports a retry in flight while the call runs', () => {
      requestsClient.retryDeletion.mockReturnValue(new Subject<void>());

      facade.retryDeletion(GdprRequestDto.fromJS({ id: 'req-1', userId: 'user-1' }));

      expect(facade.retrying()).toBe(true);
    });

    it('ignores a second retry while one is in flight', () => {
      requestsClient.retryDeletion.mockReturnValue(new Subject<void>());
      facade.retryDeletion(GdprRequestDto.fromJS({ id: 'req-1', userId: 'user-1' }));

      facade.retryDeletion(GdprRequestDto.fromJS({ id: 'req-2', userId: 'user-2' }));

      expect(requestsClient.retryDeletion).toHaveBeenCalledTimes(1);
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.retryDeletion(GdprRequestDto.fromJS({ id: 'req-1', userId: 'user-1' }));

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.data_protection.requests.retry_confirm_message',
        'pages.data_protection.requests.retry_confirm_title',
        { userId: 'user-1' },
        { acceptLabelKey: 'pages.data_protection.requests.retry_confirm_yes' }
      );
      expect(requestsClient.retryDeletion).not.toHaveBeenCalled();
      expect(facade.retrying()).toBe(false);
    });
  });
});
