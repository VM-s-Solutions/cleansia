import { TestBed } from '@angular/core/testing';
import { PartnerAuthService } from '@cleansia/partner-services';
import { loadUserCurrent } from '@cleansia/partner-stores';
import { SnackbarService } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { of, throwError } from 'rxjs';
import { ConfirmEmailFacade } from './confirm-email.facade';

describe('ConfirmEmailFacade (partner)', () => {
  let facade: ConfirmEmailFacade;
  let store: MockStore;
  let authService: {
    confirmUserEmail: jest.Mock;
    resendEmailConfirmation: jest.Mock;
  };
  let snackbar: {
    showErrorTranslated: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showApiError: jest.Mock;
  };

  beforeEach(() => {
    authService = {
      confirmUserEmail: jest.fn(),
      resendEmailConfirmation: jest.fn(),
    };
    snackbar = {
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showApiError: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        ConfirmEmailFacade,
        provideMockStore(),
        { provide: PartnerAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
      ],
    });

    facade = TestBed.inject(ConfirmEmailFacade);
    store = TestBed.inject(MockStore);
  });

  it('sends BOTH the code and the known email to the auth service', () => {
    authService.confirmUserEmail.mockReturnValue(of({}));
    facade.setEmail('jan@example.com');
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();

    expect(authService.confirmUserEmail).toHaveBeenCalledWith(
      '123456',
      'jan@example.com'
    );
  });

  it('sends the manually typed email when navigation did not provide one', () => {
    authService.confirmUserEmail.mockReturnValue(of({}));
    facade.formGroup.setValue({ code: '654321', email: 'typed@example.com' });

    facade.confirmEmail();

    expect(authService.confirmUserEmail).toHaveBeenCalledWith(
      '654321',
      'typed@example.com'
    );
  });

  it('marks the email as known once navigation provides it', () => {
    expect(facade.emailKnown()).toBe(false);

    facade.setEmail('jan@example.com');

    expect(facade.emailKnown()).toBe(true);
    expect(facade.formGroup.get('email')?.value).toBe('jan@example.com');
  });

  it('does not call the service and shows a validation snackbar when the email is missing', () => {
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();

    expect(authService.confirmUserEmail).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'validation.common.not_all_fields_filled'
    );
  });

  it('on success: reloads the current user', () => {
    authService.confirmUserEmail.mockReturnValue(of({}));
    const dispatchSpy = jest.spyOn(store, 'dispatch');
    facade.setEmail('jan@example.com');
    facade.formGroup.get('code')?.setValue('123456');

    facade.confirmEmail();

    expect(dispatchSpy).toHaveBeenCalledWith(loadUserCurrent());
  });

  it('resends using the email from state and runs the cooldown', () => {
    jest.useFakeTimers();
    authService.resendEmailConfirmation.mockReturnValue(of(true));
    facade.setEmail('jan@example.com');

    facade.resendCode();

    expect(authService.resendEmailConfirmation).toHaveBeenCalledWith(
      'jan@example.com'
    );
    expect(facade.isResendDisabled()).toBe(true);

    jest.advanceTimersByTime(30000);

    expect(facade.isResendDisabled()).toBe(false);
    jest.useRealTimers();
  });

  it('does not resend when no valid email is available', () => {
    facade.resendCode();

    expect(authService.resendEmailConfirmation).not.toHaveBeenCalled();
    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith(
      'validation.common.not_all_fields_filled'
    );
    expect(facade.isResendDisabled()).toBe(false);
  });

  it('keeps the email field visible when the query param is not a valid email', () => {
    facade.setEmail('not-an-email');

    expect(facade.emailKnown()).toBe(false);
    expect(facade.formGroup.get('email')?.value).toBe('not-an-email');
  });

  it('on resend success: shows the success snackbar', () => {
    authService.resendEmailConfirmation.mockReturnValue(of(true));
    facade.setEmail('jan@example.com');

    facade.resendCode();

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'auth.confirm_email.resend_success'
    );
  });

  it('on resend error: surfaces the error and resets the cooldown', () => {
    authService.resendEmailConfirmation.mockReturnValue(
      throwError(() => new Error('down'))
    );
    facade.setEmail('jan@example.com');

    facade.resendCode();

    expect(snackbar.showApiError).toHaveBeenCalledWith(
      expect.any(Error),
      'auth.confirm_email.resend_error'
    );
    expect(facade.isResendDisabled()).toBe(false);
  });
});
