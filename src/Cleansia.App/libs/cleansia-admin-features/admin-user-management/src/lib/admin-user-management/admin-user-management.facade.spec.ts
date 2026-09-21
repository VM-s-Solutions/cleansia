import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, AdminUserListItem, PagedDataOfAdminUserListItem } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of } from 'rxjs';
import { AdminUserManagementFacade } from './admin-user-management.facade';

describe('AdminUserManagementFacade', () => {
  let facade: AdminUserManagementFacade;
  let getPagedMock: jest.Mock;
  let activateMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };

  const active = AdminUserListItem.fromJS({ id: 'u-1', email: 'a@cleansia.cz', isActive: true });
  const inactive = AdminUserListItem.fromJS({ id: 'u-2', email: 'b@cleansia.cz', isActive: false });
  const page = PagedDataOfAdminUserListItem.fromJS({ data: [active, inactive], total: 2 });

  beforeEach(() => {
    TestBed.resetTestingModule();
    getPagedMock = jest.fn().mockReturnValue(of(page));
    activateMock = jest.fn().mockReturnValue(of({ userId: 'u-2' }));
    deactivateMock = jest.fn().mockReturnValue(of({ userId: 'u-1' }));
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = { showSuccessTranslated: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        AdminUserManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminUserClient: { getPaged: getPagedMock, activate: activateMock, deactivate: deactivateMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        {
          provide: TranslateService,
          useValue: { instant: (k: string) => k, currentLang: 'cs', onLangChange: EMPTY },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(AdminUserManagementFacade);
  });

  it('loads the users and stores the rows and total', () => {
    facade.loadUsers();

    expect(facade.users().length).toBe(2);
    expect(facade.totalRecords()).toBe(2);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  describe('toggleUserStatus', () => {
    it('asks before deactivating an active user, then deactivates, toasts and re-reads', () => {
      facade.toggleUserStatus(active);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.admin_user_management.deactivate_confirm',
        'pages.admin_user_management.deactivate_user'
      );
      expect(deactivateMock).toHaveBeenCalledWith('u-1');
      expect(activateMock).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.admin_user_management.messages.deactivate_success');
      expect(getPagedMock).toHaveBeenCalledTimes(1);
    });

    it('asks before activating an inactive user, then activates, toasts and re-reads', () => {
      facade.toggleUserStatus(inactive);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.admin_user_management.activate_confirm',
        'pages.admin_user_management.activate_user'
      );
      expect(activateMock).toHaveBeenCalledWith('u-2');
      expect(deactivateMock).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.admin_user_management.messages.activate_success');
      expect(getPagedMock).toHaveBeenCalledTimes(1);
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.toggleUserStatus(active);
      facade.toggleUserStatus(inactive);

      expect(confirmMock).toHaveBeenCalledTimes(2);
      expect(deactivateMock).not.toHaveBeenCalled();
      expect(activateMock).not.toHaveBeenCalled();
      expect(getPagedMock).not.toHaveBeenCalled();
    });
  });
});
