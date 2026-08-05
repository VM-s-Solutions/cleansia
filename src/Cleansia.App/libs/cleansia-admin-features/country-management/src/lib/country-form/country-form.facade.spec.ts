import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCountryCommand,
  UpdateCountryCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { CountryFormData, CountryFormFacade } from './country-form.facade';

describe('CountryFormFacade', () => {
  let facade: CountryFormFacade;
  let createMock: jest.Mock;
  let updateMock: jest.Mock;
  let detailsMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };
  let navigate: jest.Mock;

  const formData: CountryFormData = {
    isoCode: 'CZ',
    name: 'Czechia',
  };

  beforeEach(() => {
    TestBed.resetTestingModule();
    createMock = jest.fn().mockReturnValue(of({ id: 'country-1' }));
    updateMock = jest.fn().mockReturnValue(of({ id: 'country-1' }));
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
    it('serializes a create with the ISO code and the name', () => {
      facade.createCountry(formData);

      const command: CreateCountryCommand = createMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateCountryCommand);
      expect(command.toJSON()).toEqual({
        isoCode: 'CZ',
        name: 'Czechia',
      });
    });

    it('serializes an update with the country id and the name, and no ISO code — the code is immutable', () => {
      facade.updateCountry('country-1', { isoCode: 'SK', name: 'Slovakia' });

      const command: UpdateCountryCommand = updateMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateCountryCommand);
      expect(command.toJSON()).toEqual({
        countryId: 'country-1',
        name: 'Slovakia',
      });
    });
  });
});
