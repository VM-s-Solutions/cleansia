import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  ChangePasswordCommand,
  PartnerClient,
  RequestPasswordChangeCommand,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ForgotPasswordFacade } from './forgot-password.facade';

describe('ForgotPasswordFacade (partner)', () => {
  let facade: ForgotPasswordFacade;
  let userClient: { requestPasswordChange: jest.Mock; changePassword: jest.Mock };
  let snackbar: { showError: jest.Mock; showApiError: jest.Mock; showSuccess: jest.Mock };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    // sendCode arms a resend cooldown interval; fake timers keep it off the real event loop.
    jest.useFakeTimers();
    userClient = { requestPasswordChange: jest.fn(), changePassword: jest.fn() };
    snackbar = { showError: jest.fn(), showApiError: jest.fn(), showSuccess: jest.fn() };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ForgotPasswordFacade,
        { provide: Router, useValue: router },
        { provide: PartnerClient, useValue: { userClient } },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) => k,
            currentLang: 'cs',
            getDefaultLang: () => 'en',
          },
        },
      ],
    });

    facade = TestBed.inject(ForgotPasswordFacade);
  });

  afterEach(() => {
    jest.clearAllTimers();
    jest.useRealTimers();
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

  it('sendCode error surfaces via showApiError, re-enables resend and clears loading', () => {
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

  it('changePassword error surfaces via showApiError and does not navigate', () => {
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

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes the code request with the email and the active language', () => {
      userClient.requestPasswordChange.mockReturnValue(of(undefined));
      facade.emailFormGroup.setValue({ email: 'jan@example.com' });

      facade.sendCode();

      const command: RequestPasswordChangeCommand =
        userClient.requestPasswordChange.mock.calls[0][0];
      expect(command).toBeInstanceOf(RequestPasswordChangeCommand);
      expect(command.toJSON()).toEqual({
        email: 'jan@example.com',
        language: 'cs',
      });
    });

    it('serializes the password change with the email, the code and the new password', () => {
      userClient.changePassword.mockReturnValue(of(undefined));
      facade.emailFormGroup.setValue({ email: 'jan@example.com' });
      facade.passwordFormGroup.setValue({
        code: '123456',
        password: 'Heslo1234',
        confirmPassword: 'Heslo1234',
      });

      facade.changePassword();

      const command: ChangePasswordCommand =
        userClient.changePassword.mock.calls[0][0];
      expect(command).toBeInstanceOf(ChangePasswordCommand);
      expect(command.toJSON()).toEqual({
        email: 'jan@example.com',
        code: '123456',
        newPassword: 'Heslo1234',
      });
    });
  });
});
