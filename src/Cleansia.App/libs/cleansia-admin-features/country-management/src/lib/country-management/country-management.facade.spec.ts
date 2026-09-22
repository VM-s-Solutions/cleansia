import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { AdminClient, CountryListItem } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CountryManagementFacade } from './country-management.facade';

describe('CountryManagementFacade', () => {
  let facade: CountryManagementFacade;
  let getOverviewMock: jest.Mock;
  let defaultMarketMock: jest.Mock;
  let deleteMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: {
    showSuccess: jest.Mock;
    showSuccessTranslated: jest.Mock;
    showError: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  const countries = [
    CountryListItem.fromJS({ id: 'c-1', isoCode: 'CZE', isDefaultMarket: true }),
    CountryListItem.fromJS({ id: 'c-2', isoCode: 'SVK', isDefaultMarket: false }),
  ];

  beforeEach(() => {
    TestBed.resetTestingModule();
    getOverviewMock = jest.fn().mockReturnValue(of([]));
    defaultMarketMock = jest.fn();
    deleteMock = jest.fn().mockReturnValue(of(null));
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = {
      showSuccess: jest.fn(),
      showSuccessTranslated: jest.fn(),
      showError: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    TestBed.configureTestingModule({
      providers: [
        CountryManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCountryClient: {
              getOverview: getOverviewMock,
              defaultMarket: defaultMarketMock,
              delete: deleteMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    });

    facade = TestBed.inject(CountryManagementFacade);
  });

  it('loads the country overview', () => {
    getOverviewMock.mockReturnValue(
      of([CountryListItem.fromJS({ id: 'c-1', name: 'Czechia' })])
    );

    facade.loadCountries();

    expect(facade.countries().length).toBe(1);
    expect(facade.initialLoading()).toBe(false);
    expect(facade.loading()).toBe(false);
  });

  // Seeded with `of(null)`, not a plausible array: the generated client answers a non-array 200
  // and a 204 with NULL. → service-form.facade.spec.ts
  it('leaves the country list an empty array when the client answers null', () => {
    getOverviewMock.mockReturnValue(of(null));

    facade.loadCountries();

    expect(facade.countries()).toEqual([]);
    expect(facade.initialLoading()).toBe(false);
  });

  describe('setDefaultMarket', () => {
    it('promotes the country, shows success and re-reads the list', () => {
      defaultMarketMock.mockReturnValue(of({ countryId: 'c-2' }));
      getOverviewMock.mockReturnValue(of(countries));

      facade.setDefaultMarket(countries[1]);

      expect(defaultMarketMock).toHaveBeenCalledWith('c-2');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith(
        'pages.country_management.messages.set_default_market_success'
      );
      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(getOverviewMock).toHaveBeenCalledTimes(1);
    });

    it('does not call the server for a row without id', () => {
      facade.setDefaultMarket(CountryListItem.fromJS({}));

      expect(defaultMarketMock).not.toHaveBeenCalled();
    });

    it('does not call the server for the row that already is the default market', () => {
      facade.setDefaultMarket(countries[0]);

      expect(defaultMarketMock).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
    });

    it.each([
      'country.not_serviced',
      'country.market_not_ready',
      'country.default_market_changed_concurrently',
      'country.not_found',
    ])('leaves the %s refusal to the interceptor toast and leaves the list alone', (code) => {
      defaultMarketMock.mockReturnValue(
        throwError(() => ({ result: { detail: code } }))
      );

      facade.setDefaultMarket(countries[1]);

      expect(snackbar.showError).not.toHaveBeenCalled();
      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(getOverviewMock).not.toHaveBeenCalled();
    });

    it('leaves an unparsed refusal to the interceptor toast', () => {
      defaultMarketMock.mockReturnValue(
        throwError(() => ({
          response: JSON.stringify({ detail: 'country.market_not_ready' }),
        }))
      );

      facade.setDefaultMarket(countries[1]);

      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
    });

    it('leaves an unknown refusal to the interceptor toast', () => {
      defaultMarketMock.mockReturnValue(
        throwError(() => ({ result: { detail: 'something.unknown' } }))
      );

      facade.setDefaultMarket(countries[1]);

      expect(snackbar.showErrorTranslated).not.toHaveBeenCalled();
      expect(getOverviewMock).not.toHaveBeenCalled();
    });

    it('treats an empty 2xx body as no promotion', () => {
      defaultMarketMock.mockReturnValue(of(null));

      facade.setDefaultMarket(countries[1]);

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(getOverviewMock).not.toHaveBeenCalled();
    });
  });

  describe('deleteCountry', () => {
    it('asks in red with a delete label, deletes, shows success and re-reads the list', () => {
      deleteMock.mockReturnValue(of({ id: 'c-2' }));
      getOverviewMock.mockReturnValue(of(countries));

      facade.deleteCountry(countries[1]);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.country_management.delete_confirm',
        'pages.country_management.delete_country',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      );
      expect(deleteMock).toHaveBeenCalledWith('c-2');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.country_management.messages.delete_success');
      expect(getOverviewMock).toHaveBeenCalledTimes(1);
    });
  });

  it('does nothing when the confirmation is declined', () => {
    confirmMock.mockReturnValue(of(false));

    facade.setDefaultMarket(countries[1]);
    facade.deleteCountry(countries[1]);

    expect(confirmMock).toHaveBeenCalledTimes(2);
    expect(defaultMarketMock).not.toHaveBeenCalled();
    expect(deleteMock).not.toHaveBeenCalled();
    expect(getOverviewMock).not.toHaveBeenCalled();
  });
});
