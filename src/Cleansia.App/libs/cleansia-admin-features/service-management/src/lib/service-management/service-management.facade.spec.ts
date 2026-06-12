import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  PagedDataOfServiceListItem,
  ServiceListItem,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ServiceManagementFacade } from './service-management.facade';

describe('ServiceManagementFacade', () => {
  let facade: ServiceManagementFacade;
  let getPagedMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let activateMock: jest.Mock;
  let deleteMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const page = PagedDataOfServiceListItem.fromJS({
    data: [ServiceListItem.fromJS({ id: 'svc-1', name: 'Deep clean' })],
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
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
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

    facade.onPageChange(40, 20);
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
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.service_management.messages.deactivate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('activates a service, shows success and reloads the list', () => {
    activateMock.mockReturnValue(of({ id: 'svc-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.activateService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(activateMock).toHaveBeenCalledWith('svc-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
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

  it('maps service.not_found to its translation key on deactivate failure', () => {
    deactivateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'service.not_found' } }))
    );

    facade.deactivateService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith('errors.service.not_found');
  });

  it('falls back to the generic error for unknown codes on activate failure', () => {
    activateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.activateService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.common.error_occurred'
    );
  });

  it('maps service.in_use to its translation key on delete failure', () => {
    deleteMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'service.in_use' } }))
    );

    facade.deleteService(ServiceListItem.fromJS({ id: 'svc-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith('errors.service.in_use');
  });
});
