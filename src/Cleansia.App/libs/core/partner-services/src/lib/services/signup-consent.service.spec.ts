import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import {
  ConsentType,
  GdprClient,
  GrantConsentCommand,
  UserConsentDto,
} from '../client/partner-client';
import { SignupConsentService } from './signup-consent.service';

describe('SignupConsentService', () => {
  let service: SignupConsentService;
  let gdprClient: { consentsGet: jest.Mock; consentsPost: jest.Mock };

  function consentRow(
    consentType: ConsentType,
    isGranted: boolean
  ): UserConsentDto {
    return UserConsentDto.fromJS({
      id: 'consent-1',
      consentType,
      isGranted,
      createdOn: '2026-01-01T00:00:00Z',
    });
  }

  function grantedBodies(): Record<string, unknown>[] {
    return gdprClient.consentsPost.mock.calls.map(([command]) => {
      expect(command).toBeInstanceOf(GrantConsentCommand);
      return (command as GrantConsentCommand).toJSON();
    });
  }

  /** The literal ProblemDetails `GrantConsent`'s failure arm produces. */
  function alreadyGranted(): unknown {
    return {
      title: 'Bad Request',
      type: 'ConsentType',
      detail: 'gdpr.consent_already_granted',
      status: 400,
      errors: { ConsentType: 'gdpr.consent_already_granted' },
    };
  }

  /**
   * The same refusal with the two `Error` slots swapped — the shape that shipped before the fix.
   * `extractApiErrorCode` reads the bag VALUE, so this yields the prose and matches nothing.
   */
  function alreadyGrantedWithSwappedSlots(): unknown {
    return {
      title: 'Bad Request',
      type: 'gdpr.consent_already_granted',
      detail: 'Consent already granted',
      status: 400,
      errors: { 'gdpr.consent_already_granted': 'Consent already granted' },
    };
  }

  beforeEach(() => {
    localStorage.clear();
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };

    TestBed.configureTestingModule({
      providers: [
        SignupConsentService,
        { provide: GdprClient, useValue: gdprClient },
      ],
    });

    service = TestBed.inject(SignupConsentService);
  });

  it('grants exactly the two documents the signup tick names', () => {
    service.record('  Cleaner@Example.COM ');

    service.flush('cleaner@example.com');

    expect(grantedBodies()).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
    expect(grantedBodies().map((body) => Object.keys(body))).toEqual([
      ['consentType'],
      ['consentType'],
    ]);
    expect(grantedBodies().map((body) => body['consentType'])).not.toContain(
      ConsentType.MarketingEmails
    );
  });

  it('never delivers one account’s tick into another account’s session', () => {
    service.record('cleaner@example.com');

    service.flush('someone-else@example.com');

    expect(gdprClient.consentsGet).not.toHaveBeenCalled();
    expect(gdprClient.consentsPost).not.toHaveBeenCalled();

    service.flush('cleaner@example.com');

    expect(grantedBodies()).toHaveLength(2);
  });

  it('does nothing at all when no tick was recorded', () => {
    service.flush('cleaner@example.com');

    expect(gdprClient.consentsGet).not.toHaveBeenCalled();
    expect(gdprClient.consentsPost).not.toHaveBeenCalled();
  });

  it('counts an already-granted refusal as delivered', () => {
    gdprClient.consentsPost.mockReturnValue(throwError(() => alreadyGranted()));
    service.record('cleaner@example.com');

    service.flush('cleaner@example.com');
    service.flush('cleaner@example.com');

    expect(gdprClient.consentsPost).toHaveBeenCalledTimes(2);
  });

  it('cannot recognise the refusal when the backend swaps the Error slots', () => {
    gdprClient.consentsPost.mockReturnValue(
      throwError(() => alreadyGrantedWithSwappedSlots())
    );
    service.record('cleaner@example.com');

    service.flush('cleaner@example.com');
    service.flush('cleaner@example.com');

    expect(gdprClient.consentsPost).toHaveBeenCalledTimes(4);
  });

  it('keeps the tick for the next session when a grant fails for a real reason', () => {
    gdprClient.consentsPost.mockReturnValue(
      throwError(() => ({ errors: { '': 'common.error_occurred' } }))
    );
    service.record('cleaner@example.com');

    service.flush('cleaner@example.com');
    service.flush('cleaner@example.com');

    expect(gdprClient.consentsPost).toHaveBeenCalledTimes(4);
  });

  it('keeps the tick when the consent read fails', () => {
    gdprClient.consentsGet.mockReturnValueOnce(
      throwError(() => new Error('offline'))
    );
    service.record('cleaner@example.com');

    service.flush('cleaner@example.com');

    expect(gdprClient.consentsPost).not.toHaveBeenCalled();

    service.flush('cleaner@example.com');

    expect(grantedBodies()).toHaveLength(2);
  });

  it('never re-grants a consent the account has since answered for itself', () => {
    gdprClient.consentsGet.mockReturnValue(
      of([consentRow(ConsentType.PrivacyPolicy, false)])
    );
    service.record('cleaner@example.com');

    service.flush('cleaner@example.com');

    expect(grantedBodies()).toEqual([
      { consentType: ConsentType.TermsOfService },
    ]);

    service.flush('cleaner@example.com');

    expect(grantedBodies()).toHaveLength(1);
  });

  it('survives a storage that refuses to write', () => {
    const setItem = jest
      .spyOn(Storage.prototype, 'setItem')
      .mockImplementation(() => {
        throw new Error('quota');
      });

    expect(() => service.record('cleaner@example.com')).not.toThrow();

    setItem.mockRestore();
  });

  it('survives a client that throws on the spot', () => {
    gdprClient.consentsGet.mockImplementation(() => {
      throw new Error('boom');
    });
    service.record('cleaner@example.com');

    expect(() => service.flush('cleaner@example.com')).not.toThrow();
  });
});
