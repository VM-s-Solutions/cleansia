import { TestBed } from '@angular/core/testing';
import { AdminClient, ChangeOwnPasswordResponse } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminProfileFacade } from './admin-profile.facade';

describe('AdminProfileFacade', () => {
  let facade: AdminProfileFacade;
  let changePasswordMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    changePasswordMock = jest.fn();
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        AdminProfileFacade,
        {
          provide: AdminClient,
          useValue: { adminAuthClient: { changePassword: changePasswordMock } },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
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

    const command = changePasswordMock.mock.calls[0][0];
    expect(command.currentPassword).toBe('OldPass123');
    expect(command.newPassword).toBe('NewPass456');
  });

  it('shows success, bumps the changed counter and clears saving on success', () => {
    changePasswordMock.mockReturnValue(
      of(ChangeOwnPasswordResponse.fromJS({ id: 'usr-1' }))
    );

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'NewPass456',
    });

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.admin_profile.messages.change_password_success'
    );
    expect(facade.passwordChanged()).toBe(1);
    expect(facade.saving()).toBe(false);
  });

  it('maps auth.current_password_invalid to its translation key', () => {
    changePasswordMock.mockReturnValue(
      throwError(() => ({
        result: { detail: 'auth.current_password_invalid' },
      }))
    );

    facade.changePassword({
      currentPassword: 'WrongPass1',
      newPassword: 'NewPass456',
    });

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.auth.current_password_invalid'
    );
    expect(facade.passwordChanged()).toBe(0);
    expect(facade.saving()).toBe(false);
  });

  it('maps auth.invalid_password_format to its translation key', () => {
    changePasswordMock.mockReturnValue(
      throwError(() => ({
        result: { detail: 'auth.invalid_password_format' },
      }))
    );

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'short',
    });

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.auth.invalid_password_format'
    );
  });

  it('falls back to the generic change-password error for unknown codes', () => {
    changePasswordMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.changePassword({
      currentPassword: 'OldPass123',
      newPassword: 'NewPass456',
    });

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.auth.change_password_failed'
    );
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
});
