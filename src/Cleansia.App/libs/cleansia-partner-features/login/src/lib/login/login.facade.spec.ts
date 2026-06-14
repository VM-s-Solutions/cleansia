import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { PartnerAuthService } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { LoginFacade } from './login.facade';

describe('LoginFacade (partner)', () => {
  let facade: LoginFacade;
  let authService: { login: jest.Mock; setSession: jest.Mock };
  let snackbar: { showError: jest.Mock; showApiError: jest.Mock };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    authService = { login: jest.fn(), setSession: jest.fn() };
    snackbar = { showError: jest.fn(), showApiError: jest.fn() };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        LoginFacade,
        provideMockStore(),
        { provide: Router, useValue: router },
        { provide: PartnerAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(LoginFacade);
  });

  function fillValid(): void {
    facade.formGroup.setValue({
      email: 'cleaner@example.com',
      password: 'Heslo1234',
      rememberMe: false,
    });
  }

  it('shows a validation snackbar and does not call login when the form is invalid', () => {
    facade.login();

    expect(snackbar.showError).toHaveBeenCalled();
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('on a confirmed login: sets session and navigates', () => {
    authService.login.mockReturnValue(of({ isEmailConfirmed: true }));
    fillValid();

    facade.login();

    expect(authService.setSession).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalled();
  });

  it('surfaces a failed login via showApiError (IA-9 — was silently swallowed)', () => {
    authService.login.mockReturnValue(throwError(() => ({ message: 'invalid' })));
    fillValid();

    facade.login();

    expect(snackbar.showApiError).toHaveBeenCalledWith(expect.anything(), 'auth.login.error');
    expect(authService.setSession).not.toHaveBeenCalled();
  });
});
