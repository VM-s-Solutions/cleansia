import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, CurrencyListItem } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CurrencyManagementFacade } from './currency-management.facade';

describe('CurrencyManagementFacade', () => {
  let facade: CurrencyManagementFacade;
  let getOverviewMock: jest.Mock;
  let setDefaultMock: jest.Mock;
  let deleteMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  const currencies = [
    CurrencyListItem.fromJS({ id: 'cur-1', code: 'CZK', isDefault: true }),
    CurrencyListItem.fromJS({ id: 'cur-2', code: 'EUR', isDefault: false }),
  ];

  beforeEach(() => {
    getOverviewMock = jest.fn();
    setDefaultMock = jest.fn();
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
    facade.setDefaultCurrency(CurrencyListItem.fromJS({}));

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
      'errors.currency.not_found'
    );
  });

  it('falls back to the generic error for unknown codes on setDefault failure', () => {
    setDefaultMock.mockReturnValue(
      throwError(() => ({ result: { detail: 'something.unknown' } }))
    );

    facade.setDefaultCurrency(currencies[1]);

    expect(snackbar.showError).toHaveBeenCalledWith(
      'errors.common.error_occurred'
    );
  });
});
