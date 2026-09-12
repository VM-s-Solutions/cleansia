import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  ExtraListItem,
  PagedDataOfExtraListItem,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ExtraManagementFacade } from './extra-management.facade';

describe('ExtraManagementFacade', () => {
  let facade: ExtraManagementFacade;
  let getPagedMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let activateMock: jest.Mock;
  let deleteMock: jest.Mock;
  let getOverviewMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const page = PagedDataOfExtraListItem.fromJS({
    data: [ExtraListItem.fromJS({ id: 'ext-1', slug: 'inside-oven', name: 'Inside oven' })],
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
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ExtraManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminExtraClient: {
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
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(ExtraManagementFacade);
  });

  it('loads extras and stores data + total', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.loadExtras();

    expect(getPagedMock).toHaveBeenCalledTimes(1);
    expect(facade.extras().length).toBe(1);
    expect(facade.totalRecords()).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  it('passes searchTerm and isActive into getPaged', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.applyFilter({ searchTerm: 'oven', isActive: false });

    const args = getPagedMock.mock.calls[0];
    expect(args[0]).toBe('oven');
    expect(args[1]).toBe(false);
    expect(args[3]).toBe(0);
  });

  it('passes isActive undefined when no status filter is set', () => {
    getPagedMock.mockReturnValue(of(page));

    facade.applyFilter({ searchTerm: 'oven' });

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

    facade.loadExtras();

    expect(facade.loading()).toBe(false);
    expect(facade.extras().length).toBe(0);
  });

  it('deactivates an extra, shows success and reloads the list', () => {
    deactivateMock.mockReturnValue(of({ extraId: 'ext-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.deactivateExtra(ExtraListItem.fromJS({ id: 'ext-1' }));

    expect(deactivateMock).toHaveBeenCalledWith('ext-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.extra_management.messages.deactivate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('activates an extra, shows success and reloads the list', () => {
    activateMock.mockReturnValue(of({ extraId: 'ext-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.activateExtra(ExtraListItem.fromJS({ id: 'ext-1' }));

    expect(activateMock).toHaveBeenCalledWith('ext-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.extra_management.messages.activate_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('deletes an extra, shows success and reloads the list', () => {
    deleteMock.mockReturnValue(of({ extraId: 'ext-1' }));
    getPagedMock.mockReturnValue(of(page));

    facade.deleteExtra(ExtraListItem.fromJS({ id: 'ext-1' }));

    expect(deleteMock).toHaveBeenCalledWith('ext-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.extra_management.messages.delete_success'
    );
    expect(getPagedMock).toHaveBeenCalledTimes(1);
  });

  it('does not call deactivate, activate or delete for a row without id', () => {
    facade.deactivateExtra(ExtraListItem.fromJS({}));
    facade.activateExtra(ExtraListItem.fromJS({}));
    facade.deleteExtra(ExtraListItem.fromJS({}));

    expect(deactivateMock).not.toHaveBeenCalled();
    expect(activateMock).not.toHaveBeenCalled();
    expect(deleteMock).not.toHaveBeenCalled();
  });

  it('maps extra.not_found to its translation key on deactivate failure', () => {
    deactivateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'extra.not_found' } }))
    );

    facade.deactivateExtra(ExtraListItem.fromJS({ id: 'ext-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith('api.extra.not_found');
  });

  it('falls back to the generic error for unknown codes on activate failure', () => {
    activateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.activateExtra(ExtraListItem.fromJS({ id: 'ext-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith(
      'api.common.error_occurred'
    );
  });

  // An extra any order has ever bought cannot be deleted (the FK is ON DELETE RESTRICT); the
  // backend answers extra.in_use and the admin must be told to deactivate instead, not shown
  // the generic error.
  it('maps extra.in_use to its translation key on delete failure', () => {
    deleteMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'extra.in_use' } }))
    );

    facade.deleteExtra(ExtraListItem.fromJS({ id: 'ext-1' }));

    expect(snackbar.showError).toHaveBeenCalledWith('api.extra.in_use');
  });

  describe('the price column names its currency', () => {
    it('formats the list price in the platform default currency, not in crowns', () => {
      getPagedMock.mockReturnValue(of(page));

      facade.loadExtras();

      expect(getOverviewMock).toHaveBeenCalledTimes(1);
      expect(facade.defaultCurrencyCode()).toBe('EUR');
      expect(facade.formatCurrency(45.1)).toContain('€');
      expect(facade.formatCurrency(45.1)).not.toContain('CZK');
    });

    it('reads the currency once across reloads', () => {
      getPagedMock.mockReturnValue(of(page));

      facade.loadExtras();
      facade.loadExtras();

      expect(getOverviewMock).toHaveBeenCalledTimes(1);
      expect(getPagedMock).toHaveBeenCalledTimes(2);
    });

    it('prints a bare number rather than a currency it cannot name', () => {
      getOverviewMock.mockReturnValueOnce(of([]));
      getPagedMock.mockReturnValue(of(page));

      facade.loadExtras();

      expect(facade.defaultCurrencyCode()).toBeNull();
      expect(facade.formatCurrency(45.1)).toBe('45.1');
    });
  });
});
