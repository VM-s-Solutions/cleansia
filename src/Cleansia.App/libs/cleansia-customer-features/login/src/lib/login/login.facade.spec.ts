import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { CustomerAuthService } from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
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
    setSession: jest.Mock;
  };
  let snackbar: { showError: jest.Mock; showApiError: jest.Mock; showSuccessTranslated: jest.Mock };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    authService = {
      login: jest.fn(),
      authenticateWithGoogle: jest.fn(),
      setSession: jest.fn(),
    };
    snackbar = { showError: jest.fn(), showApiError: jest.fn(), showSuccessTranslated: jest.fn() };
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
});
