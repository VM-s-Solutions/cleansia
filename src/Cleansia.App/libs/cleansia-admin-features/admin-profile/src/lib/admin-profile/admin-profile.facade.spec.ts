import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  ChangeOwnPasswordCommand,
  ChangeOwnPasswordResponse,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
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

  beforeEach(() => {
    changePasswordMock = jest.fn();
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
});
