import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, OnDestroy, PLATFORM_ID, signal, AfterViewInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { isPlatformBrowser } from '@angular/common';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
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
import { LoyaltyTier, SavedAddressDto } from '@cleansia/customer-services';
import { persistPreferredLanguage, ThemeService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { DialogModule } from 'primeng/dialog';
import { NotificationPreferencesComponent } from '../notification-preferences/notification-preferences.component';
import { PROFILE_SECTIONS, SectionDef, setupScrollSpy } from './profile.helpers';
import { ProfileFacade } from './profile.facade';
import {
  AVATAR_ACCEPT_ATTRIBUTE,
  CreditRow,
  SavedAddressFields,
  buildAddSavedAddressCommand,
  buildChangePasswordCommand,
  buildCreditRow,
  buildUpdateSavedAddressCommand,
} from './profile.models';

@Component({
  selector: 'cleansia-customer-profile',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
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
    NotificationPreferencesComponent,
  ],
  providers: [ProfileFacade],
  templateUrl: './profile.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly translate = inject(TranslateService);
  private readonly themeService = inject(ThemeService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly facade = inject(ProfileFacade);
  private readonly destroyRef = inject(DestroyRef);
  readonly router = inject(Router);

  // Re-expose facade signals so the existing template bindings keep working.
  readonly user = this.facade.user;
  readonly loading = this.facade.loading;
  readonly saving = this.facade.saving;
  readonly addresses = this.facade.addresses;
  readonly addressesLoading = this.facade.addressesLoading;
  readonly countryOptions = this.facade.countryOptions;
  readonly avatarUrl = this.facade.avatarUrl;
  readonly avatarSaving = this.facade.avatarSaving;

  readonly avatarAccept = AVATAR_ACCEPT_ATTRIBUTE;

  activeSection = signal('personal');
  sections: SectionDef[] = PROFILE_SECTIONS;

  showAddressDialog = signal(false);
  editingAddressId = signal<string | null>(null);

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

  /**
   * i18n suffix for the tier the rail names, or null when the account has no
   * loyalty record yet — the row is then omitted rather than shown as blank.
   * Mirrors RewardsFacade.tierKey; duplicated as four cases rather than
   * imported, because reaching into a sibling feature for a switch would cross
   * a module boundary to save eight lines.
   */
  /**
   * The credit balance for the rail row. Exposed through the component rather than the private
   * facade, exactly like `loyaltyTierKey` beside it.
   *
   * Null means ONLY that the read failed — `loadCredit` maps an error to null — and the row is then
   * omitted rather than claiming a balance nobody verified. A balance of zero is NOT that case:
   * `GetMyCredit` answers `Balance: 0` in the platform's default currency for a customer who has
   * never had an account, precisely "so the client renders one shape either way". Hiding the row at
   * zero left a customer unable to tell "you have no credit" from "this screen does not mention
   * credit", which is the question the row exists to answer. Owner remark 2026-09-06.
   */
  readonly creditBalance = computed<CreditRow | null>(() => buildCreditRow(this.facade.credit()));

  readonly loyaltyTierKey = computed<string | null>(() => {
    switch (this.facade.loyaltyTier()) {
      case LoyaltyTier.PlatinumSparkler:
        return 'platinum_sparkler';
      case LoyaltyTier.GoldPolisher:
        return 'gold_polisher';
      case LoyaltyTier.SilverMopper:
        return 'silver_mopper';
      case LoyaltyTier.BronzeCleaner:
        return 'bronze_cleaner';
      default:
        return null;
    }
  });

  /**
   * The languages the PLATFORM has, in their own names — read from ngx-translate rather than typed
   * out here.
   *
   * <p>It was a hardcoded list of three, and it was wrong in both directions: it offered <b>Polski</b>,
   * which the platform does not have, and omitted Slovak, Ukrainian and Russian, which it does. This
   * control writes the user's `PreferredLanguageCode`, a column with an FK to `Languages`, so `pl`
   * was not merely a dead option — it was a value the server has no row for.</p>
   *
   * <p>`addLangs` is called with the app's supported set at bootstrap, so `getLangs()` IS that set;
   * the native name comes from `Intl.DisplayNames`, exactly as the shared header switcher does it.
   * Neither needs a list anybody has to remember to update.</p>
   */
  readonly languageOptions = computed<ICleansiaSelectOption[]>(() =>
    this.availableLanguages().map((code) => ({
      label: ProfileComponent.nativeLanguageName(code),
      value: code,
    })),
  );

  /** Re-read on language change so the list survives ngx-translate finishing its own bootstrap. */
  private readonly availableLanguages = signal<string[]>([]);

  private static nativeLanguageName(code: string): string {
    try {
      const name = new Intl.DisplayNames([code], { type: 'language' }).of(code);
      if (name && name !== code) {
        return name.charAt(0).toLocaleUpperCase(code) + name.slice(1);
      }
    } catch {
      // An unknown locale falls through to the code itself, which is still selectable.
    }
    return code.toUpperCase();
  }

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
    // ngx-translate registers the app's supported set at bootstrap; read it once here and again on
    // every language change, because on a cold SSR hydrate `getLangs()` can still be empty when the
    // component first runs.
    this.availableLanguages.set(this.translate.getLangs());
    this.translate.onLangChange
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.availableLanguages.set(this.translate.getLangs()));

    this.loadProfile();
    this.facade.refreshSavedAddresses();
    this.facade.loadCountries();
    if (this.isBrowser) {
      window.addEventListener('scroll', this.onScroll);
    }
    this.passwordForm.controls.newPassword.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(v => this.newPasswordValue.set(v || ''));
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
    this.facade.loadProfile((user) => {
      this.profileForm.patchValue({
        firstName: user.firstName || '',
        lastName: user.lastName || '',
        phoneNumber: user.phoneNumber || '',
        birthDate: user.birthDate ? new Date(user.birthDate) : null,
      });
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) return;

    this.facade.saveProfile(
      {
        firstName: this.profileForm.value.firstName || undefined,
        lastName: this.profileForm.value.lastName || undefined,
        phoneNumber: this.profileForm.value.phoneNumber || undefined,
        birthDate: this.profileForm.value.birthDate || undefined,
        languageCode: this.translate.currentLang,
      },
      () => this.loadProfile(),
    );
  }

  onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (file) {
      void this.facade.uploadAvatar(file);
    }
  }

  removeAvatar(): void {
    this.facade.removeAvatar();
  }

  onAvatarLoaded(): void {
    this.facade.onAvatarLoaded();
  }

  onAvatarLoadFailed(): void {
    this.facade.onAvatarLoadFailed();
  }

  changePassword(): void {
    if (this.passwordForm.invalid || this.passwordMismatch) return;

    const cmd = buildChangePasswordCommand(
      this.user()?.email,
      this.passwordForm.value.newPassword || undefined,
    );

    this.facade.changePassword(cmd, () => this.passwordForm.reset());
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
    this.facade.showAddressSearchFailed();
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
      this.facade.showCoordsRequired();
      return;
    }

    // Backend now requires non-null lat/lng. Bail with the same message if
    // the picker somehow filled the address but skipped coords.
    const lat = this.pickedLatitude();
    const lng = this.pickedLongitude();
    if (lat === undefined || lng === undefined) {
      this.facade.showCoordsRequired();
      return;
    }

    const editingId = this.editingAddressId();
    const fields: SavedAddressFields = {
      label: v.label,
      street: v.street,
      city: v.city,
      zipCode: v.zip,
      countryId: v.country || undefined,
      latitude: lat,
      longitude: lng,
    };

    const ok = editingId
      ? await this.facade.updateSavedAddress(
          buildUpdateSavedAddressCommand(editingId, fields),
        )
      : await this.facade.addSavedAddress(
          buildAddSavedAddressCommand(fields, v.isDefault),
        );

    if (ok) {
      this.closeAddressDialog();
    }
  }

  private defaultCountryId(): string {
    // Pre-select Czech Republic so customers in CZ don't have to pick it every time.
    const cz = this.countryOptions().find((o) =>
      o.label.includes('(CZE)') || o.label.toLowerCase().includes('czech')
    );
    return (cz?.value as string) ?? '';
  }

  async deleteAddress(id: string): Promise<void> {
    await this.facade.deleteSavedAddress(id);
  }

  async setDefaultAddress(id: string): Promise<void> {
    await this.facade.setDefaultSavedAddress(id);
  }

  // Preferences
  get currentLanguage(): string {
    return this.translate.currentLang || 'cs';
  }

  onLanguageChange(lang: string): void {
    this.translate.use(lang);
    if (this.isBrowser) {
      persistPreferredLanguage(lang);
    }
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  /**
   * The board shows light and dark as a segmented pair rather than one button
   * that toggles, so the control has to be able to SET a side rather than flip.
   */
  setTheme(dark: boolean): void {
    if (this.isDarkMode() !== dark) {
      this.themeService.toggleTheme();
    }
  }
}
