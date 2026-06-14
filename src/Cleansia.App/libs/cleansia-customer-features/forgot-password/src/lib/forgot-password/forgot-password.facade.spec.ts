import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { CustomerClient } from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ForgotPasswordFacade } from './forgot-password.facade';

describe('ForgotPasswordFacade (customer)', () => {
  let facade: ForgotPasswordFacade;
  let userClient: { requestPasswordChange: jest.Mock; changePassword: jest.Mock };
  let snackbar: { showError: jest.Mock; showApiError: jest.Mock; showSuccess: jest.Mock };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    userClient = { requestPasswordChange: jest.fn(), changePassword: jest.fn() };
    snackbar = { showError: jest.fn(), showApiError: jest.fn(), showSuccess: jest.fn() };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ForgotPasswordFacade,
        { provide: Router, useValue: router },
        { provide: CustomerClient, useValue: { userClient } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'en', getDefaultLang: () => 'en' },
        },
      ],
    });

    facade = TestBed.inject(ForgotPasswordFacade);
  });

  it('rejects an invalid email without calling the API', () => {
    facade.sendCode();

    expect(snackbar.showError).toHaveBeenCalled();
    expect(userClient.requestPasswordChange).not.toHaveBeenCalled();
  });

  it('sendCode success transitions to email-sent and clears loading', () => {
    userClient.requestPasswordChange.mockReturnValue(of(undefined));
    facade.emailFormGroup.setValue({ email: 'jan@example.com' });

    facade.sendCode();

    expect(facade.isEmailSent()).toBe(true);
    expect(facade.loading()).toBe(false);
  });

  it('sendCode error surfaces via showApiError, re-enables resend, clears loading (IA-8)', () => {
    userClient.requestPasswordChange.mockReturnValue(throwError(() => ({ message: 'x' })));
    facade.emailFormGroup.setValue({ email: 'jan@example.com' });

    facade.sendCode();

    expect(snackbar.showApiError).toHaveBeenCalledWith(
      expect.anything(),
      'pages.forgot_password.send_code_error'
    );
    expect(facade.isEmailSent()).toBe(false);
    expect(facade.isResendDisabled()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  it('changePassword error surfaces via showApiError and clears loading (IA-8)', () => {
    userClient.changePassword.mockReturnValue(throwError(() => ({ message: 'x' })));
    facade.emailFormGroup.setValue({ email: 'jan@example.com' });
    facade.passwordFormGroup.setValue({
      code: '123456',
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
    });

    facade.changePassword();

    expect(snackbar.showApiError).toHaveBeenCalledWith(
      expect.anything(),
      'pages.forgot_password.change_password_error'
    );
    expect(facade.loading()).toBe(false);
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('changePassword success shows success and navigates to login', () => {
    userClient.changePassword.mockReturnValue(of(undefined));
    facade.emailFormGroup.setValue({ email: 'jan@example.com' });
    facade.passwordFormGroup.setValue({
      code: '123456',
      password: 'Heslo1234',
      confirmPassword: 'Heslo1234',
    });

    facade.changePassword();

    expect(snackbar.showSuccess).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
    expect(facade.isEmailSent()).toBe(false);
  });

  it('accepts a backend-valid password (letter + digit, min 8)', () => {
    facade.passwordFormGroup.setValue({
      code: '123456',
      password: 'abcd1234',
      confirmPassword: 'abcd1234',
    });

    expect(facade.passwordFormGroup.get('password')?.valid).toBe(true);
  });
});
