import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
import { loadUserCurrent } from '@cleansia/admin-stores';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { MockStore, provideMockStore } from '@ngrx/store/testing';
import { TranslateService } from '@ngx-translate/core';
import { NEVER, of, throwError } from 'rxjs';
import { AdminLoginFacade } from './admin-login.facade';

describe('AdminLoginFacade', () => {
  let facade: AdminLoginFacade;
  let store: MockStore;
  let authService: { login: jest.Mock; setSession: jest.Mock };
  let snackbar: { showError: jest.Mock; showApiError: jest.Mock };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    authService = { login: jest.fn(), setSession: jest.fn() };
    snackbar = { showError: jest.fn(), showApiError: jest.fn() };
    router = { navigate: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        AdminLoginFacade,
        provideMockStore({ initialState: { loading: { loading: true } } }),
        { provide: Router, useValue: router },
        { provide: AdminAuthService, useValue: authService },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(AdminLoginFacade);
    store = TestBed.inject(MockStore);
  });

  function fillValid(): void {
    facade.formGroup.setValue({
      email: 'admin@example.com',
      password: 'Heslo1234',
      rememberMe: false,
    });
  }

  it('starts not loading even while the global HTTP loading flag is on', () => {
    expect(facade.loading()).toBe(false);
  });

  it('stays interactive while an unrelated boot request hangs', () => {
    store.setState({ loading: { loading: true } });

    expect(facade.loading()).toBe(false);
  });

  it('shows a validation snackbar and does not call the API when the form is invalid', () => {
    facade.login();

    expect(snackbar.showError).toHaveBeenCalled();
    expect(authService.login).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
  });

  it('is loading only while its own login request is in flight', () => {
    authService.login.mockReturnValue(NEVER);
    fillValid();

    facade.login();

    expect(facade.loading()).toBe(true);
  });

  it('logs in a confirmed admin: session, current user load, navigation, loading cleared', () => {
    authService.login.mockReturnValue(
      of({ isEmailConfirmed: true, hasAdminAccess: true })
    );
    const dispatchSpy = jest.spyOn(store, 'dispatch');
    fillValid();

    facade.login();

    expect(authService.setSession).toHaveBeenCalled();
    expect(dispatchSpy).toHaveBeenCalledWith(loadUserCurrent());
    expect(router.navigate).toHaveBeenCalledWith([
      CleansiaAdminRoute.EMPLOYEE_MANAGEMENT,
    ]);
    expect(facade.loading()).toBe(false);
  });

  it('blocks an unconfirmed email with an error snackbar and clears loading', () => {
    authService.login.mockReturnValue(of({ isEmailConfirmed: false }));
    fillValid();

    facade.login();

    expect(snackbar.showError).toHaveBeenCalledWith(
      'validation.auth.email_not_confirmed'
    );
    expect(authService.setSession).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
  });

  it('routes an account without admin access to /unauthorized', () => {
    authService.login.mockReturnValue(
      of({ isEmailConfirmed: true, hasAdminAccess: false })
    );
    fillValid();

    facade.login();

    expect(router.navigate).toHaveBeenCalledWith(['/unauthorized']);
    expect(authService.setSession).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
  });

  it('surfaces a failed login and re-enables the button', () => {
    authService.login.mockReturnValue(throwError(() => ({ message: 'boom' })));
    fillValid();

    facade.login();

    expect(snackbar.showApiError).toHaveBeenCalledWith(
      expect.anything(),
      'validation.auth.login_failed'
    );
    expect(authService.setSession).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
  });
});
