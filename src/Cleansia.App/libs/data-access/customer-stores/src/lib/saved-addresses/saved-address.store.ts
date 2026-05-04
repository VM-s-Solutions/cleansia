import { computed, inject, Injectable, signal } from '@angular/core';
import {
  AddSavedAddressCommand,
  CustomerClient,
  SavedAddressDto,
  SetDefaultSavedAddressCommand,
  UpdateSavedAddressCommand,
} from '@cleansia/customer-services';
import { SnackbarService } from '@cleansia/services';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class SavedAddressStore {
  private readonly customerClient = inject(CustomerClient);
  private readonly snackbar = inject(SnackbarService);

  readonly addresses = signal<SavedAddressDto[]>([]);
  readonly loading = signal(false);
  readonly loaded = signal(false);

  readonly defaultAddress = computed(
    () => this.addresses().find((a) => a.isDefault) ?? null
  );

  async refresh(): Promise<boolean> {
    this.loading.set(true);
    try {
      const list = await firstValueFrom(
        this.customerClient.savedAddressClient.getMine()
      );
      this.addresses.set(list ?? []);
      this.loaded.set(true);
      return true;
    } catch {
      this.snackbar.showErrorTranslated(
        'pages.profile.addresses_load_failed'
      );
      return false;
    } finally {
      this.loading.set(false);
    }
  }

  async add(command: AddSavedAddressCommand): Promise<SavedAddressDto | null> {
    try {
      const result = await firstValueFrom(
        this.customerClient.savedAddressClient.add(command)
      );
      // When setAsDefault was true, the server demoted peers — refetch to sync.
      if (command.setAsDefault) {
        await this.refresh();
      } else {
        this.addresses.update((list) => [...list, result]);
      }
      return result;
    } catch {
      this.snackbar.showErrorTranslated(
        'pages.profile.addresses_save_failed'
      );
      return null;
    }
  }

  async update(
    command: UpdateSavedAddressCommand
  ): Promise<SavedAddressDto | null> {
    try {
      const result = await firstValueFrom(
        this.customerClient.savedAddressClient.update(command)
      );
      this.addresses.update((list) =>
        list.map((a) => (a.id === result.id ? result : a))
      );
      return result;
    } catch {
      this.snackbar.showErrorTranslated(
        'pages.profile.addresses_save_failed'
      );
      return null;
    }
  }

  async setDefault(savedAddressId: string): Promise<boolean> {
    try {
      const command = new SetDefaultSavedAddressCommand({
        savedAddressId,
        userId: undefined,
      });
      await firstValueFrom(
        this.customerClient.savedAddressClient.setDefault(command)
      );
      await this.refresh();
      return true;
    } catch {
      this.snackbar.showErrorTranslated(
        'pages.profile.addresses_default_failed'
      );
      return false;
    }
  }

  async delete(id: string): Promise<boolean> {
    try {
      await firstValueFrom(
        this.customerClient.savedAddressClient.delete(id)
      );
      this.addresses.update((list) => list.filter((a) => a.id !== id));
      return true;
    } catch {
      this.snackbar.showErrorTranslated(
        'pages.profile.addresses_delete_failed'
      );
      return false;
    }
  }

  clear(): void {
    this.addresses.set([]);
    this.loaded.set(false);
  }
}
