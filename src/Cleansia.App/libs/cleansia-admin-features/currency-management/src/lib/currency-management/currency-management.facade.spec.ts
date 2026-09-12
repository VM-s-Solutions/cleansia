import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, AdminCurrencyListItem } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CurrencyManagementFacade } from './currency-management.facade';

describe('CurrencyManagementFacade', () => {
  let facade: CurrencyManagementFacade;
  let getOverviewMock: jest.Mock;
  let setDefaultMock: jest.Mock;
  let deactivateMock: jest.Mock;
  let activateMock: jest.Mock;
  let deleteMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const currencies = [
    AdminCurrencyListItem.fromJS({ id: 'cur-1', code: 'CZK', isDefault: true }),
    AdminCurrencyListItem.fromJS({ id: 'cur-2', code: 'EUR', isDefault: false }),
  ];

  beforeEach(() => {
    getOverviewMock = jest.fn();
    setDefaultMock = jest.fn();
    deactivateMock = jest.fn();
    activateMock = jest.fn();
    deleteMock = jest.fn();
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        CurrencyManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCurrencyClient: {
              getOverview: getOverviewMock,
              setDefault: setDefaultMock,
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

    facade = TestBed.inject(CurrencyManagementFacade);
  });

  it('loads the currency overview', () => {
    getOverviewMock.mockReturnValue(of(currencies));

    facade.loadCurrencies();

    expect(facade.currencies().length).toBe(2);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  // Seeded with `of(null)`, not a plausible array: the generated client answers a non-array 200
  // and a 204 with NULL. → service-form.facade.spec.ts
  it('leaves the currency list an empty array when the client answers null', () => {
    getOverviewMock.mockReturnValue(of(null));

    facade.loadCurrencies();

    expect(facade.currencies()).toEqual([]);
    expect(facade.initialLoading()).toBe(false);
  });

  it('sets a currency as default, shows success and reloads', () => {
    setDefaultMock.mockReturnValue(of({ id: 'cur-2' }));
    getOverviewMock.mockReturnValue(of(currencies));

    facade.setDefaultCurrency(currencies[1]);

    expect(setDefaultMock).toHaveBeenCalledWith('cur-2');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.currency_management.messages.set_default_success'
    );
    expect(getOverviewMock).toHaveBeenCalledTimes(1);
  });

  it('does not call setDefault for a row without id', () => {
    facade.setDefaultCurrency(AdminCurrencyListItem.fromJS({}));

    expect(setDefaultMock).not.toHaveBeenCalled();
  });

  it('does not call setDefault for the current default currency', () => {
    facade.setDefaultCurrency(currencies[0]);

    expect(setDefaultMock).not.toHaveBeenCalled();
  });

  it('maps currency.not_found to its translation key on setDefault failure', () => {
    setDefaultMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'currency.not_found' } }))
    );

    facade.setDefaultCurrency(currencies[1]);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'api.currency.not_found'
    );
  });

  it('falls back to the generic error for unknown codes on setDefault failure', () => {
    setDefaultMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.setDefaultCurrency(currencies[1]);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'api.common.error_occurred'
    );
  });

  it('maps currency.invalid to its translation key on setDefault failure', () => {
    setDefaultMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'currency.invalid' } }))
    );

    facade.setDefaultCurrency(currencies[1]);

    expect(snackbar.showError).toHaveBeenCalledWith('api.currency.invalid');
  });

  it('maps currency.not_priced to its translation key on setDefault failure', () => {
    setDefaultMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'currency.not_priced' } }))
    );

    facade.setDefaultCurrency(currencies[1]);

    expect(snackbar.showError).toHaveBeenCalledWith('api.currency.not_priced');
  });

  it('switches a currency off, shows success and reloads the list', () => {
    deactivateMock.mockReturnValue(of({ currencyId: 'cur-2' }));
    getOverviewMock.mockReturnValue(of(currencies));

    facade.deactivateCurrency(currencies[1]);

    expect(deactivateMock).toHaveBeenCalledWith('cur-2');
    expect(activateMock).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.currency_management.messages.deactivate_success'
    );
    expect(getOverviewMock).toHaveBeenCalledTimes(1);
  });

  it('switches a currency on, shows success and reloads the list', () => {
    activateMock.mockReturnValue(of({ currencyId: 'cur-2' }));
    getOverviewMock.mockReturnValue(of(currencies));

    facade.activateCurrency(currencies[1]);

    expect(activateMock).toHaveBeenCalledWith('cur-2');
    expect(deactivateMock).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.currency_management.messages.activate_success'
    );
    expect(getOverviewMock).toHaveBeenCalledTimes(1);
  });

  it('does not call deactivate or activate for a row without id', () => {
    facade.deactivateCurrency(AdminCurrencyListItem.fromJS({}));
    facade.activateCurrency(AdminCurrencyListItem.fromJS({}));

    expect(deactivateMock).not.toHaveBeenCalled();
    expect(activateMock).not.toHaveBeenCalled();
  });

  it('maps currency.cannot_deactivate_default to its translation key on deactivate failure', () => {
    deactivateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'currency.cannot_deactivate_default' } }))
    );

    facade.deactivateCurrency(currencies[0]);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'api.currency.cannot_deactivate_default'
    );
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
    expect(getOverviewMock).not.toHaveBeenCalled();
  });

  it('falls back to the generic error for unknown codes on activate failure', () => {
    activateMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.activateCurrency(currencies[1]);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'api.common.error_occurred'
    );
    expect(getOverviewMock).not.toHaveBeenCalled();
  });
});
