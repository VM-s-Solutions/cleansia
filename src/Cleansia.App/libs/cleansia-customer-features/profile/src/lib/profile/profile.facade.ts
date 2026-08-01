import { inject, Injectable, signal } from '@angular/core';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AddSavedAddressCommand,
  BlobFileDto,
  ChangePasswordCommand,
  CustomerClient,
  GetCurrentUserQuery,
  MyProfileDto,
  UpdateSavedAddressCommand,
} from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  AvatarIntent,
  ProfileDetails,
  buildAvatarBlobFile,
  buildUpdateCurrentUserCommand,
  readFileAsDataUrl,
  validateAvatarFile,
} from './profile.models';

@Injectable()
export class ProfileFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly savedAddressStore = inject(SavedAddressStore);

  user = signal<MyProfileDto | null>(null);
  loading = signal(true);
  saving = signal(false);

  readonly avatarUrl = signal<string | null>(null);
  readonly avatarSaving = signal(false);

  private avatarFileName: string | null = null;
  private avatarRetryAvailable = true;
  private adoptNextAvatarUrl = false;

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
          this.applyAvatar(user.profilePhoto);
          this.loading.set(false);
          onSuccess?.(user);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  saveProfile(
    details: ProfileDetails,
    onSuccess?: () => void,
  ): void {
    this.saving.set(true);
    this.customerClient.userClient
      .updateCurrentUser(
        buildUpdateCurrentUserCommand(details, { kind: 'unchanged' }),
      )
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

  async uploadAvatar(file: File): Promise<void> {
    const validation = validateAvatarFile(file);
    if (!validation.valid) {
      this.snackbar.showErrorTranslated(validation.errorKey);
      return;
    }

    let photo: BlobFileDto;
    try {
      photo = buildAvatarBlobFile(file, await readFileAsDataUrl(file));
    } catch {
      this.snackbar.showErrorTranslated('pages.profile.avatar.read_failed');
      return;
    }

    this.submitAvatarChange(
      { kind: 'upload', photo },
      'pages.profile.avatar.upload_success',
    );
  }

  removeAvatar(): void {
    if (!this.user()?.profilePhoto?.fileName) return;

    this.submitAvatarChange(
      { kind: 'remove' },
      'pages.profile.avatar.remove_success',
    );
  }

  onAvatarLoaded(): void {
    this.avatarRetryAvailable = true;
  }

  /**
   * An `<img>` error carries no status — ORB strips the body, so an expired SAS and a deleted blob
   * are indistinguishable here. Re-read the profile once for a fresh signature; a second failure
   * means the blob is gone, so fall back to the initials.
   */
  onAvatarLoadFailed(): void {
    if (!this.avatarRetryAvailable) {
      this.avatarUrl.set(null);
      return;
    }

    this.avatarRetryAvailable = false;
    this.adoptNextAvatarUrl = true;
    this.loadProfile();
  }

  private submitAvatarChange(intent: AvatarIntent, successKey: string): void {
    const user = this.user();
    if (!user) return;

    this.avatarSaving.set(true);
    this.customerClient.userClient
      .updateCurrentUser(
        buildUpdateCurrentUserCommand(this.detailsOf(user), intent),
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.avatarSaving.set(false)),
      )
      .subscribe((response) => {
        if (!response) return;
        this.snackbar.showSuccess(this.translate.instant(successKey));
        this.loadProfile();
      });
  }

  private detailsOf(user: MyProfileDto): ProfileDetails {
    return {
      firstName: user.firstName,
      lastName: user.lastName,
      phoneNumber: user.phoneNumber,
      birthDate: user.birthDate,
      languageCode: user.preferredLanguageCode,
    };
  }

  /**
   * The signed url is minted per read, so it is not a cache key — the blob name is, and the backend
   * mints a new one on every replace. Holding the url steady while the name is unchanged keeps the
   * browser's copy of the image.
   */
  private applyAvatar(photo: BlobFileDto | undefined): void {
    const adopt = this.adoptNextAvatarUrl;
    this.adoptNextAvatarUrl = false;

    const fileName = photo?.fileName ?? null;
    const url = photo?.blobUrl ?? null;

    if (!fileName || !url) {
      this.avatarFileName = null;
      this.avatarRetryAvailable = true;
      this.avatarUrl.set(null);
      return;
    }

    if (fileName !== this.avatarFileName) {
      this.avatarRetryAvailable = true;
      this.avatarUrl.set(url);
    } else if (adopt) {
      this.avatarUrl.set(url);
    }

    this.avatarFileName = fileName;
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
