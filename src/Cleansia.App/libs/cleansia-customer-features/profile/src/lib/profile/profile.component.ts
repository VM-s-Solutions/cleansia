import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, OnDestroy, PLATFORM_ID, signal, effect, AfterViewInit } from '@angular/core';
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
  CleansiaButtonComponent,
  CleansiaTextInputComponent,
  CleansiaTelephoneComponent,
  CleansiaCalendarComponent,
  CleansiaSelectComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { CustomerClient } from '@cleansia/customer-services';
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

export interface SavedAddress {
  id: string;
  street: string;
  city: string;
  zip: string;
  country: string;
  isDefault: boolean;
}

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
  ],
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit, OnDestroy, AfterViewInit {
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);
  private readonly themeService = inject(ThemeService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  readonly router = inject(Router);

  user = signal<UserListItem | null>(null);
  loading = signal(true);
  saving = signal(false);

  // Section navigation
  activeSection = signal('personal');
  sections = [
    { id: 'personal', icon: 'pi pi-user', labelKey: 'pages.profile.personal_info' },
    { id: 'security', icon: 'pi pi-lock', labelKey: 'pages.profile.security_title' },
    { id: 'addresses', icon: 'pi pi-map-marker', labelKey: 'pages.profile.addresses_title' },
    { id: 'preferences', icon: 'pi pi-sliders-h', labelKey: 'pages.profile.preferences_title' },
    { id: 'danger', icon: 'pi pi-exclamation-triangle', labelKey: 'pages.profile.danger_zone_title' },
  ];

  // Addresses (localStorage-based until backend support)
  addresses = signal<SavedAddress[]>([]);
  showAddressDialog = signal(false);
  editingAddress = signal<SavedAddress | null>(null);
  countryOptions = signal<ICleansiaSelectOption[]>([]);

  addressForm = new FormGroup({
    street: new FormControl<string>('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(255),
      ],
    }),
    city: new FormControl<string>('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(100),
      ],
    }),
    zip: new FormControl<string>('', {
      nonNullable: true,
      validators: [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(20),
      ],
    }),
    country: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    isDefault: new FormControl<boolean>(false, { nonNullable: true }),
  });

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
    this.loadAddresses();
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

    const options: IntersectionObserverInit = {
      rootMargin: '-20% 0px -60% 0px',
      threshold: 0,
    };

    this.scrollObserver = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          const id = entry.target.id.replace('profile-', '');
          this.activeSection.set(id);
        }
      }
    }, options);

    for (const section of this.sections) {
      if (!this.isBrowser) continue;
      const el = document.getElementById(`profile-${section.id}`);
      if (el) {
        this.scrollObserver.observe(el);
      }
    }
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

  // Address management (localStorage-based — TODO: backend integration)
  private readonly STORAGE_KEY = 'cleansia_saved_addresses';

  loadAddresses(): void {
    if (!this.isBrowser) return;
    try {
      const stored = localStorage.getItem(this.STORAGE_KEY);
      if (stored) {
        this.addresses.set(JSON.parse(stored));
      }
    } catch {
      this.addresses.set([]);
    }
  }

  private saveAddresses(): void {
    if (!this.isBrowser) return;
    localStorage.setItem(this.STORAGE_KEY, JSON.stringify(this.addresses()));
  }

  openAddAddress(): void {
    this.editingAddress.set(null);
    this.addressForm.reset({
      street: '',
      city: '',
      zip: '',
      // Default to Czech Republic when available.
      country: this.defaultCountryId(),
      isDefault: false,
    });
    this.showAddressDialog.set(true);
  }

  openEditAddress(address: SavedAddress): void {
    this.editingAddress.set(address);
    this.addressForm.reset({
      street: address.street,
      city: address.city,
      zip: address.zip,
      country: address.country,
      isDefault: address.isDefault,
    });
    this.showAddressDialog.set(true);
  }

  saveAddress(): void {
    if (this.addressForm.invalid) {
      this.addressForm.markAllAsTouched();
      return;
    }

    const value = this.addressForm.getRawValue();
    const current = [...this.addresses()];
    const editing = this.editingAddress();

    if (value.isDefault) {
      current.forEach((a) => (a.isDefault = false));
    }

    if (editing) {
      const idx = current.findIndex((a) => a.id === editing.id);
      if (idx !== -1) {
        current[idx] = { ...value, id: editing.id };
      }
    } else {
      current.push({
        ...value,
        id: crypto.randomUUID(),
      });
    }

    this.addresses.set(current);
    this.saveAddresses();
    this.showAddressDialog.set(false);
    this.snackbar.showSuccess(this.translate.instant('pages.profile.address_saved'));
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

  deleteAddress(id: string): void {
    this.addresses.update(list => list.filter(a => a.id !== id));
    this.saveAddresses();
    this.snackbar.showSuccess(this.translate.instant('pages.profile.address_deleted'));
  }

  setDefaultAddress(id: string): void {
    this.addresses.update(list =>
      list.map(a => ({ ...a, isDefault: a.id === id }))
    );
    this.saveAddresses();
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
