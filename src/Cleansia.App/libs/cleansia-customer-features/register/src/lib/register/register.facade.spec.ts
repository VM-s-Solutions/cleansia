import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  ValidateReferralResponse,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { Subject, of, throwError } from 'rxjs';
import { RegisterFacade } from './register.facade';

describe('RegisterFacade — referral landing capture (/r/{code})', () => {
  let facade: RegisterFacade;
  let referralClient: { validate: jest.Mock };
  let authService: { register: jest.Mock; authenticateWithGoogle: jest.Mock };
  let snackbar: {
    showError: jest.Mock;
    showApiError: jest.Mock;
    showSuccessTranslated: jest.Mock;
  };

  const validResponse = ValidateReferralResponse.fromJS({
    isValid: true,
    referrerFirstName: 'Petra',
  });

  beforeEach(() => {
    referralClient = { validate: jest.fn() };
    authService = {
      register: jest.fn().mockReturnValue(of({})),
      authenticateWithGoogle: jest.fn(),
    };
    snackbar = {
      showError: jest.fn(),
      showApiError: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        RegisterFacade,
        provideMockStore(),
        { provide: Router, useValue: { navigate: jest.fn() } },
        { provide: CustomerAuthService, useValue: authService },
        { provide: CustomerClient, useValue: { referralClient } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(RegisterFacade);
  });

  it('captures and normalizes the URL code into the signal and form control before validation resolves', () => {
    const pending$ = new Subject<ValidateReferralResponse>();
    referralClient.validate.mockReturnValue(pending$.asObservable());

    facade.applyReferralCodeFromUrl('  abc12 ');

    expect(facade.referralCode()).toBe('ABC12');
    expect(facade.formGroup.get('referralCode')?.value).toBe('ABC12');
    expect(facade.referralState()).toEqual({ kind: 'validating' });
  });

  it('validates exactly once and reaches the valid state with the referrer first name', async () => {
    referralClient.validate.mockReturnValue(of(validResponse));

    await facade.applyReferralCodeFromUrl('abc12');

    expect(referralClient.validate).toHaveBeenCalledTimes(1);
    expect(referralClient.validate.mock.calls[0][0].code).toBe('ABC12');
    expect(facade.referralState()).toEqual({
      kind: 'valid',
      referrerFirstName: 'Petra',
    });
    expect(facade.referralCode()).toBe('ABC12');
  });

  it('keeps the code applied on an invalid response (fail-soft, backend skips bad codes)', async () => {
    referralClient.validate.mockReturnValue(
      of(ValidateReferralResponse.fromJS({ isValid: false, errorCode: 'NotFound' }))
    );

    await facade.applyReferralCodeFromUrl('badcode');

    expect(facade.referralState()).toEqual({
      kind: 'invalid',
      error: 'NotFound',
    });
    expect(facade.formGroup.get('referralCode')?.value).toBe('BADCODE');
    expect(facade.formGroup.get('referralCode')?.valid).toBe(true);
  });

  it('fails soft on a network failure — state invalid, form still submittable', async () => {
    referralClient.validate.mockReturnValue(
      throwError(() => new Error('network'))
    );

    await facade.applyReferralCodeFromUrl('abc12');

    expect(facade.referralState()).toEqual({ kind: 'invalid', error: null });
    expect(facade.formGroup.get('referralCode')?.value).toBe('ABC12');
    expect(facade.formGroup.get('referralCode')?.valid).toBe(true);
  });

  it('does nothing for an empty or missing code', () => {
    facade.applyReferralCodeFromUrl(null);
    facade.applyReferralCodeFromUrl('   ');

    expect(referralClient.validate).not.toHaveBeenCalled();
    expect(facade.referralState()).toEqual({ kind: 'idle' });
  });

  it('sends the captured code through to authService.register at signup', async () => {
    referralClient.validate.mockReturnValue(of(validResponse));
    await facade.applyReferralCodeFromUrl('abc12');

    facade.formGroup.patchValue({
      firstName: 'Jan',
      lastName: 'Novák',
      email: 'jan@example.com',
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
      terms: true,
    });

    facade.register();

    expect(authService.register).toHaveBeenCalledWith(
      'jan@example.com',
      'Heslo1234',
      'Jan',
      'Novák',
      'ABC12'
    );
  });
});
