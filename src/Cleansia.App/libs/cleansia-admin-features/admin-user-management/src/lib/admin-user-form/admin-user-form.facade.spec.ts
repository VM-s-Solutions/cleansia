import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, LanguageListItem } from '@cleansia/admin-services';
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
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const birthDate = new Date(1990, 4, 15);

  const fullData: AdminUserFormData = {
    email: 'admin@cleansia.cz',
    password: 'Heslo1234',
    firstName: 'Jana',
    lastName: 'Nováková',
    phoneNumber: '+420777111222',
    birthDate,
    preferredLanguageCode: 'cs',
  };

  beforeEach(() => {
    createMock = jest.fn();
    updateMock = jest.fn();
    detailsMock = jest.fn();
    getOverviewMock = jest.fn();
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

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
            },
            adminLanguageClient: { getOverview: getOverviewMock },
          },
        },
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
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
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

  it('maps active languages to select options', () => {
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

  it('maps admin_user.email_exists to its translation key on create failure', () => {
    createMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'admin_user.email_exists' } }))
    );

    facade.createUser(fullData);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.admin_user.email_exists'
    );
    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('maps language.not_supported to its translation key on update failure', () => {
    updateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'language.not_supported' } }))
    );

    facade.updateUser('usr-1', fullData);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.language.not_supported'
    );
  });

  it('falls back to the generic error for unknown codes', () => {
    createMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.createUser(fullData);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.common.error_occurred'
    );
  });
});
