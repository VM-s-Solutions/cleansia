import { inject, Injectable, signal } from '@angular/core';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';

@Injectable()
export class SavedAddressesFacade extends UnsubscribeControlDirective {
  private readonly savedAddressStore = inject(SavedAddressStore);
  private readonly snackbar = inject(SnackbarService);

  readonly addresses = this.savedAddressStore.addresses;
  readonly loading = this.savedAddressStore.loading;
  readonly mutating = signal(false);

  load(): void {
    void this.savedAddressStore.refresh();
  }

  async setDefault(id: string): Promise<void> {
    if (this.mutating()) return;
    this.mutating.set(true);
    try {
      const ok = await this.savedAddressStore.setDefault(id);
      if (ok) {
        this.snackbar.showSuccessTranslated(
          'pages.saved_addresses.default_success'
        );
      }
    } finally {
      this.mutating.set(false);
    }
  }

  async delete(id: string): Promise<void> {
    if (this.mutating()) return;
    this.mutating.set(true);
    try {
      const ok = await this.savedAddressStore.delete(id);
      if (ok) {
        this.snackbar.showSuccessTranslated(
          'pages.saved_addresses.delete_success'
        );
      }
    } finally {
      this.mutating.set(false);
    }
  }
}
