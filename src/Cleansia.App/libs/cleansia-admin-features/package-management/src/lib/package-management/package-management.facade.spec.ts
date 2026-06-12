import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  PackageListItem,
  PagedDataOfPackageListItem,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { PackageManagementFacade } from './package-management.facade';

describe('PackageManagementFacade', () => {
  let facade: PackageManagementFacade;
  let getPagedMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let activateMock: jest.Mock;
  let deleteMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const page = PagedDataOfPackageListItem.fromJS({
    data: [PackageListItem.fromJS({ id: 'pkg-1', name: 'Move-out bundle' })],
    total: 1,
  });

  beforeEach(() => {
    getPagedMock = jest.fn();
    deactivateMock = jest.fn();
    activateMock = jest.fn();
    deleteMock = jest.fn();
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        PackageManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminPackageClient: {
              getPaged: getPagedMock,
              deactivate: deactivateMock,
              activate: activateMock,
              delete: deleteMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(PackageManagementFacade);
  });

  it('loads packages and stores data + total', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.loadPackages();

    expect(getPagedMock).toHaveBeenCalledTimes(1);
    expect(facade.packages().length).toBe(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  it('passes searchTerm and isActive into the new positional getPaged shape', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.applyFilter({ searchTerm: 'move', isActive: false });

    const args = getPagedMock.mock.calls[0];
    expect(args[0]).toBe('move');
    expect(args[1]).toBe(false);
    expect(args[3]).toBe(0);
  });

  it('passes isActive undefined when no status filter is set', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.applyFilter({ searchTerm: 'move' });

    expect(getPagedMock.mock.calls[0][1]).toBeUndefined();
  });

  it('resets offset when a filter is applied', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.onPageChange(40, 20);
    facade.applyFilter({ isActive: true });

    const lastArgs = getPagedMock.mock.calls.at(-1);
    expect(lastArgs?.[1]).toBe(true);
    expect(lastArgs?.[3]).toBe(0);
  });

  it('deactivates a package, shows success and reloads the list', () => {
    deactivateMock.mockReturnValue(of({ id: 'pkg-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.deactivatePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(deactivateMock).toHaveBeenCalledWith('pkg-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.package_management.messages.deactivate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('activates a package, shows success and reloads the list', () => {
    activateMock.mockReturnValue(of({ id: 'pkg-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.activatePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(activateMock).toHaveBeenCalledWith('pkg-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.package_management.messages.activate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('does not call deactivate or activate for a row without id', () => {
    facade.deactivatePackage(PackageListItem.fromJS({}));
    facade.activatePackage(PackageListItem.fromJS({}));

    expect(deactivateMock).not.toHaveBeenCalled();
    expect(activateMock).not.toHaveBeenCalled();
  });

  it('maps package.not_found to its translation key on deactivate failure', () => {
    deactivateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'package.not_found' } }))
    );

    facade.deactivatePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith('errors.package.not_found');
  });

  it('maps package.in_use to its translation key on delete failure', () => {
    deleteMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'package.in_use' } }))
    );

    facade.deletePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith('errors.package.in_use');
  });

  it('falls back to the generic error for unknown codes', () => {
    activateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.activatePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.common.error_occurred'
    );
  });
});
