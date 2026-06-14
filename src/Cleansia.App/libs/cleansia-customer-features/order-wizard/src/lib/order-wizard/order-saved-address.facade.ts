import { Injectable, inject, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { AddSavedAddressCommand, AddressDto } from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { OrderWizardFormData } from './order-wizard.models';

/** Dependencies the saved-address collaborator reads from the orchestrating wizard facade. */
interface SavedAddressConnection {
  currentFormData: () => OrderWizardFormData;
  patchFormData: (partial: Partial<OrderWizardFormData>) => void;
}

/**
 * Saved-address management for the booking wizard.
 *
 * Wraps the cross-feature `SavedAddressStore` (selection, persisting a new
 * address) and keeps the wizard's address form fields in sync. Manual edits or
 * Mapbox picks break the saved binding so the order POSTs a one-off
 * `customerAddress` instead of a `savedAddressId`. The orchestrating facade owns
 * the form model and connects the read/patch callbacks in via [connect].
 */
@Injectable()
export class OrderSavedAddressFacade extends UnsubscribeControlDirective {
  private readonly savedAddressStore = inject(SavedAddressStore);

  private deps: SavedAddressConnection | null = null;

  readonly savedAddresses = this.savedAddressStore.addresses;
  readonly selectedSavedAddressId = signal<string | null>(null);

  connect(deps: SavedAddressConnection): void {
    this.deps = deps;
  }

  isSavedAddressSelected(): boolean {
    return this.selectedSavedAddressId() !== null;
  }

  selectSavedAddress(addressId: string): void {
    const addr = this.savedAddresses().find((a) => a.id === addressId);
    if (!addr) return;
    this.selectedSavedAddressId.set(addressId);
    this.deps?.patchFormData({
      address: new AddressDto({
        street: addr.street ?? '',
        city: addr.city ?? '',
        zipCode: addr.zipCode ?? '',
        countryId: addr.countryId ?? '',
        state: addr.state ?? '',
      }),
      addressLatitude: addr.latitude ?? null,
      addressLongitude: addr.longitude ?? null,
    });
  }

  updateAddressFromForm(next: AddressDto): void {
    // Manual edits to the address break the saved-address binding — the user is
    // entering a one-off. Nulling the id ensures we POST customerAddress instead
    // of savedAddressId on submit. Also clears any previously-picked coords —
    // they don't belong to this freshly-typed address anymore.
    this.selectedSavedAddressId.set(null);
    this.deps?.patchFormData({
      address: next,
      addressLatitude: null,
      addressLongitude: null,
    });
  }

  applyAddressSuggestion(suggestion: {
    street: string;
    city: string;
    zipCode: string;
    latitude: number;
    longitude: number;
  }): void {
    this.selectedSavedAddressId.set(null);
    const current = this.deps?.currentFormData().address;
    this.deps?.patchFormData({
      address: new AddressDto({
        street: suggestion.street || current?.street || '',
        city: suggestion.city || current?.city || '',
        zipCode: suggestion.zipCode || current?.zipCode || '',
        countryId: current?.countryId ?? '',
        state: current?.state ?? '',
      }),
      addressLatitude: suggestion.latitude,
      addressLongitude: suggestion.longitude,
    });
  }

  async saveCurrentAddressAsSaved(label: string): Promise<boolean> {
    const data = this.deps?.currentFormData();
    if (!data) return false;
    const addr = data.address;
    // Backend requires Mapbox-resolved coordinates. If the user hasn't picked
    // a suggestion, we can't save — bail out.
    const lat = data.addressLatitude;
    const lng = data.addressLongitude;
    if (lat === undefined || lng === undefined || lat === null || lng === null) {
      return false;
    }
    const command = new AddSavedAddressCommand({
      label,
      street: addr.street ?? '',
      city: addr.city ?? '',
      zipCode: addr.zipCode ?? '',
      countryId: addr.countryId ?? undefined,
      setAsDefault: false,
      latitude: lat,
      longitude: lng,
    });
    const result = await this.savedAddressStore.add(command);
    if (result?.id) {
      this.selectedSavedAddressId.set(result.id);
      return true;
    }
    return false;
  }
}
