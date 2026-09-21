import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  PagedDataOfServiceListItem,
  ServiceListItem,
} from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { EMPTY, of, throwError } from 'rxjs';
import { ServiceManagementFacade } from './service-management.facade';

describe('ServiceManagementFacade', () => {
  let facade: ServiceManagementFacade;
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

  const page = PagedDataOfServiceListItem.fromJS({
    data: [ServiceListItem.fromJS({ id: 'svc-1', name: 'Deep clean' })],
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
        ServiceManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminServiceClient: {
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

    facade = TestBed.inject(ServiceManagementFacade);
  });

  it('loads services and stores data + total', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.loadServices();

    expect(getPagedMock).toHaveBeenCalledTimes(1);
    expect(facade.services().length).toBe(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  it('passes searchTerm and isActive into the new positional getPaged shape', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.applyFilter({ searchTerm: 'deep', isActive: false });

    const args = getPagedMock.mock.calls[0];
    expect(args[0]).toBe('deep');
    expect(args[1]).toBe(false);
    expect(args[3]).toBe(0);
  });

  it('passes isActive undefined when no status filter is set', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.applyFilter({ searchTerm: 'deep' });

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

  it('clears loading on load failure', () => {
    getPagedMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadServices();

    expect(facade.loading()).toBe(false);
    expect(facade.services().length).toBe(0);
  });

  it('deactivates a service, shows success and reloads the list', () => {
    deactivateMock.mockReturnValue(of({ id: 'svc-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.deactivateService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(deactivateMock).toHaveBeenCalledWith('svc-1');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.service_management.messages.deactivate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('activates a service, shows success and reloads the list', () => {
    activateMock.mockReturnValue(of({ id: 'svc-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.activateService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(activateMock).toHaveBeenCalledWith('svc-1');
    expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
      'pages.service_management.messages.activate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('does not call deactivate or activate for a row without id', () => {
    facade.deactivateService(ServiceListItem.fromJS({}));
    facade.activateService(ServiceListItem.fromJS({}));

    expect(deactivateMock).not.toHaveBeenCalled();
    expect(activateMock).not.toHaveBeenCalled();
  });

  it('leaves the service.not_found refusal to the interceptor toast on deactivate failure', () => {
    deactivateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'service.not_found' } }))
    );

    facade.deactivateService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves an unknown refusal to the interceptor toast on activate failure', () => {
    activateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.activateService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  it('leaves the service.in_use refusal to the interceptor toast on delete failure', () => {
    deleteMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'service.in_use' } }))
    );

    facade.deleteService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
  });

  describe('the price columns name their currency', () => {
    it('formats the list price in the platform default currency, not in crowns', () => {
      getPagedMock.mockReturnValue(of(page));

      facade.loadServices();

      expect(getOverviewMock).toHaveBeenCalledTimes(1);
      expect(facade.defaultCurrencyCode()).toBe('EUR');
      expect(facade.formatCurrency(45.1)).toContain('€');
      expect(facade.formatCurrency(45.1)).not.toContain('CZK');
    });

    it('reads the currency before the page and once across reloads', () => {
      getPagedMock.mockReturnValue(of(page));

      facade.loadServices();
      facade.loadServices();

      expect(getOverviewMock).toHaveBeenCalledTimes(1);
      expect(getPagedMock).toHaveBeenCalledTimes(2);
      expect(getOverviewMock.mock.invocationCallOrder[0]).toBeLessThan(getPagedMock.mock.invocationCallOrder[0]);
    });

    it('prints a bare number rather than a currency it cannot name', () => {
      getOverviewMock.mockReturnValueOnce(of([]));
      getPagedMock.mockReturnValue(of(page));

      facade.loadServices();

      expect(facade.defaultCurrencyCode()).toBeNull();
      expect(facade.formatCurrency(45.1)).toBe('45,10');
    });
  });
});
