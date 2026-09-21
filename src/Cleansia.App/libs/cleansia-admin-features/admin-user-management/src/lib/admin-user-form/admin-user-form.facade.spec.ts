import { FormControl } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminAuthService,
  AdminClient,
  AdminRole,
  AdminUserDetailDto,
  CreateAdminUserCommand,
  LanguageListItem,
  SetAdminRoleCommand,
  SetAdminRoleResponse,
  UpdateAdminUserCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { AdminUserFormData, AdminUserFormFacade } from './admin-user-form.facade';

describe('AdminUserFormFacade', () => {
  let facade: AdminUserFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let detailsMock: jest.Mock;
  let getOverviewMock: jest.Mock;
  let roleMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };
  let navigate: jest.Mock;
  let currentUserId: string | null;

  const birthDate = new Date(1990, 4, 15);

  const fullData: AdminUserFormData = {
    email: 'admin@cleansia.cz',
    password: 'Heslo1234',
    firstName: 'Jana',
    lastName: 'Nováková',
    phoneNumber: '+420777111222',
    birthDate,
    preferredLanguageCode: 'cs',
    role: AdminRole.Manager,
  };

  beforeEach(() => {
    createMock = jest.fn();
    updateMock = jest.fn();
    detailsMock = jest.fn();
    getOverviewMock = jest.fn();
    roleMock = jest.fn();
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };
    navigate = jest.fn();
    currentUserId = 'usr-me';

    TestBed.configureTestingModule({
      providers: [
        AdminUserFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminUserClient: {
              create: createMock,
              update: updateMock,
              details: detailsMock,
              role: roleMock,
            },
            adminLanguageClient: { getOverview: getOverviewMock },
          },
        },
        { provide: AdminAuthService, useValue: { getUserId: () => currentUserId } },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(AdminUserFormFacade);
  });

  it('sends birthDate and preferredLanguageCode in the create command', () => {
    createMock.mockReturnValue(of({ id: 'usr-1' }));

    facade.createUser(fullData);

    const command = createMock.mock.calls[0][0];
    expect(command.birthDate).toEqual(birthDate);
    expect(command.preferredLanguageCode).toBe('cs');
    expect(command.email).toBe('admin@cleansia.cz');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.admin_user_form.messages.create_success'
    );
    expect(navigate).toHaveBeenCalled();
  });

  it('sends birthDate and preferredLanguageCode in the update command', () => {
    updateMock.mockReturnValue(of({ id: 'usr-1' }));

    facade.updateUser('usr-1', fullData);

    const command = updateMock.mock.calls[0][1];
    expect(command.userId).toBe('usr-1');
    expect(command.birthDate).toEqual(birthDate);
    expect(command.preferredLanguageCode).toBe('cs');
  });

  it('sends undefined for unset birthDate and language', () => {
    updateMock.mockReturnValue(of({ id: 'usr-1' }));

    facade.updateUser('usr-1', {
      email: 'admin@cleansia.cz',
      firstName: 'Jana',
      lastName: 'Nováková',
    });

    const command = updateMock.mock.calls[0][1];
    expect(command.birthDate).toBeUndefined();
    expect(command.preferredLanguageCode).toBeUndefined();
  });

  it('leaves the active languages to select options refusal to the interceptor toast', () => {
    getOverviewMock.mockReturnValue(
      of([
        LanguageListItem.fromJS({ id: 'l1', code: 'cs', name: 'Čeština' }),
        LanguageListItem.fromJS({ id: 'l2', code: 'en', name: 'English' }),
        LanguageListItem.fromJS({ id: 'l3' }),
      ])
    );

    facade.loadLanguages();

    expect(facade.languageOptions()).toEqual([
      { label: 'Čeština', value: 'cs' },
      { label: 'English', value: 'en' },
    ]);
  });

  it('leaves the admin_user.email_exists refusal to the interceptor toast on create failure', () => {
    createMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'admin_user.email_exists' } }))
    );

    facade.createUser(fullData);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('leaves the language.not_supported refusal to the interceptor toast on update failure', () => {
    updateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'language.not_supported' } }))
    );

    facade.updateUser('usr-1', fullData);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves an unknown refusal to the interceptor toast', () => {
    createMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.createUser(fullData);

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  // Every member of a generated command is optional, so a dropped assignment type-checks.
  // These pin the serialized body instead (ADR-0031).
  describe('command bodies on the wire', () => {
    it('serializes a create with the credentials and the profile fields', () => {
      createMock.mockReturnValue(of({ id: 'usr-1' }));

      facade.createUser(fullData);

      const command: CreateAdminUserCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateAdminUserCommand);
      expect(command.toJSON()).toEqual({
        email: 'admin@cleansia.cz',
        password: 'Heslo1234',
        firstName: 'Jana',
        lastName: 'Nováková',
        phoneNumber: '+420777111222',
        birthDate: '1990-05-15',
        preferredLanguageCode: 'cs',
        role: AdminRole.Manager,
      });
    });

    it('serializes an update with the user id and no password', () => {
      updateMock.mockReturnValue(of({ id: 'usr-1' }));

      facade.updateUser('usr-1', fullData);

      const command: UpdateAdminUserCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateAdminUserCommand);
      expect(command.toJSON()).toEqual({
        userId: 'usr-1',
        firstName: 'Jana',
        lastName: 'Nováková',
        phoneNumber: '+420777111222',
        birthDate: '1990-05-15',
        preferredLanguageCode: 'cs',
      });
    });

    it('sends the least-privilege role when the form names none', () => {
      createMock.mockReturnValue(of({ id: 'usr-1' }));

      facade.createUser({ ...fullData, role: undefined });

      expect(createMock.mock.calls[0][0].toJSON().role).toBe(AdminRole.Support);
    });

    it('sends undefined rather than an empty string for a blank phone and language', () => {
      createMock.mockReturnValue(of({ id: 'usr-1' }));

      facade.createUser({ ...fullData, phoneNumber: '', preferredLanguageCode: '' });

      const body = createMock.mock.calls[0][0].toJSON();
      expect(body.phoneNumber).toBeUndefined();
      expect(body.preferredLanguageCode).toBeUndefined();
    });
  });

  /**
   * The picker is a control the facade drives: it follows the loaded detail, sends one command per
   * change, and steps back to the last server-confirmed role when the server refuses. The refusal
   * sentence itself reaches the user through the shared interceptor, never a second toast here.
   */
  describe('role picker', () => {
    let control: FormControl<AdminRole | null>;

    function loadTarget(id: string, role: AdminRole): void {
      detailsMock.mockReturnValue(of(AdminUserDetailDto.fromJS({ id, adminRole: role })));
      facade.loadUser(id);
    }

    beforeEach(() => {
      control = new FormControl<AdminRole | null>(null);
      facade.connectRoleControl(control);
    });

    it('seeds the control from the loaded detail without firing a command', () => {
      loadTarget('usr-1', AdminRole.Accountant);

      expect(control.value).toBe(AdminRole.Accountant);
      expect(facade.role()).toBe(AdminRole.Accountant);
      expect(roleMock).not.toHaveBeenCalled();
    });

    it('sends one SetAdminRole command for the target when the picker changes', () => {
      loadTarget('usr-1', AdminRole.Support);
      roleMock.mockReturnValue(
        of(SetAdminRoleResponse.fromJS({ id: 'usr-1', role: AdminRole.Manager }))
      );

      control.setValue(AdminRole.Manager);

      expect(roleMock).toHaveBeenCalledTimes(1);
      const [userId, command] = roleMock.mock.calls[0] as [string, SetAdminRoleCommand];
      expect(userId).toBe('usr-1');
      expect(command).toBeInstanceOf(SetAdminRoleCommand);
      expect(command.toJSON()).toEqual({ userId: 'usr-1', role: AdminRole.Manager });
      expect(facade.role()).toBe(AdminRole.Manager);
      expect(facade.roleSaving()).toBe(false);
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.admin_user_form.messages.role_success');
    });

    it('steps the picker back to the confirmed role when the server refuses, with no toast of its own', () => {
      loadTarget('usr-1', AdminRole.Administrator);
      roleMock.mockReturnValue(
        throwError(() => ({ result: { detail: 'admin_user.cannot_demote_last_administrator' } }))
      );

      control.setValue(AdminRole.Support);

      expect(control.value).toBe(AdminRole.Administrator);
      expect(facade.role()).toBe(AdminRole.Administrator);
      expect(facade.roleSaving()).toBe(false);
      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    });

    it('ignores a change that names the role already held', () => {
      loadTarget('usr-1', AdminRole.Support);

      control.setValue(AdminRole.Support);

      expect(roleMock).not.toHaveBeenCalled();
    });

    it('refuses self before any command: the control is disabled and the target is flagged', () => {
      loadTarget('usr-me', AdminRole.Administrator);

      expect(facade.isSelf()).toBe(true);
      expect(control.disabled).toBe(true);

      control.enable();
      control.setValue(AdminRole.Support);
      expect(roleMock).not.toHaveBeenCalled();
    });

    it('leaves the control enabled for another administrator', () => {
      loadTarget('usr-1', AdminRole.Administrator);

      expect(facade.isSelf()).toBe(false);
      expect(control.disabled).toBe(false);
    });
  });
});
