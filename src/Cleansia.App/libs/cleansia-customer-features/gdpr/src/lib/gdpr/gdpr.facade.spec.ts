import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ConsentType,
  CustomerAuthService,
  CustomerClient,
  NotificationPreferencesDto,
  UpdateNotificationPreferencesCommand,
  UserConsentDto,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { GdprFacade } from './gdpr.facade';

describe('GdprFacade (customer)', () => {
  let gdprClient: { consentsGet: jest.Mock };
  let notificationPreferencesClient: { getMine: jest.Mock; update: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let facade: GdprFacade;

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

  const preferences = (promo: boolean): NotificationPreferencesDto =>
    NotificationPreferencesDto.fromJS({
      orderUpdates: true,
      cleanerOnTheWay: true,
      orderCompleted: true,
      orderCancelled: true,
      refundIssued: true,
      membershipExpiring: true,
      membershipCancelled: true,
      tierUpgrade: true,
      promo,
      disputeReply: false,
      recurringScheduled: true,
    });

  beforeEach(() => {
    TestBed.resetTestingModule();
    gdprClient = { consentsGet: jest.fn().mockReturnValue(of([])) };
    notificationPreferencesClient = {
      getMine: jest.fn().mockReturnValue(of(preferences(false))),
      update: jest.fn(),
    };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        GdprFacade,
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: CustomerClient,
          useValue: { gdprClient, notificationPreferencesClient },
        },
        {
          provide: CustomerAuthService,
          useValue: { isLoggedIn: () => true, logout: () => of(undefined) },
        },
        { provide: SnackbarService, useValue: snackbar },
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

    facade = TestBed.inject(GdprFacade);
  });

  describe('loading the consent section', () => {
    it('is loading while either read is in flight', () => {
      const consents$ = new Subject<UserConsentDto[]>();
      gdprClient.consentsGet.mockReturnValue(consents$.asObservable());

      facade.loadConsents();
      expect(facade.loadingConsents()).toBe(true);

      consents$.next([]);
      consents$.complete();
      expect(facade.loadingConsents()).toBe(false);
      expect(facade.consentsError()).toBe(false);
    });

    it('turns to the error state when a read fails', () => {
      notificationPreferencesClient.getMine.mockReturnValue(
        throwError(() => new Error('x'))
      );

      facade.loadConsents();

      expect(facade.consentsError()).toBe(true);
      expect(facade.loadingConsents()).toBe(false);
    });

    it('clears a previous error when retried', () => {
      gdprClient.consentsGet.mockReturnValueOnce(
        throwError(() => new Error('x'))
      );
      facade.loadConsents();

      facade.loadConsents();

      expect(facade.consentsError()).toBe(false);
    });

    // The generated client emits NULL from an array-returning method for a 200 whose body is not a
    // JSON array, and for a 204, while `consentsGet` is declared to return a list.
    it('holds an empty consent list when the read emits null', () => {
      gdprClient.consentsGet.mockReturnValue(of(null));

      facade.loadConsents();

      expect(facade.consents()).toEqual([]);
      expect(facade.loadingConsents()).toBe(false);
      expect(facade.legalConsents().map((row) => row.detailKey)).toEqual([
        'pages.gdpr.legal.not_accepted',
        'pages.gdpr.legal.not_accepted',
      ]);
    });
  });

  describe('terms and privacy, read-only', () => {
    it('shows the terms and the privacy policy, and nothing else', () => {
      gdprClient.consentsGet.mockReturnValue(
        of([
          consent(ConsentType.TermsOfService),
          consent(ConsentType.PrivacyPolicy),
          consent(ConsentType.MarketingEmails),
          consent(ConsentType.DataProcessing),
        ])
      );

      facade.loadConsents();

      expect(facade.legalConsents().map((row) => row.type)).toEqual([
        ConsentType.TermsOfService,
        ConsentType.PrivacyPolicy,
      ]);
    });

    it('states the accepted version and the date it was accepted', () => {
      gdprClient.consentsGet.mockReturnValue(
        of([consent(ConsentType.TermsOfService)])
      );

      facade.loadConsents();

      const [terms] = facade.legalConsents();
      expect(terms.labelKey).toBe('pages.gdpr.consent_types.terms_of_service');
      expect(terms.detailKey).toBe('pages.gdpr.legal.accepted_version');
      expect(terms.detailParams).toEqual({
        version: '2026-09',
        date: formatDate(grantedAt, 'en'),
      });
      expect(terms.newerVersionInForce).toBe(false);
    });

    it('states the date alone for an acceptance recorded without a version', () => {
      gdprClient.consentsGet.mockReturnValue(
        of([
          consent(ConsentType.PrivacyPolicy, {
            documentVersion: undefined,
            coversCurrentVersion: false,
          }),
        ])
      );

      facade.loadConsents();

      const privacy = facade.legalConsents()[1];
      expect(privacy.detailKey).toBe('pages.gdpr.legal.accepted');
      expect(privacy.detailParams.date).toBe(formatDate(grantedAt, 'en'));
      expect(privacy.newerVersionInForce).toBe(true);
    });

    it('flags an acceptance of an older version than the one in force', () => {
      gdprClient.consentsGet.mockReturnValue(
        of([
          consent(ConsentType.TermsOfService, { coversCurrentVersion: false }),
          consent(ConsentType.PrivacyPolicy),
        ])
      );

      facade.loadConsents();

      expect(
        facade.legalConsents().map((row) => row.newerVersionInForce)
      ).toEqual([true, false]);
    });

    it('treats a withdrawn row as not accepted, with no newer-version note', () => {
      gdprClient.consentsGet.mockReturnValue(
        of([
          consent(ConsentType.TermsOfService, {
            isGranted: false,
            coversCurrentVersion: false,
          }),
        ])
      );

      facade.loadConsents();

      const [terms] = facade.legalConsents();
      expect(terms.detailKey).toBe('pages.gdpr.legal.not_accepted');
      expect(terms.newerVersionInForce).toBe(false);
    });
  });

  describe('marketing consent is the promo push preference', () => {
    beforeEach(() => {
      notificationPreferencesClient.getMine.mockReturnValue(
        of(preferences(false))
      );
      facade.loadConsents();
    });

    it('reads the promo preference', () => {
      expect(facade.marketingConsent()).toBe(false);
    });

    it('saves every preference as it was, with promo changed', () => {
      notificationPreferencesClient.update.mockReturnValue(
        of(preferences(true))
      );

      facade.setMarketingConsent(true);

      const command: UpdateNotificationPreferencesCommand =
        notificationPreferencesClient.update.mock.calls[0][0];
      expect(command).toBeInstanceOf(UpdateNotificationPreferencesCommand);
      expect(command.toJSON()).toEqual({
        ...preferences(false).toJSON(),
        promo: true,
      });
      expect(facade.marketingConsent()).toBe(true);
      expect(facade.savingMarketing()).toBe(false);
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.gdpr.consent_updated'
      );
    });

    it('shows the new value while the save is in flight', () => {
      const saved$ = new Subject<NotificationPreferencesDto>();
      notificationPreferencesClient.update.mockReturnValue(
        saved$.asObservable()
      );

      facade.setMarketingConsent(true);

      expect(facade.marketingConsent()).toBe(true);
      expect(facade.savingMarketing()).toBe(true);
    });

    it('puts the switch back and says so when the save fails', () => {
      notificationPreferencesClient.update.mockReturnValue(
        throwError(() => new Error('x'))
      );

      facade.setMarketingConsent(true);

      expect(facade.marketingConsent()).toBe(false);
      expect(facade.savingMarketing()).toBe(false);
      expect(snackbar.showError).toHaveBeenCalledWith(
        'pages.gdpr.consent_error'
      );
    });

    it('does not save while a save is in flight', () => {
      notificationPreferencesClient.update.mockReturnValue(
        new Subject<NotificationPreferencesDto>().asObservable()
      );

      facade.setMarketingConsent(true);
      facade.setMarketingConsent(false);

      expect(notificationPreferencesClient.update).toHaveBeenCalledTimes(1);
    });
  });

  it('does not save the marketing consent before the preferences are read', () => {
    facade.setMarketingConsent(true);

    expect(notificationPreferencesClient.update).not.toHaveBeenCalled();
  });
});
