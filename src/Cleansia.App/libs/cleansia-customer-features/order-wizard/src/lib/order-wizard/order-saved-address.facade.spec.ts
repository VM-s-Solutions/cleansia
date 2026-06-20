import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AddressDto } from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { OrderSavedAddressFacade } from './order-saved-address.facade';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

describe('OrderSavedAddressFacade', () => {
  let facade: OrderSavedAddressFacade;
  let store: {
    addresses: ReturnType<typeof signal>;
    add: jest.Mock;
  };
  let formData: OrderWizardFormData;
  let patched: Partial<OrderWizardFormData>[];

  const savedAddress = {
    id: 'addr-1',
    street: 'Wenceslas 1',
    city: 'Prague',
    zipCode: '11000',
    countryId: 'cz',
    state: '',
    latitude: 50.08,
    longitude: 14.42,
  };

  function patchFormData(partial: Partial<OrderWizardFormData>): void {
    patched.push(partial);
    formData = { ...formData, ...partial };
  }

  beforeEach(() => {
    formData = { ...ORDER_WIZARD_INITIAL_DATA };
    patched = [];
    store = {
      addresses: signal([savedAddress]),
      add: jest.fn().mockResolvedValue({ id: 'addr-new' }),
    };

    TestBed.configureTestingModule({
      providers: [
        OrderSavedAddressFacade,
        { provide: SavedAddressStore, useValue: store },
      ],
    });

    facade = TestBed.inject(OrderSavedAddressFacade);
    facade.connect({
      currentFormData: () => formData,
      patchFormData,
    });
  });

  describe('selectSavedAddress', () => {
    it('copies the record into the form and marks it selected', () => {
      facade.selectSavedAddress('addr-1');

      expect(facade.selectedSavedAddressId()).toBe('addr-1');
      expect(facade.isSavedAddressSelected()).toBe(true);
      expect(patched[0].address?.street).toBe('Wenceslas 1');
      expect(patched[0].addressLatitude).toBe(50.08);
      expect(patched[0].addressLongitude).toBe(14.42);
    });

    it('is a no-op for an unknown id', () => {
      facade.selectSavedAddress('nope');

      expect(facade.selectedSavedAddressId()).toBeNull();
      expect(patched).toHaveLength(0);
    });
  });

  describe('updateAddressFromForm', () => {
    it('clears the saved binding and coordinates', () => {
      facade.selectSavedAddress('addr-1');
      patched = [];

      facade.updateAddressFromForm(
        new AddressDto({ street: 'New St 9', city: 'Plzen', zipCode: '30100', countryId: 'cz', state: '' }),
      );

      expect(facade.selectedSavedAddressId()).toBeNull();
      expect(patched[0].address?.street).toBe('New St 9');
      expect(patched[0].addressLatitude).toBeNull();
      expect(patched[0].addressLongitude).toBeNull();
    });
  });

  describe('applyAddressSuggestion', () => {
    it('captures coordinates, merges with the current address and clears the saved binding', () => {
      facade.selectSavedAddress('addr-1');
      patched = [];

      facade.applyAddressSuggestion({
        street: 'Park Ave 2',
        city: 'Ostrava',
        zipCode: '70200',
        latitude: 49.83,
        longitude: 18.28,
      });

      expect(facade.selectedSavedAddressId()).toBeNull();
      expect(patched[0].address?.street).toBe('Park Ave 2');
      expect(patched[0].address?.countryId).toBe('cz');
      expect(patched[0].addressLatitude).toBe(49.83);
      expect(patched[0].addressLongitude).toBe(18.28);
    });
  });

  describe('saveCurrentAddressAsSaved', () => {
    it('returns false and skips the store when coordinates are missing', async () => {
      formData = {
        ...formData,
        address: new AddressDto({ street: 'New St 9', city: 'Plzen', zipCode: '30100', countryId: 'cz', state: '' }),
        addressLatitude: null,
        addressLongitude: null,
      };

      const result = await facade.saveCurrentAddressAsSaved('Home');

      expect(result).toBe(false);
      expect(store.add).not.toHaveBeenCalled();
    });

    it('persists and selects the new id when coordinates exist', async () => {
      facade.applyAddressSuggestion({
        street: 'Park Ave 2',
        city: 'Ostrava',
        zipCode: '70200',
        latitude: 49.83,
        longitude: 18.28,
      });

      const result = await facade.saveCurrentAddressAsSaved('Home');

      expect(result).toBe(true);
      expect(store.add).toHaveBeenCalledTimes(1);
      expect(facade.selectedSavedAddressId()).toBe('addr-new');
    });

    it('returns false when the store does not return an id', async () => {
      store.add.mockResolvedValue(null);
      facade.applyAddressSuggestion({
        street: 'Park Ave 2',
        city: 'Ostrava',
        zipCode: '70200',
        latitude: 49.83,
        longitude: 18.28,
      });

      const result = await facade.saveCurrentAddressAsSaved('Home');

      expect(result).toBe(false);
      expect(facade.selectedSavedAddressId()).toBeNull();
    });
  });
});
