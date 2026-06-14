import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AddressDto, CustomerClient } from '@cleansia/customer-services';
import { of, throwError } from 'rxjs';
import { OrderServiceAreaFacade } from './order-service-area.facade';

describe('OrderServiceAreaFacade', () => {
  let facade: OrderServiceAreaFacade;
  let apiClient: { serviceCity: jest.Mock };
  let address: AddressDto;

  function makeAddress(partial: Partial<{ city: string; countryId: string }> = {}): AddressDto {
    return new AddressDto({
      street: 'Main 1',
      city: partial.city ?? 'Prague',
      zipCode: '11000',
      countryId: partial.countryId ?? 'cz',
      state: '',
    });
  }

  function build(platform: 'server' | 'browser'): void {
    apiClient = { serviceCity: jest.fn().mockReturnValue(of([{ name: 'Prague' }])) };
    address = makeAddress();

    TestBed.configureTestingModule({
      providers: [
        OrderServiceAreaFacade,
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CustomerClient, useValue: { apiClient } },
      ],
    });

    facade = TestBed.inject(OrderServiceAreaFacade);
    facade.connect({ currentAddress: () => address });
  }

  describe('refreshCheck (browser)', () => {
    beforeEach(() => build('browser'));

    it('starts idle', () => {
      expect(facade.cityServiced()).toBe('idle');
    });

    it('stays idle and skips the network when the city is empty', () => {
      address = makeAddress({ city: '' });

      facade.refreshCheck();

      expect(facade.cityServiced()).toBe('idle');
      expect(apiClient.serviceCity).not.toHaveBeenCalled();
    });

    it('stays idle and skips the network when the country is empty', () => {
      address = makeAddress({ countryId: '' });

      facade.refreshCheck();

      expect(facade.cityServiced()).toBe('idle');
      expect(apiClient.serviceCity).not.toHaveBeenCalled();
    });

    it('resolves to ok when the city matches a served city', () => {
      apiClient.serviceCity.mockReturnValue(of([{ name: 'Prague' }]));

      facade.refreshCheck();

      expect(facade.cityServiced()).toBe('ok');
    });

    it('matches the city case-insensitively', () => {
      address = makeAddress({ city: 'prague' });
      apiClient.serviceCity.mockReturnValue(of([{ name: 'PRAGUE' }]));

      facade.refreshCheck();

      expect(facade.cityServiced()).toBe('ok');
    });

    it('resolves to rejected when the city is not served', () => {
      apiClient.serviceCity.mockReturnValue(of([{ name: 'Brno' }]));

      facade.refreshCheck();

      expect(facade.cityServiced()).toBe('rejected');
    });

    it('degrades to error (pass-through) on a network failure', () => {
      apiClient.serviceCity.mockReturnValue(throwError(() => new Error('boom')));

      facade.refreshCheck();

      expect(facade.cityServiced()).toBe('error');
    });

    it('skips re-querying when the city/country key is unchanged', () => {
      facade.refreshCheck();
      facade.refreshCheck();

      expect(apiClient.serviceCity).toHaveBeenCalledTimes(1);
    });

    it('re-queries when the city changes', () => {
      facade.refreshCheck();
      address = makeAddress({ city: 'Brno' });
      facade.refreshCheck();

      expect(apiClient.serviceCity).toHaveBeenCalledTimes(2);
    });

    it('returns to idle when the city is cleared after a successful check', () => {
      facade.refreshCheck();
      expect(facade.cityServiced()).toBe('ok');

      address = makeAddress({ city: '' });
      facade.refreshCheck();

      expect(facade.cityServiced()).toBe('idle');
    });
  });

  describe('refreshCheck (server)', () => {
    beforeEach(() => build('server'));

    it('does nothing during SSR', () => {
      facade.refreshCheck();

      expect(apiClient.serviceCity).not.toHaveBeenCalled();
      expect(facade.cityServiced()).toBe('idle');
    });
  });
});
