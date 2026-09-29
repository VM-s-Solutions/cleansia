import { TestBed } from '@angular/core/testing';
import {
  ConsentType,
  GdprClient,
  GdprExportDto,
  PartnerAuthService,
  UserConsentDto,
} from '@cleansia/partner-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { PartnerGdprFacade } from './gdpr.facade';

describe('PartnerGdprFacade', () => {
  let gdprClient: {
    consentsGet: jest.Mock;
    export: jest.Mock;
    deleteAccount: jest.Mock;
  };
  let authService: { isLoggedIn: jest.Mock; logout: jest.Mock };
  let confirmMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock; showSuccessTranslated: jest.Mock;
    showError: jest.Mock; showErrorTranslated: jest.Mock;
    showApiError: jest.Mock;
  };

  const grantedAt = new Date('2026-09-01T10:00:00Z');

  const consent = (
    consentType: ConsentType,
    overrides: Partial<UserConsentDto> = {}
  ): UserConsentDto =>
    UserConsentDto.fromJS({
      id: `c-${consentType}`,
      consentType,
      isGranted: true,
      grantedAt: grantedAt.toISOString(),
      createdOn: grantedAt.toISOString(),
      documentVersion: '2026-09',
      coversCurrentVersion: true,
      ...overrides,
    });

  const createFacade = (loggedIn: boolean): PartnerGdprFacade => {
    authService.isLoggedIn.mockReturnValue(loggedIn);

    TestBed.configureTestingModule({
      providers: [
        PartnerGdprFacade,
        { provide: GdprClient, useValue: gdprClient },
        { provide: PartnerAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) => k,
            currentLang: 'en',
            onLangChange: new Subject(),
          },
        },
      ],
    });

    return TestBed.inject(PartnerGdprFacade);
  };

  beforeEach(() => {
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      export: jest.fn(),
      deleteAccount: jest.fn(),
    };
    authService = { isLoggedIn: jest.fn(), logout: jest.fn() };
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = {
      showSuccess: jest.fn(), showSuccessTranslated: jest.fn(),
      showError: jest.fn(), showErrorTranslated: jest.fn(),
      showApiError: jest.fn(),
    };
  });

  describe('loading the consents', () => {
    it('is loading while the read is in flight', () => {
      const facade = createFacade(true);
      const consents$ = new Subject<UserConsentDto[]>();
      gdprClient.consentsGet.mockReturnValue(consents$.asObservable());

      facade.loadConsents();
      expect(facade.loadingConsents()).toBe(true);

      consents$.next([]);
      consents$.complete();
      expect(facade.loadingConsents()).toBe(false);
      expect(facade.consentsError()).toBe(false);
    });

    it('never calls the consents endpoint while unauthenticated', () => {
      const facade = createFacade(false);

      facade.loadConsents();

      expect(gdprClient.consentsGet).not.toHaveBeenCalled();
      expect(facade.loadingConsents()).toBe(false);
    });

    it('turns to the error state when the read fails, rather than reading as not accepted', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadConsents();

      expect(facade.consentsError()).toBe(true);
      expect(facade.loadingConsents()).toBe(false);
    });

    it('clears a previous error when retried', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValueOnce(throwError(() => new Error('boom')));
      facade.loadConsents();

      facade.loadConsents();

      expect(facade.consentsError()).toBe(false);
    });

    it('holds an empty consent list when the read emits null', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(of(null));

      facade.loadConsents();

      expect(facade.consents()).toEqual([]);
      expect(facade.legalConsents().map((row) => row.detailKey)).toEqual([
        'pages.gdpr.legal.not_accepted',
        'pages.gdpr.legal.not_accepted',
      ]);
    });
  });

  describe('terms and privacy, read-only', () => {
    it('shows the terms and the privacy policy, and nothing else', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(
        of([
          consent(ConsentType.TermsOfService),
          consent(ConsentType.PrivacyPolicy),
          consent(ConsentType.MarketingEmails),
          consent(ConsentType.DataProcessing),
          consent(ConsentType.CleanerFrameworkContract),
        ])
      );

      facade.loadConsents();

      expect(facade.legalConsents().map((row) => row.type)).toEqual([
        ConsentType.TermsOfService,
        ConsentType.PrivacyPolicy,
      ]);
    });

    it('states the accepted version and the date it was accepted', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(of([consent(ConsentType.TermsOfService)]));

      facade.loadConsents();

      const [terms] = facade.legalConsents();
      expect(terms.labelKey).toBe('pages.gdpr.consent_types.terms_of_service');
      expect(terms.detailKey).toBe('pages.gdpr.legal.accepted_version');
      expect(terms.detailParams).toEqual({
        version: '2026-09',
        date: formatDate(grantedAt, 'en'),
      });
    });

    it('states the date alone for an acceptance recorded without a version', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(
        of([consent(ConsentType.PrivacyPolicy, { documentVersion: undefined })])
      );

      facade.loadConsents();

      const privacy = facade.legalConsents()[1];
      expect(privacy.labelKey).toBe('pages.gdpr.consent_types.privacy_policy');
      expect(privacy.detailKey).toBe('pages.gdpr.legal.accepted');
      expect(privacy.detailParams.date).toBe(formatDate(grantedAt, 'en'));
    });

    it('treats a withdrawn row as not accepted', () => {
      const facade = createFacade(true);
      gdprClient.consentsGet.mockReturnValue(
        of([consent(ConsentType.TermsOfService, { isGranted: false })])
      );

      facade.loadConsents();

      const [terms] = facade.legalConsents();
      expect(terms.detailKey).toBe('pages.gdpr.legal.not_accepted');
      expect(terms.detailParams).toEqual({ version: '', date: '' });
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
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
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
    // The endpoint files a Pending GdprRequest for a cleaner and changes nothing else — ending a
    // working relationship needs signed paperwork and an in-person step. Announcing "deleted" and
    // signing them out was true when the backend erased on the spot; it is a lie now, and the
    // cleaner has to keep working until an admin fulfils the request.
    it('files the request, says so, and keeps the partner signed in', () => {
      const facade = createFacade(true);
      gdprClient.deleteAccount.mockReturnValue(of(undefined));

      facade.deleteAccount();

      expect(gdprClient.deleteAccount).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.gdpr.delete_requested'
      );
      expect(authService.logout).not.toHaveBeenCalled();
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

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));
      const facade = createFacade(true);

      facade.deleteAccount();

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.gdpr.delete_confirm_message',
        'pages.gdpr.delete_confirm_title',
        undefined,
        { danger: true, acceptLabelKey: 'pages.gdpr.delete_confirm_yes' }
      );
      expect(gdprClient.deleteAccount).not.toHaveBeenCalled();
      expect(facade.deleting()).toBe(false);
    });
  });
});
