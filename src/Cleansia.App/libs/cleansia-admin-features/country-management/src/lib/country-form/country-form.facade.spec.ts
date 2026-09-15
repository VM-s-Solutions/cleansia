import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCountryCommand,
  UpdateCountryCommand,
  UpdateCountryMarketContentCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CountryFormData, CountryFormFacade } from './country-form.facade';

describe('CountryFormFacade', () => {
  let facade: CountryFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let marketContentMock: jest.Mock;
  let detailsMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const formData: CountryFormData = {
    isoCode: 'CZE',
    isoAlpha2: 'CZ',
    name: 'Czechia',
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ id: 'country-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'country-1' }));
    marketContentMock = jest.fn().mockReturnValue(of({ countryId: 'country-1' }));
    detailsMock = jest.fn().mockReturnValue(of(null));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };
    navigate = jest.fn();

    TestBed.configureTestingModule({
      providers: [
        CountryFormFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCountryClient: {
              create: createMock,
              update: updateMock,
              marketContent: marketContentMock,
              details: detailsMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
        { provide: Router, useValue: { navigate } },
      ],
    });

    facade = TestBed.inject(CountryFormFacade);
  });

  it('starts empty with nothing loading', () => {
    expect(facade.country()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(facade.saving()).toBe(false);
  });

  it('reports success and returns to the list once a create lands', () => {
    facade.createCountry(formData);

    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.country_form.messages.create_success'
    );
    expect(navigate).toHaveBeenCalled();
    expect(facade.saving()).toBe(false);
  });

  it('clears saving and stays on the form when a create fails', () => {
    createMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createCountry(formData);

    expect(facade.saving()).toBe(false);
    expect(navigate).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  it('leaves the loaded country untouched and returns to the list when the detail read fails', () => {
    detailsMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadCountry('country-1');

    expect(facade.country()).toBeNull();
    expect(facade.loading()).toBe(false);
    expect(navigate).toHaveBeenCalled();
  });

  describe('command bodies on the wire', () => {
    it('serializes a create with the alpha-3 code, the alpha-2 code and the name', () => {
      facade.createCountry(formData);

      const command: CreateCountryCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateCountryCommand);
      expect(command.toJSON()).toEqual({
        isoCode: 'CZE',
        isoAlpha2: 'CZ',
        name: 'Czechia',
      });
    });

    it('serializes an update with the country id, the name and the alpha-2 code, and no alpha-3 code — that one is immutable', () => {
      facade.updateCountry('country-1', { isoCode: 'SVK', isoAlpha2: 'SK', name: 'Slovakia' }, null);

      const command: UpdateCountryCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateCountryCommand);
      expect(command.toJSON()).toEqual({
        countryId: 'country-1',
        name: 'Slovakia',
        isoAlpha2: 'SK',
      });
    });

    it('leaves the alpha-2 code out of an update when it was left blank, so the server keeps the stored one', () => {
      facade.updateCountry('country-1', { isoCode: 'SVK', isoAlpha2: '', name: 'Slovakia' }, null);

      const command: UpdateCountryCommand = updateMock.mock.calls[0][1];
      expect(command.toJSON().isoAlpha2).toBeUndefined();
      expect(JSON.stringify(command)).not.toContain('isoAlpha2');
    });
  });

  describe('market content', () => {
    it('runs the country update first and the market-content update second, and reports both', () => {
      facade.updateCountry('country-1', formData, { insuranceCoverageAmount: 1000000 });

      expect(updateMock).toHaveBeenCalledTimes(1);
      expect(marketContentMock).toHaveBeenCalledTimes(1);
      expect(updateMock.mock.invocationCallOrder[0]).toBeLessThan(
        marketContentMock.mock.invocationCallOrder[0]
      );
      const [countryId, command] = marketContentMock.mock.calls[0] as [
        string,
        UpdateCountryMarketContentCommand,
      ];
      expect(countryId).toBe('country-1');
      expect(command).toBeInstanceOf(UpdateCountryMarketContentCommand);
      expect(command.toJSON()).toEqual({
        countryId: 'country-1',
        insuranceCoverageAmount: 1000000,
      });
      expect(snackbar.showSuccess).toHaveBeenNthCalledWith(
        1,
        'pages.country_form.messages.update_success'
      );
      expect(snackbar.showSuccess).toHaveBeenNthCalledWith(
        2,
        'pages.country_form.messages.market_content_success'
      );
      expect(navigate).toHaveBeenCalledTimes(1);
      expect(facade.saving()).toBe(false);
    });

    it('sends a cleared ceiling as an absent amount, which the server stores as null', () => {
      facade.updateCountry('country-1', formData, { insuranceCoverageAmount: null });

      const command: UpdateCountryMarketContentCommand = marketContentMock.mock.calls[0][1];
      expect(command.toJSON().insuranceCoverageAmount).toBeUndefined();
      expect(JSON.stringify(command)).not.toContain('insuranceCoverageAmount');
    });

    it('does not touch the market content when the form had no market section to save', () => {
      facade.updateCountry('country-1', formData, null);

      expect(marketContentMock).not.toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledTimes(1);
    });

    it('does not run the market-content update when the country update failed', () => {
      updateMock.mockReturnValue(throwError(() => new Error('boom')));

      facade.updateCountry('country-1', formData, { insuranceCoverageAmount: 5 });

      expect(marketContentMock).not.toHaveBeenCalled();
      expect(navigate).not.toHaveBeenCalled();
      expect(facade.saving()).toBe(false);
    });

    it('stays on the form with the country update reported when the market-content update is refused', () => {
      marketContentMock.mockReturnValue(throwError(() => new Error('country.configuration_missing')));

      facade.updateCountry('country-1', formData, { insuranceCoverageAmount: 5 });

      expect(snackbar.showSuccess).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.country_form.messages.update_success'
      );
      expect(navigate).not.toHaveBeenCalled();
      expect(facade.saving()).toBe(false);
    });

    it('updates the market content on its own', () => {
      facade.updateMarketContent('country-1', { insuranceCoverageAmount: 250000 });

      expect(marketContentMock).toHaveBeenCalledTimes(1);
      expect(snackbar.showSuccess).toHaveBeenCalledWith(
        'pages.country_form.messages.market_content_success'
      );
      expect(navigate).toHaveBeenCalledTimes(1);
      expect(facade.saving()).toBe(false);
    });
  });
});
