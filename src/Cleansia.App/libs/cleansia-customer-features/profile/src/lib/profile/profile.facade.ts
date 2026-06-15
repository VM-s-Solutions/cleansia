import { inject, Injectable, signal } from '@angular/core';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AddSavedAddressCommand,
  ChangePasswordCommand,
  CustomerClient,
  GetCurrentUserQuery,
  MyProfileDto,
  UpdateCurrentUserCommand,
  UpdateSavedAddressCommand,
} from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class ProfileFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly savedAddressStore = inject(SavedAddressStore);

  user = signal<MyProfileDto | null>(null);
  loading = signal(true);
  saving = signal(false);

  readonly addresses = this.savedAddressStore.addresses;
  readonly addressesLoading = this.savedAddressStore.loading;
  countryOptions = signal<ICleansiaSelectOption[]>([]);

  loadProfile(
    onSuccess?: (user: MyProfileDto) => void,
  ): void {
    this.loading.set(true);
    this.customerClient.userClient
      .getCurrent(new GetCurrentUserQuery())
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (user) => {
          this.user.set(user);
          this.loading.set(false);
          onSuccess?.(user);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  saveProfile(
    cmd: UpdateCurrentUserCommand,
    onSuccess?: () => void,
  ): void {
    this.saving.set(true);
    this.customerClient.userClient
      .updateCurrentUser(cmd)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.snackbar.showSuccess(
            this.translate.instant('pages.profile.save_success'),
          );
          onSuccess?.();
        },
        error: () => {
          this.saving.set(false);
          this.snackbar.showError(
            this.translate.instant('pages.profile.save_error'),
          );
        },
      });
  }

  changePassword(
    cmd: ChangePasswordCommand,
    onSuccess?: () => void,
  ): void {
    this.saving.set(true);
    this.customerClient.userClient
      .changePassword(cmd)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.snackbar.showSuccess(
            this.translate.instant('pages.profile.save_success'),
          );
          onSuccess?.();
        },
        error: () => {
          this.saving.set(false);
          this.snackbar.showError(
            this.translate.instant('pages.profile.save_error'),
          );
        },
      });
  }

  loadCountries(): void {
    // Customer profile only ever uses this to render the address country
    // picker, so use the serviced list — same reasoning as the order
    // wizard. See planning/active/service-areas.md.
    this.customerClient.countryClient
      .getServiced()
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: (countries) => {
          const currentLang = this.translate.currentLang;
          const options: ICleansiaSelectOption[] = (countries ?? []).map((country) => {
            const translation = country.translations?.[currentLang]?.name;
            const name = translation ?? country.name ?? '';
            const iso = country.isoCode ?? '';
            return {
              label: iso ? `${name} (${iso})` : name,
              value: country.id!,
            };
          });
          this.countryOptions.set(options);
        },
      });
  }

  refreshSavedAddresses(): void {
    if (!this.savedAddressStore.loaded()) {
      void this.savedAddressStore.refresh();
    }
  }

  async addSavedAddress(command: AddSavedAddressCommand): Promise<boolean> {
    const result = await this.savedAddressStore.add(command);
    if (result) {
      this.snackbar.showSuccess(
        this.translate.instant('pages.profile.address_saved'),
      );
      return true;
    }
    return false;
  }

  async updateSavedAddress(
    command: UpdateSavedAddressCommand,
  ): Promise<boolean> {
    const result = await this.savedAddressStore.update(command);
    if (result) {
      this.snackbar.showSuccess(
        this.translate.instant('pages.profile.address_saved'),
      );
      return true;
    }
    return false;
  }

  async deleteSavedAddress(id: string): Promise<void> {
    const ok = await this.savedAddressStore.delete(id);
    if (ok) {
      this.snackbar.showSuccess(
        this.translate.instant('pages.profile.address_deleted'),
      );
    }
  }

  async setDefaultSavedAddress(id: string): Promise<void> {
    await this.savedAddressStore.setDefault(id);
  }

  showAddressSearchFailed(): void {
    this.snackbar.showError(
      this.translate.instant('address_picker.search_failed'),
    );
  }

  showCoordsRequired(): void {
    this.snackbar.showError(
      this.translate.instant('api.address.mapbox_coords_required'),
    );
  }
}
