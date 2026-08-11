import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ConsentType,
  CustomerAuthService,
  CustomerClient,
  GrantConsentCommand,
  WithdrawConsentCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { GdprFacade } from './gdpr.facade';

describe('GdprFacade (customer)', () => {
  let gdprClient: { consentsGet: jest.Mock; consentsPost: jest.Mock };
  let consentsClient: { withdraw: jest.Mock };
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let facade: GdprFacade;

  beforeEach(() => {
    TestBed.resetTestingModule();
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };
    consentsClient = { withdraw: jest.fn().mockReturnValue(of(undefined)) };
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        GdprFacade,
        { provide: PLATFORM_ID, useValue: 'browser' },
        {
          provide: CustomerClient,
          useValue: { gdprClient, consentsClient },
        },
        {
          provide: CustomerAuthService,
          useValue: { isLoggedIn: () => true, logout: () => of(undefined) },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(GdprFacade);
  });

  it('grants through the gdpr endpoint and re-reads the consent list', () => {
    facade.toggleConsent(ConsentType.MarketingEmails, true);

    expect(consentsClient.withdraw).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.gdpr.consent_updated'
    );
    expect(gdprClient.consentsGet).toHaveBeenCalledTimes(1);
  });

  it('withdraws through the consents endpoint and re-reads the consent list', () => {
    facade.toggleConsent(ConsentType.MarketingEmails, false);

    expect(gdprClient.consentsPost).not.toHaveBeenCalled();
    expect(gdprClient.consentsGet).toHaveBeenCalledTimes(1);
  });

  it('re-reads on failure too, so the toggle reflects the server', () => {
    gdprClient.consentsPost.mockReturnValue(throwError(() => new Error('nope')));

    facade.toggleConsent(ConsentType.MarketingEmails, true);

    expect(snackbar.showError).toHaveBeenCalledWith('pages.gdpr.consent_error');
    expect(gdprClient.consentsGet).toHaveBeenCalledTimes(1);
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a grant with the consent type alone', () => {
      facade.toggleConsent(ConsentType.MarketingEmails, true);

      const command: GrantConsentCommand =
        gdprClient.consentsPost.mock.calls[0][0];
      expect(command).toBeInstanceOf(GrantConsentCommand);
      // IP and user agent are captured server-side for legal-audit integrity.
      expect(command.toJSON()).toEqual({
        consentType: ConsentType.MarketingEmails,
      });
    });

    it('serializes a withdrawal with the consent type alone', () => {
      facade.toggleConsent(ConsentType.DataProcessing, false);

      const command: WithdrawConsentCommand =
        consentsClient.withdraw.mock.calls[0][0];
      expect(command).toBeInstanceOf(WithdrawConsentCommand);
      expect(command.toJSON()).toEqual({
        consentType: ConsentType.DataProcessing,
      });
    });
  });
});
