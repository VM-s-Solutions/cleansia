import { TestBed } from '@angular/core/testing';
import {
  ConsentType,
  ConsentsClient,
  GdprClient,
  GdprExportDto,
  GrantConsentCommand,
  PartnerAuthService,
  UserConsentDto,
  WithdrawConsentCommand,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PartnerGdprFacade } from './gdpr.facade';

describe('PartnerGdprFacade', () => {
  let gdprClient: {
    consentsGet: jest.Mock;
    consentsPost: jest.Mock;
    export: jest.Mock;
    deleteAccount: jest.Mock;
  };
  let consentsClient: { withdraw: jest.Mock };
  let authService: { isLoggedIn: jest.Mock; logout: jest.Mock };
  let snackbar: {
    showSuccess: jest.Mock;
    showError: jest.Mock;
    showApiError: jest.Mock;
  };

  const consentRows = [
    UserConsentDto.fromJS({
      id: 'con-1',
      consentType: ConsentType.MarketingEmails,
      isGranted: true,
    }),
    UserConsentDto.fromJS({
      id: 'con-2',
      consentType: ConsentType.DataProcessing,
      isGranted: false,
    }),
  ];

  const createFacade = (loggedIn: boolean): PartnerGdprFacade => {
    authService.isLoggedIn.mockReturnValue(loggedIn);

    TestBed.configureTestingModule({
      providers: [
        PartnerGdprFacade,
        { provide: GdprClient, useValue: gdprClient },
        { provide: ConsentsClient, useValue: consentsClient },
        { provide: PartnerAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    return TestBed.inject(PartnerGdprFacade);
  };

  beforeEach(() => {
    gdprClient = {
      consentsGet: jest.fn(),
      consentsPost: jest.fn(),
      export: jest.fn(),
      deleteAccount: jest.fn(),
    };
    consentsClient = { withdraw: jest.fn() };
    authService = { isLoggedIn: jest.fn(), logout: jest.fn() };
    snackbar = {
      showSuccess: jest.fn(),
      showError: jest.fn(),
      showApiError: jest.fn(),
    };
  });

  describe('consents', () => {
    it('loads consents and clears the loading flag', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(of(consentRows));

      facade.loadConsents();

      expect(gdprClient.consentsGet).toHaveBeenCalledTimes(1);
      expect(facade.consents().length).toBe(2);
      expect(facade.loadingConsents()).toBe(false);
    });

    it('never calls the consents endpoint while unauthenticated', () => {
      const facade = createFacade(false);

      facade.loadConsents();

      expect(gdprClient.consentsGet).not.toHaveBeenCalled();
      expect(facade.loadingConsents()).toBe(false);
    });

    it('clears the loading flag on a failed load', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(
        throwError(() => new Error('boom'))
      );

      facade.loadConsents();

      expect(facade.loadingConsents()).toBe(false);
    });

    it('reports granted state per consent type', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(of(consentRows));

      facade.loadConsents();

      expect(facade.isConsentGranted(ConsentType.MarketingEmails)).toBe(true);
      expect(facade.isConsentGranted(ConsentType.DataProcessing)).toBe(false);
      expect(facade.isConsentGranted(ConsentType.PrivacyPolicy)).toBe(false);
    });

    it('grants via the gdpr endpoint, shows success and re-fetches', () => {
      const facade = createFacade(true);
      gdprClient.consentsPost.mockReturnValue(of(undefined));
      gdprClient.consentsGet.mockReturnValue(of(consentRows));

      facade.toggleConsent(ConsentType.MarketingEmails, true);

      const command = gdprClient.consentsPost.mock
        .calls[0][0] as GrantConsentCommand;
      expect(command.consentType).toBe(ConsentType.MarketingEmails);
      expect(consentsClient.withdraw).not.toHaveBeenCalled();
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.gdpr.consent_updated'
      );
      expect(gdprClient.consentsGet).toHaveBeenCalledTimes(1);
    });

    it('withdraws via the consents endpoint and re-fetches', () => {
      const facade = createFacade(true);
      consentsClient.withdraw.mockReturnValue(of(undefined));
      gdprClient.consentsGet.mockReturnValue(of(consentRows));

      facade.toggleConsent(ConsentType.MarketingEmails, false);

      const command = consentsClient.withdraw.mock
        .calls[0][0] as WithdrawConsentCommand;
      expect(command.consentType).toBe(ConsentType.MarketingEmails);
      expect(gdprClient.consentsPost).not.toHaveBeenCalled();
      expect(gdprClient.consentsGet).toHaveBeenCalledTimes(1);
    });

    it('surfaces the API error and re-fetches so the toggle reflects server state', () => {
      const facade = createFacade(true);
      const error = new Error('blocked');
      gdprClient.consentsPost.mockReturnValue(throwError(() => error));
      gdprClient.consentsGet.mockReturnValue(of(consentRows));

      facade.toggleConsent(ConsentType.MarketingEmails, true);

      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.gdpr.consent_error'
      );
      expect(gdprClient.consentsGet).toHaveBeenCalledTimes(1);
    });
  });

  describe('export', () => {
    it('downloads my-data-export.json and shows success', () => {
      const facade = createFacade(true);
      gdprClient.export.mockReturnValue(
        of(GdprExportDto.fromJS({ userId: 'me' }))
      );
      const download = jest
        .spyOn(
          facade as unknown as {
            downloadJson: (d: unknown, n: string) => void;
          },
          'downloadJson'
        )
        .mockImplementation(() => undefined);

      facade.exportData();

      expect(download).toHaveBeenCalledWith(
        expect.anything(),
        'my-data-export.json'
      );
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.gdpr.export_success'
      );
      expect(facade.exporting()).toBe(false);
    });

    it('surfaces the API error and downloads nothing on failure', () => {
      const facade = createFacade(true);
      const error = new Error('500');
      gdprClient.export.mockReturnValue(throwError(() => error));
      const download = jest.spyOn(
        facade as unknown as { downloadJson: (d: unknown, n: string) => void },
        'downloadJson'
      );

      facade.exportData();

      expect(download).not.toHaveBeenCalled();
      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.gdpr.export_error'
      );
      expect(facade.exporting()).toBe(false);
    });

    it('ignores a second export while one is in flight', () => {
      const facade = createFacade(true);
      facade.exporting.set(true);

      facade.exportData();

      expect(gdprClient.export).not.toHaveBeenCalled();
    });
  });

  describe('delete account', () => {
    it('deletes, shows success and logs the partner out', () => {
      const facade = createFacade(true);
      gdprClient.deleteAccount.mockReturnValue(of(undefined));
      authService.logout.mockReturnValue(of(true));

      facade.deleteAccount();

      expect(gdprClient.deleteAccount).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.gdpr.delete_success'
      );
      expect(authService.logout).toHaveBeenCalledTimes(1);
      expect(facade.deleting()).toBe(false);
    });

    it('surfaces the blocked-deletion error and keeps the partner logged in', () => {
      const facade = createFacade(true);
      const error = {
        result: { detail: 'gdpr.deletion_blocked_by_order' },
      };
      gdprClient.deleteAccount.mockReturnValue(throwError(() => error));

      facade.deleteAccount();

      expect(snackbar.showApiError).toHaveBeenCalledWith(
        error,
        'pages.gdpr.delete_error'
      );
      expect(authService.logout).not.toHaveBeenCalled();
      expect(facade.deleting()).toBe(false);
    });

    it('ignores a second delete while one is in flight', () => {
      const facade = createFacade(true);
      facade.deleting.set(true);

      facade.deleteAccount();

      expect(gdprClient.deleteAccount).not.toHaveBeenCalled();
    });
  });
});
