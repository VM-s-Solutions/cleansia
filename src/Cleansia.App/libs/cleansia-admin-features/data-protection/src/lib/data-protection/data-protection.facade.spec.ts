import { TestBed } from '@angular/core/testing';
import {
  AdminGdprClient,
  ConsentType,
  GdprExportDto,
  GdprRequestDto,
  GdprRequestStatus,
  PagedDataOfGdprRequestDto,
  UserConsentDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { DataProtectionFacade } from './data-protection.facade';

describe('DataProtectionFacade', () => {
  let facade: DataProtectionFacade;
  let gdprClient: {
    requests: jest.Mock;
    consents: jest.Mock;
    export: jest.Mock;
    deleteAccount: jest.Mock;
  };
  let snackbar: {
    showSuccess: jest.Mock;
    showError: jest.Mock;
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
    snackbar = {
      showSuccess: jest.fn(),
      showError: jest.fn(),
      showApiError: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        DataProtectionFacade,
        { provide: AdminGdprClient, useValue: gdprClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(DataProtectionFacade);
  });

  describe('requests list', () => {
    it('loads the first page by default and stores the rows and total', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.loadRequests();

      expect(gdprClient.requests).toHaveBeenCalledWith(undefined, 0, 20);
      expect(facade.requests().length).toBe(1);
      expect(facade.totalRecords()).toBe(1);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.loading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('passes the offset/limit from the page event straight through', () => {
      gdprClient.requests.mockReturnValue(of(pagedRequests(requestRows, 1)));

      facade.onPageChange(40, 20);

      expect(gdprClient.requests).toHaveBeenCalledWith(undefined, 40, 20);
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
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
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
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
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
  });
});
