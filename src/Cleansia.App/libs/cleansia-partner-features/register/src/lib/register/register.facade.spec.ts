import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  ConsentType,
  GdprClient,
  GrantConsentCommand,
  PartnerAuthService,
  SignupConsentService,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { RegisterFacade } from './register.facade';

describe('RegisterFacade — the consent ticked at signup', () => {
  let facade: RegisterFacade;
  let signupConsent: SignupConsentService;
  let gdprClient: Record<string, jest.Mock>;
  let authService: { registerEmployee: jest.Mock };
  let router: { navigate: jest.Mock };
  let snackbar: { showError: jest.Mock };

  const EMAIL = 'cleaner@example.com';

  function fillForm(terms: boolean): void {
    facade.formGroup.patchValue({
      firstName: 'Petr',
      lastName: 'Dvořák',
      email: EMAIL,
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
      terms,
    });
  }

  function grantedBodies(): Record<string, unknown>[] {
    return gdprClient['consentsPost'].mock.calls.map(([command]) => {
      expect(command).toBeInstanceOf(GrantConsentCommand);
      return (command as GrantConsentCommand).toJSON();
    });
  }

  beforeEach(() => {
    localStorage.clear();
    authService = { registerEmployee: jest.fn().mockReturnValue(of(true)) };
    router = { navigate: jest.fn() };
    snackbar = { showError: jest.fn() };
    gdprClient = {
      consentsGet: jest.fn().mockReturnValue(of([])),
      consentsPost: jest.fn().mockReturnValue(of(undefined)),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        { provide: Router, useValue: router },
        { provide: PartnerAuthService, useValue: authService },
        { provide: GdprClient, useValue: gdprClient },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
    signupConsent = TestBed.inject(SignupConsentService);
  });

  it('grants the ticked documents at the session that follows the signup', () => {
    fillForm(true);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(grantedBodies()).toEqual([
      { consentType: ConsentType.TermsOfService },
      { consentType: ConsentType.PrivacyPolicy },
    ]);
  });

  it('grants nothing when the registration itself failed', () => {
    authService.registerEmployee.mockReturnValue(
      throwError(() => new Error('taken'))
    );
    fillForm(true);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
  });

  it('refuses to register at all while the box is unticked', () => {
    fillForm(false);

    facade.register();

    expect(authService.registerEmployee).not.toHaveBeenCalled();
  });

  // Unreachable in the shipped form, which the test above pins: `terms` is
  // `requiredTrue`, so an unticked submit never reaches the grant. This pins the
  // guard, not its reachability — it is what keeps an untick from becoming a
  // manufactured record if the tick ever stops being required.
  it('grants nothing for an absent tick when the form does not require one', () => {
    const terms = facade.formGroup.get('terms');
    terms?.clearValidators();
    terms?.updateValueAndValidity();
    fillForm(false);

    facade.register();
    signupConsent.flush(EMAIL);

    expect(authService.registerEmployee).toHaveBeenCalled();
    expect(gdprClient['consentsPost']).not.toHaveBeenCalled();
  });

  it('completes the signup even when the tick cannot be parked for delivery', () => {
    const setItem = jest
      .spyOn(Storage.prototype, 'setItem')
      .mockImplementation(() => {
        throw new Error('quota');
      });
    fillForm(true);

    expect(() => facade.register()).not.toThrow();

    setItem.mockRestore();
    expect(router.navigate).toHaveBeenCalled();
  });
});
