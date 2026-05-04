import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, OnDestroy, PLATFORM_ID, signal, AfterViewInit } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaAddressAutocompleteComponent,
  CleansiaButtonComponent,
  CleansiaTextInputComponent,
  CleansiaTelephoneComponent,
  CleansiaCalendarComponent,
  CleansiaSelectComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import type { MapboxAddressSuggestion } from '@cleansia/services';
import {
  AddSavedAddressCommand,
  CustomerClient,
  SavedAddressDto,
  UpdateSavedAddressCommand,
} from '@cleansia/customer-services';
import { SavedAddressStore } from '@cleansia/customer-stores';
import {
  ChangePasswordCommand,
  GetCurrentUserQuery,
  UpdateCurrentUserCommand,
  UserListItem,
} from '@cleansia/partner-services';
import { SnackbarService, ThemeService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { DialogModule } from 'primeng/dialog';
import { RewardsCardComponent } from '@cleansia-customer/rewards';
import { PROFILE_SECTIONS, SectionDef, setupScrollSpy } from './profile.helpers';

@Component({
  selector: 'cleansia-customer-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    TranslatePipe,
    InputTextModule,
    SkeletonModule,
    ToggleSwitchModule,
    DialogModule,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaTelephoneComponent,
    CleansiaCalendarComponent,
    CleansiaSelectComponent,
    CleansiaAddressAutocompleteComponent,
    RewardsCardComponent,
  ],
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly themeService = inject(ThemeService);
  private readonly savedAddressStore = inject(SavedAddressStore);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly router = inject(Router);

  user = signal<UserListItem | null>(null);
  loading = signal(true);
  saving = signal(false);

  activeSection = signal('personal');
  sections: SectionDef[] = PROFILE_SECTIONS;

  readonly addresses = this.savedAddressStore.addresses;
  readonly addressesLoading = this.savedAddressStore.loading;
  showAddressDialog = signal(false);
  editingAddressId = signal<string | null>(null);
  countryOptions = signal<ICleansiaSelectOption[]>([]);

  addressForm = new FormGroup({
    label: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    // street/city/zip are populated by the Mapbox autocomplete picker
    // (read-only in the dialog), so no user-typed validators are needed.
    street: new FormControl<string>('', { nonNullable: true }),
    city: new FormControl<string>('', { nonNullable: true }),
    zip: new FormControl<string>('', { nonNullable: true }),
    country: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    isDefault: new FormControl<boolean>(false, { nonNullable: true }),
  });

  /**
   * Lat/lng captured from a Mapbox autocomplete pick. Forwarded to the backend
   * so cleaners get accurate routing. Manual edits do NOT clear it — the user
   * is usually just adjusting the unit/floor, and the suggestion's coords are
   * still close enough for routing.
   */
  pickedLatitude = signal<number | undefined>(undefined);
  pickedLongitude = signal<number | undefined>(undefined);

  // Preferences
  readonly isDarkMode = computed(() => this.themeService.currentTheme() === 'dark');
  emailNotifications = signal(true);

  languageOptions = [
    { label: 'Čeština', value: 'cs' },
    { label: 'English', value: 'en' },
    { label: 'Polski', value: 'pl' },
  ];

  readonly userInitials = computed(() => {
    const u = this.user();
    if (u?.firstName && u?.lastName) {
      return `${u.firstName[0]}${u.lastName[0]}`.toUpperCase();
    }
    return null;
  });

  profileForm = new FormGroup({
    firstName: new FormControl('', [
      Validators.required,
      Validators.maxLength(50),
    ]),
    lastName: new FormControl('', [
      Validators.required,
      Validators.maxLength(50),
    ]),
    phoneNumber: new FormControl('', [Validators.maxLength(20)]),
    birthDate: new FormControl<Date | null>(null),
  });

  passwordForm = new FormGroup({
    currentPassword: new FormControl('', [Validators.required]),
    newPassword: new FormControl('', [
      Validators.required,
      Validators.pattern(/^(?=.*[a-zA-Z])(?=.*\d).{8,}$/),
    ]),
    confirmPassword: new FormControl('', [Validators.required]),
  });

  newPasswordValue = signal('');

  get hasNewPasswordInput(): boolean {
    return !!this.newPasswordValue();
  }

  readonly passwordValidation = computed(() => {
    const pw = this.newPasswordValue();
    return {
      hasMinLength: pw.length >= 8,
      hasLetter: /[a-zA-Z]/.test(pw),
      hasNumber: /\d/.test(pw),
    };
  });

  private scrollObserver: IntersectionObserver | null = null;
  showScrollTop = signal(false);

  ngOnInit(): void {
    this.loadProfile();
    if (!this.savedAddressStore.loaded()) {
      void this.savedAddressStore.refresh();
    }
    this.loadCountries();
    if (this.isBrowser) {
      window.addEventListener('scroll', this.onScroll);
    }
    this.passwordForm.controls.newPassword.valueChanges.subscribe(v => this.newPasswordValue.set(v || ''));
  }

  ngAfterViewInit(): void {
    this.setupScrollSpy();
  }

  ngOnDestroy(): void {
    this.scrollObserver?.disconnect();
    if (this.isBrowser) {
      window.removeEventListener('scroll', this.onScroll);
    }
  }

  private onScroll = (): void => {
    if (!this.isBrowser) return;
    this.showScrollTop.set(window.scrollY > 400);
  };

  scrollToTop(): void {
    if (!this.isBrowser) return;
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  private setupScrollSpy(): void {
    if (!this.isBrowser) return;
    this.scrollObserver = setupScrollSpy(this.sections, (id) =>
      this.activeSection.set(id)
    );
  }

  loadProfile(): void {
    this.loading.set(true);
    this.customerClient.userClient
      .getCurrent(new GetCurrentUserQuery())
      .subscribe({
        next: (user) => {
          this.user.set(user);
          this.profileForm.patchValue({
            firstName: user.firstName || '',
            lastName: user.lastName || '',
            phoneNumber: user.phoneNumber || '',
            birthDate: user.birthDate ? new Date(user.birthDate) : null,
          });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) return;
    this.saving.set(true);

    const user = this.user();
    const cmd = new UpdateCurrentUserCommand({
      id: user?.id,
      firstName: this.profileForm.value.firstName || undefined,
      lastName: this.profileForm.value.lastName || undefined,
      phoneNumber: this.profileForm.value.phoneNumber || undefined,
      birthDate: this.profileForm.value.birthDate || undefined,
      languageCode: this.translate.currentLang,
      photo: undefined as any,
    });

    this.customerClient.userClient.updateCurrentUser(cmd).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackbar.showSuccess(
          this.translate.instant('pages.profile.save_success')
        );
        this.loadProfile();
      },
      error: () => {
        this.saving.set(false);
        this.snackbar.showError(
          this.translate.instant('pages.profile.save_error')
        );
      },
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid || this.passwordMismatch) return;
    this.saving.set(true);

    const cmd = new ChangePasswordCommand({
      email: this.user()?.email,
      code: '',
      newPassword: this.passwordForm.value.newPassword || undefined,
    });

    this.customerClient.userClient.changePassword(cmd).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackbar.showSuccess(
          this.translate.instant('pages.profile.save_success')
        );
        this.passwordForm.reset();
      },
      error: () => {
        this.saving.set(false);
        this.snackbar.showError(
          this.translate.instant('pages.profile.save_error')
        );
      },
    });
  }

  get passwordMismatch(): boolean {
    return (
      this.passwordForm.value.newPassword !==
      this.passwordForm.value.confirmPassword
    );
  }

  // Section navigation
  scrollToSection(sectionId: string): void {
    this.activeSection.set(sectionId);
    if (!this.isBrowser) return;
    const el = document.getElementById(`profile-${sectionId}`);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }

  openAddAddress(): void {
    this.editingAddressId.set(null);
    this.pickedLatitude.set(undefined);
    this.pickedLongitude.set(undefined);
    // First saved address — default the toggle to "Set as default" so the
    // user doesn't have to flip it manually for the only address on file.
    const isFirstAddress = this.addresses().length === 0;
    this.addressForm.reset({
      label: '',
      street: '',
      city: '',
      zip: '',
      country: this.defaultCountryId(),
      isDefault: isFirstAddress,
    });
    this.showAddressDialog.set(true);
  }

  openEditAddress(address: SavedAddressDto): void {
    this.editingAddressId.set(address.id ?? null);
    // Preserve any previously-saved coords when editing — the user can re-pick
    // from autocomplete to refresh them.
    this.pickedLatitude.set(address.latitude ?? undefined);
    this.pickedLongitude.set(address.longitude ?? undefined);
    this.addressForm.reset({
      label: address.label ?? '',
      street: address.street ?? '',
      city: address.city ?? '',
      zip: address.zipCode ?? '',
      country: address.countryId ?? '',
      isDefault: address.isDefault,
    });
    this.showAddressDialog.set(true);
  }

  /**
   * Mapbox autocomplete picked a suggestion — populate the form fields and
   * stash lat/lng for the eventual AddSavedAddressCommand. Country is left
   * untouched: Mapbox doesn't return our internal Country.Id, and the user
   * has typically already chosen it (defaults to CZ).
   */
  onAddressPicked(suggestion: MapboxAddressSuggestion): void {
    this.addressForm.patchValue({
      street: suggestion.street || this.addressForm.value.street || '',
      city: suggestion.city || this.addressForm.value.city || '',
      zip: suggestion.zipCode || this.addressForm.value.zip || '',
    });
    this.pickedLatitude.set(suggestion.latitude);
    this.pickedLongitude.set(suggestion.longitude);
  }

  onAddressSearchFailed(): void {
    this.snackbar.showError(
      this.translate.instant('address_picker.search_failed')
    );
  }

  private closeAddressDialog(): void {
    this.showAddressDialog.set(false);
    this.editingAddressId.set(null);
  }

  async saveAddress(): Promise<void> {
    if (this.addressForm.invalid) {
      this.addressForm.markAllAsTouched();
      return;
    }
    const v = this.addressForm.getRawValue();

    // Mapbox pick is mandatory — street is only populated by onAddressPicked.
    if (!v.street) {
      this.snackbar.showError(
        this.translate.instant('api.address.mapbox_coords_required')
      );
      return;
    }

    // Backend now requires non-null lat/lng. Bail with the same message if
    // the picker somehow filled the address but skipped coords.
    const lat = this.pickedLatitude();
    const lng = this.pickedLongitude();
    if (lat === undefined || lng === undefined) {
      this.snackbar.showError(
        this.translate.instant('api.address.mapbox_coords_required')
      );
      return;
    }

    const editingId = this.editingAddressId();

    if (editingId) {
      const result = await this.savedAddressStore.update(
        new UpdateSavedAddressCommand({
          savedAddressId: editingId,
          label: v.label,
          street: v.street,
          city: v.city,
          zipCode: v.zip,
          countryId: v.country || undefined,
          latitude: lat,
          longitude: lng,
          userId: undefined,
        })
      );
      if (result) {
        this.snackbar.showSuccess(
          this.translate.instant('pages.profile.address_saved')
        );
        this.closeAddressDialog();
      }
    } else {
      const result = await this.savedAddressStore.add(
        new AddSavedAddressCommand({
          label: v.label,
          street: v.street,
          city: v.city,
          zipCode: v.zip,
          countryId: v.country || undefined,
          setAsDefault: v.isDefault,
          latitude: lat,
          longitude: lng,
          userId: undefined,
        })
      );
      if (result) {
        this.snackbar.showSuccess(
          this.translate.instant('pages.profile.address_saved')
        );
        this.closeAddressDialog();
      }
    }
  }

  private loadCountries(): void {
    this.customerClient.countryClient.getOverview().subscribe({
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

  private defaultCountryId(): string {
    // Pre-select Czech Republic so customers in CZ don't have to pick it every time.
    const cz = this.countryOptions().find((o) =>
      o.label.includes('(CZE)') || o.label.toLowerCase().includes('czech')
    );
    return (cz?.value as string) ?? '';
  }

  async deleteAddress(id: string): Promise<void> {
    const ok = await this.savedAddressStore.delete(id);
    if (ok) {
      this.snackbar.showSuccess(
        this.translate.instant('pages.profile.address_deleted')
      );
    }
  }

  async setDefaultAddress(id: string): Promise<void> {
    await this.savedAddressStore.setDefault(id);
  }

  // Preferences
  get currentLanguage(): string {
    return this.translate.currentLang || 'cs';
  }

  onLanguageChange(lang: string): void {
    this.translate.use(lang);
    if (this.isBrowser) {
      localStorage.setItem('preferred_language', lang);
    }
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }
}
