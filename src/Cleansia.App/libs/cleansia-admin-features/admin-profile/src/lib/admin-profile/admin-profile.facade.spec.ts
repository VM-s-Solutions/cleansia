import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminAuthService,
  AdminClient,
  ChangeOwnPasswordCommand,
  ChangeOwnPasswordResponse,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminProfileFacade } from './admin-profile.facade';

describe('AdminProfileFacade', () => {
  let facade: AdminProfileFacade;
  let changePasswordMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };
  let authService: {
    passwordChangeRequired: jest.Mock;
    clearPasswordChangeRequired: jest.Mock;
    logout: jest.Mock;
  };
  let router: { navigate: jest.Mock };

  beforeEach(() => {
    changePasswordMock = jest.fn();
    authService = {
      passwordChangeRequired: jest.fn(() => false),
      clearPasswordChangeRequired: jest.fn(),
      logout: jest.fn(() => of(true)),
    };
    router = { navigate: jest.fn() };
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        AdminProfileFacade,
        {
          provide: AdminClient,
          useValue: { adminAuthClient: { changePassword: changePasswordMock } },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: AdminAuthService, useValue: authService },
        { provide: Router, useValue: router },
      ],
    });

    facade = TestBed.inject(AdminProfileFacade);
  });

  it('sends current and new password in the command', () => {
    changePasswordMock.mockReturnValue(
      of(ChangeOwnPasswordResponse.fromJS({ id: 'usr-1' }))
    );

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'NewPass456',
    });

    // Every member of a generated command is optional, so a dropped assignment
    // type-checks — pin the serialized body instead (ADR-0031).
    const command = changePasswordMock.mock.calls[0][0];
    expect(command).toBeInstanceOf(ChangeOwnPasswordCommand);
    expect(command.toJSON()).toEqual({
      currentPassword: 'OldPass123',
      newPassword: 'NewPass456',
      // Server-enriched from the refresh cookie; the blank is what satisfies the required member.
      currentRefreshToken: '',
    });
  });

  it('shows success, bumps the changed counter and clears saving on success', () => {
    changePasswordMock.mockReturnValue(
      of(ChangeOwnPasswordResponse.fromJS({ id: 'usr-1' }))
    );

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'NewPass456',
    });

    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.admin_profile.messages.change_password_success'
    );
    expect(facade.passwordChanged()).toBe(1);
    expect(facade.saving()).toBe(false);
  });

  it('leaves the auth.current_password_invalid refusal to the interceptor toast', () => {
    changePasswordMock.mockReturnValue(
      throwError(() => ({
        result: { detail: 'auth.current_password_invalid' },
      }))
    );

    facade.changePassword({
      currentPassword: 'WrongPass1',
      newPassword: 'NewPass456',
    });

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.passwordChanged()).toBe(0);
    expect(facade.saving()).toBe(false);
  });

  it('leaves the auth.invalid_password_format refusal to the interceptor toast', () => {
    changePasswordMock.mockReturnValue(
      throwError(() => ({
        result: { detail: 'auth.invalid_password_format' },
      }))
    );

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'short',
    });

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves an unknown refusal to the interceptor toast', () => {
    changePasswordMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'NewPass456',
    });

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('ignores a submit while a change is already in flight', () => {
    changePasswordMock.mockReturnValue(of(undefined).pipe());
    facade.saving.set(true);

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'NewPass456',
    });

    expect(changePasswordMock).not.toHaveBeenCalled();
  });

  it('changes a password at will without leaving the profile', () => {
    changePasswordMock.mockReturnValue(of(ChangeOwnPasswordResponse.fromJS({ id: 'usr-1' })));

    facade.changePassword({ currentPassword: 'OldPass123456', newPassword: 'NewPass456789' });

    expect(facade.passwordChangeRequired).toBe(false);
    expect(authService.clearPasswordChangeRequired).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });

  /**
   * An administrator whose password someone else chose is held on this page by the admin guard
   * until the change succeeds; the success releases the hold and the app continues.
   */
  describe('while the password change is required', () => {
    let held: AdminProfileFacade;

    beforeEach(() => {
      authService.passwordChangeRequired.mockReturnValue(true);
      held = TestBed.runInInjectionContext(() => new AdminProfileFacade());
    });

    it('releases the hold and continues on the home route once the change succeeds', () => {
      changePasswordMock.mockReturnValue(of(ChangeOwnPasswordResponse.fromJS({ id: 'usr-1' })));

      held.changePassword({ currentPassword: 'Typed4Me2026', newPassword: 'MyOwnSecret2026' });

      expect(authService.clearPasswordChangeRequired).toHaveBeenCalledTimes(1);
      expect(router.navigate).toHaveBeenCalledWith([`/${CleansiaAdminRoute.HOME}`]);
    });

    it('keeps the page in its held form after the success clears the flag', () => {
      changePasswordMock.mockReturnValue(of(ChangeOwnPasswordResponse.fromJS({ id: 'usr-1' })));
      held.changePassword({ currentPassword: 'Typed4Me2026', newPassword: 'MyOwnSecret2026' });
      authService.passwordChangeRequired.mockReturnValue(false);

      expect(held.passwordChangeRequired).toBe(true);
    });

    it('stays held when the server refuses the change, as it does the same password again', () => {
      changePasswordMock.mockReturnValue(
        throwError(() => ({ result: { detail: 'auth.same_reset_password' } }))
      );

      held.changePassword({ currentPassword: 'Typed4Me2026', newPassword: 'Typed4Me2026' });

      expect(authService.clearPasswordChangeRequired).not.toHaveBeenCalled();
      expect(router.navigate).not.toHaveBeenCalled();
      expect(held.saving()).toBe(false);
    });

    it('signs out from the page, since the held shell shows no sidebar', () => {
      held.signOut();

      expect(authService.logout).toHaveBeenCalledTimes(1);
    });
  });
});
