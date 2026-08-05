import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  AdminCountryControllerSetCountryServicedRequest,
  CreateServiceCityCommand,
  UpdateServiceCityCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { of, throwError } from 'rxjs';
import { ServiceAreaManagementFacade } from './service-area-management.facade';

describe('ServiceAreaManagementFacade', () => {
  let facade: ServiceAreaManagementFacade;
  let getOverviewMock: jest.Mock;
  let detailsMock: jest.Mock;
  let servicedMock: jest.Mock;
  let cityGetMock: jest.Mock;
  let cityPostMock: jest.Mock;
  let cityPutMock: jest.Mock;
  let cityDeleteMock: jest.Mock;
  let snackbar: { showSuccess: jest.Mock; showError: jest.Mock };

  beforeEach(() => {
    TestBed.resetTestingModule();
    getOverviewMock = jest.fn().mockReturnValue(of([]));
    detailsMock = jest.fn().mockReturnValue(of(null));
    servicedMock = jest.fn().mockReturnValue(of({ isServiced: true }));
    cityGetMock = jest.fn().mockReturnValue(of([]));
    cityPostMock = jest.fn().mockReturnValue(of({ id: 'city-1' }));
    cityPutMock = jest.fn().mockReturnValue(of({ id: 'city-1' }));
    cityDeleteMock = jest.fn().mockReturnValue(of({ id: 'city-1' }));
    snackbar = { showSuccess: jest.fn(), showError: jest.fn() };

    TestBed.configureTestingModule({
      providers: [
        ServiceAreaManagementFacade,
        {
          provide: AdminClient,
          useValue: {
            adminCountryClient: {
              getOverview: getOverviewMock,
              details: detailsMock,
              serviced: servicedMock,
            },
            apiClient: {
              adminServiceCityGet: cityGetMock,
              adminServiceCityPost: cityPostMock,
              adminServiceCityPut: cityPutMock,
              adminServiceCityDelete: cityDeleteMock,
            },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: TranslateService, useValue: { instant: (k: string) => k } },
      ],
    });

    facade = TestBed.inject(ServiceAreaManagementFacade);
  });

  it('starts empty and initially loading', () => {
    expect(facade.countries()).toEqual([]);
    expect(facade.cities()).toEqual([]);
    expect(facade.servicedCountryIds().size).toBe(0);
    expect(facade.initialLoading()).toBe(true);
  });

  it('settles both loading flags on an empty catalog without asking for any detail', () => {
    facade.loadCountries();

    expect(detailsMock).not.toHaveBeenCalled();
    expect(facade.loading()).toBe(false);
    expect(facade.initialLoading()).toBe(false);
  });

  it('settles loading and keeps the catalog empty when the overview read fails', () => {
    getOverviewMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.loadCountries();

    expect(facade.countries()).toEqual([]);
    expect(facade.initialLoading()).toBe(false);
  });

  it('collects only the serviced country ids from the per-country details', () => {
    getOverviewMock.mockReturnValue(of([{ id: 'c-1' }, { id: 'c-2' }]));
    detailsMock.mockImplementation((id: string) =>
      of({ id, isServiced: id === 'c-1' })
    );

    facade.loadCountries();

    expect([...facade.servicedCountryIds()]).toEqual(['c-1']);
    expect(facade.loading()).toBe(false);
  });

  it('adds the country to the serviced set once the toggle lands', () => {
    facade.setCountryServiced('c-1', true);

    expect([...facade.servicedCountryIds()]).toEqual(['c-1']);
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.service_area_management.messages.country_updated'
    );
  });

  it('leaves the serviced set alone when the toggle fails', () => {
    servicedMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.setCountryServiced('c-1', true);

    expect(facade.servicedCountryIds().size).toBe(0);
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  it('re-reads the city list after a create', () => {
    facade.createCity('c-1', 'Prague', '110');

    expect(cityGetMock).toHaveBeenCalledWith('c-1');
    expect(snackbar.showSuccess).toHaveBeenCalledWith(
      'pages.service_area_management.messages.city_created'
    );
  });

  it('does not re-read the city list when a create fails', () => {
    cityPostMock.mockReturnValue(throwError(() => new Error('boom')));

    facade.createCity('c-1', 'Prague', '110');

    expect(cityGetMock).not.toHaveBeenCalled();
    expect(snackbar.showSuccess).not.toHaveBeenCalled();
  });

  describe('command bodies on the wire', () => {
    it('serializes the serviced toggle with the flag', () => {
      facade.setCountryServiced('c-1', false);

      const body: AdminCountryControllerSetCountryServicedRequest =
        servicedMock.mock.calls[0][1];
      expect(body).toBeInstanceOf(AdminCountryControllerSetCountryServicedRequest);
      expect(body.toJSON()).toEqual({ isServiced: false });
    });

    it('serializes a city create with the country, the name and the zip prefix', () => {
      facade.createCity('c-1', 'Prague', '110');

      const command: CreateServiceCityCommand = cityPostMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(CreateServiceCityCommand);
      expect(command.toJSON()).toEqual({
        countryId: 'c-1',
        name: 'Prague',
        zipPrefix: '110',
      });
    });

    it('sends an absent zip prefix as undefined rather than null', () => {
      facade.createCity('c-1', 'Prague', null);

      const command: CreateServiceCityCommand = cityPostMock.mock.calls[0][0];
      expect(command.zipPrefix).toBeUndefined();
      expect(command.toJSON()).toEqual({
        countryId: 'c-1',
        name: 'Prague',
        zipPrefix: undefined,
      });
    });

    it('serializes a city update with the id, the name, the zip prefix and the active flag', () => {
      facade.updateCity('city-1', 'Brno', '602', false, 'c-1');

      const command: UpdateServiceCityCommand = cityPutMock.mock.calls[0][1];
      expect(command).toBeInstanceOf(UpdateServiceCityCommand);
      expect(command.toJSON()).toEqual({
        id: 'city-1',
        name: 'Brno',
        zipPrefix: '602',
        isActive: false,
      });
    });
  });
});
