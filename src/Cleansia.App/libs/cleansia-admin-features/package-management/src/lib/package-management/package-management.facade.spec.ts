import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  PackageListItem,
  PagedDataOfPackageListItem,
} from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { PackageManagementFacade } from './package-management.facade';

describe('PackageManagementFacade', () => {
  let facade: PackageManagementFacade;
  let getPagedMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let activateMock: jest.Mock;
  let deleteMock: jest.Mock;
  let getOverviewMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const page = PagedDataOfPackageListItem.fromJS({
    data: [PackageListItem.fromJS({ id: 'pkg-1', name: 'Move-out bundle' })],
    total: 1,
  });

  beforeEach(() => {
    getPagedMock = jest.fn();
    deactivateMock = jest.fn();
    activateMock = jest.fn();
    deleteMock = jest.fn();
    getOverviewMock = jest.fn().mockReturnValue(
      of([{ id: 'cur-eur', code: 'EUR', isDefault: true }, { id: 'cur-czk', code: 'CZK', isDefault: false }])
    );
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

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
            adminCurrencyClient: {
              getOverview: getOverviewMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: jest.fn(() => of(true)) } },
        {
          provide: TranslateService,
          useValue: {
            instant: (k: string) => k,
            currentLang: 'cs',
            onLangChange: EMPTY,
          },
        },
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

    facade.onPageChange({ first: 40, rows: 20, page: 2, totalRecords: 100 });
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
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.package_management.messages.deactivate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('activates a package, shows success and reloads the list', () => {
    activateMock.mockReturnValue(of({ id: 'pkg-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.activatePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(activateMock).toHaveBeenCalledWith('pkg-1');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
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

  it('leaves the package.not_found refusal to the interceptor toast on deactivate failure', () => {
    deactivateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'package.not_found' } }))
    );

    facade.deactivatePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves the package.in_use refusal to the interceptor toast on delete failure', () => {
    deleteMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'package.in_use' } }))
    );

    facade.deletePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves an unknown refusal to the interceptor toast', () => {
    activateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.activatePackage(PackageListItem.fromJS({ id: 'pkg-1' }));

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  describe('the price columns name their currency', () => {
    it('formats the list price in the platform default currency, not in crowns', () => {
      getPagedMock.mockReturnValue(of(page));

      facade.loadPackages();

      expect(getOverviewMock).toHaveBeenCalledTimes(1);
      expect(facade.defaultCurrencyCode()).toBe('EUR');
      expect(facade.formatCurrency(45.1)).toContain('€');
      expect(facade.formatCurrency(45.1)).not.toContain('CZK');
    });

    it('reads the currency before the page and once across reloads', () => {
      getPagedMock.mockReturnValue(of(page));

      facade.loadPackages();
      facade.loadPackages();

      expect(getOverviewMock).toHaveBeenCalledTimes(1);
      expect(getPagedMock).toHaveBeenCalledTimes(2);
      expect(getOverviewMock.mock.invocationCallOrder[0]).toBeLessThan(getPagedMock.mock.invocationCallOrder[0]);
    });

    it('prints a bare number rather than a currency it cannot name', () => {
      getOverviewMock.mockReturnValueOnce(of([]));
      getPagedMock.mockReturnValue(of(page));

      facade.loadPackages();

      expect(facade.defaultCurrencyCode()).toBeNull();
      expect(facade.formatCurrency(45.1)).toBe('45,10');
    });
  });
});
