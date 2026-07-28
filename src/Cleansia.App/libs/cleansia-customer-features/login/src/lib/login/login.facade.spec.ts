import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { GuestOrderService } from '@cleansia-customer/orders';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { LoginFacade } from './login.facade';

describe('LoginFacade (customer)', () => {
  let facade: LoginFacade;
  let authService: {
    login: jest.Mock;
    authenticateWithGoogle: jest.Mock;
    authenticateWithApple: jest.Mock;
    setSession: jest.Mock;
  };
  let snackbar: {
    showError: jest.Mock;
    showApiError: jest.Mock;
    showErrorTranslated: jest.Mock;
    showSuccessTranslated: jest.Mock;
  };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    authService = {
      login: jest.fn(),
      authenticateWithGoogle: jest.fn(),
      authenticateWithApple: jest.fn(),
      setSession: jest.fn(),
    };
    snackbar = {
      showError: jest.fn(),
      showApiError: jest.fn(),
      showErrorTranslated: jest.fn(),
      showSuccessTranslated: jest.fn(),
    };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        LoginFacade,
        provideMockStore(),
        { provide: Router, useValue: router },
        { provide: CustomerAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
        { provide: GuestOrderService, useValue: { clear: jest.fn() } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(LoginFacade);
  });

  function fillValid(): void {
    facade.formGroup.setValue({
      email: 'jan@example.com',
      password: 'Heslo1234',
      rememberMe: true,
    });
  }

  it('shows a validation snackbar and does not call login when the form is invalid', () => {
    facade.login();

    expect(snackbar.showError).toHaveBeenCalled();
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('on a confirmed login: sets session and navigates to orders', () => {
    authService.login.mockReturnValue(of({ isEmailConfirmed: true }));
    fillValid();

    facade.login();

    expect(authService.setSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
    expect(snackbar.showApiError).not.toHaveBeenCalled();
  });

  it('surfaces a login error via showApiError (no swallow)', () => {
    authService.login.mockReturnValue(throwError(() => ({ message: 'bad creds' })));
    fillValid();

    facade.login();

    expect(snackbar.showApiError).toHaveBeenCalledWith(expect.anything(), 'auth.login.error');
    expect(authService.setSession).not.toHaveBeenCalled();
  });

  it('appleLogin forwards the RAW nonce untouched and signs the user in', () => {
    authService.authenticateWithApple.mockReturnValue(of({ isEmailConfirmed: true }));

    facade.appleLogin('id-token', 'raw-nonce', 'Jan', 'Novák');

    // The server hashes this value itself — sending the hash instead fails
    // every sign-in with a generic error, so pin the argument order.
    expect(authService.authenticateWithApple).toHaveBeenCalledWith(
      'id-token',
      'raw-nonce',
      'Jan',
      'Novák'
    );
    expect(authService.setSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
  });

  it('surfaces an Apple sign-in error via showApiError (no swallow)', () => {
    // The shape matters: NSwag throws the ProblemDetails BARE, so this is what
    // a real 401 looks like on the way out of the service. Asserting only
    // `expect.anything()` here would stay green even if the error resolved to
    // nothing and the user saw the generic fallback instead of the real reason.
    authService.authenticateWithApple.mockReturnValue(
      throwError(() => ({
        detail: 'auth.google_type_error',
        errors: { IdentityToken: 'auth.google_type_error' },
      }))
    );

    facade.appleLogin('id-token', 'raw-nonce');

    expect(snackbar.showApiError).toHaveBeenCalledWith(expect.anything(), 'auth.login.error');
    const [reported] = snackbar.showApiError.mock.calls[0];
    expect(extractApiErrorCode(reported)).toBe('auth.google_type_error');
    expect(authService.setSession).not.toHaveBeenCalled();
  });

  it('reports a popup failure that never reached the API', () => {
    facade.appleSignInFailed();

    expect(snackbar.showErrorTranslated).toHaveBeenCalledWith('api.common.error_occurred');
  });
});
