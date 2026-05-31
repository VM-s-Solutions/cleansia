import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { CountryListItem, ServiceCityDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { SelectModule } from 'primeng/select';
import { TabsModule } from 'primeng/tabs';
import { InputTextModule } from 'primeng/inputtext';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ServiceAreaManagementFacade } from './service-area-management.facade';

interface CountryOption {
  label: string;
  value: string;
}

@Component({
  selector: 'cleansia-admin-service-area-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
    TabsModule,
    ToggleSwitchModule,
    SelectModule,
    InputTextModule,
    DialogModule,
    ConfirmDialogModule,
  ],
  templateUrl: './service-area-management.component.html',
  styleUrl: './service-area-management.component.scss',
  providers: [ServiceAreaManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceAreaManagementComponent implements OnInit {
  protected readonly facade = inject(ServiceAreaManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  /** Tab index — 0 = Countries, 1 = Cities. */
  readonly activeTabIndex = signal(0);

  /** Selected country in the Cities tab. Drives the city table filter. */
  readonly selectedCountryId = signal<string | null>(null);

  /** Dialog state for create/edit city. */
  readonly cityDialogOpen = signal(false);
  readonly cityEditTarget = signal<ServiceCityDto | null>(null);
  readonly cityForm = signal({ name: '', zipPrefix: '', isActive: true });

  /** Cities filtered to the currently selected country (UI-side filter to
   *  avoid an extra HTTP roundtrip when admin switches between countries
   *  with the same dataset already loaded). */
  readonly visibleCities = computed(() => {
    const selected = this.selectedCountryId();
    const all = this.facade.cities();
    return selected ? all.filter((c) => c.countryId === selected) : all;
  });

  /** Country options for the Cities-tab dropdown. Only countries with
   *  IsServiced=true are eligible — there's no point managing cities for
   *  a country we don't operate in. */
  readonly servicedCountryOptions = computed<CountryOption[]>(() => {
    const ids = this.facade.servicedCountryIds();
    return this.facade
      .countries()
      .filter((c) => c.id && ids.has(c.id))
      .map((c) => ({
        label: c.name ?? c.isoCode ?? '',
        value: c.id ?? '',
      }));
  });

  constructor() {
    // When the user toggles a country to serviced for the first time, the
    // Cities tab dropdown gains a new option — but only if the user is on
    // that tab. Pre-select the first eligible country once it appears so
    // the cities table populates without a manual select.
    effect(() => {
      const opts = this.servicedCountryOptions();
      if (opts.length === 0) {
        this.selectedCountryId.set(null);
      } else if (
        !opts.find((o) => o.value === this.selectedCountryId())
      ) {
        this.selectedCountryId.set(opts[0].value);
      }
    });
  }

  ngOnInit(): void {
    this.facade.loadCountries();
    this.facade.loadCities();
  }

  onTabChange(index: number | string): void {
    const numIndex = typeof index === 'string' ? parseInt(index, 10) : index;
    this.activeTabIndex.set(numIndex);
  }

  onToggleServiced(country: CountryListItem, isServiced: boolean): void {
    if (!country.id) return;
    this.facade.setCountryServiced(country.id, isServiced);
  }

  isCountryServiced(country: CountryListItem): boolean {
    return !!country.id && this.facade.servicedCountryIds().has(country.id);
  }

  onCountryFilterChange(countryId: string | null): void {
    this.selectedCountryId.set(countryId);
  }

  /**
   * Template-friendly patch helper — Angular templates can't use the
   * spread operator (`{ ...cityForm() }`), so the form fields call this
   * via `(ngModelChange)`. Keeping the signal+method instead of a
   * FormGroup since the dialog only has 3 fields.
   */
  updateCityFormField<K extends keyof { name: string; zipPrefix: string; isActive: boolean }>(
    key: K,
    value: { name: string; zipPrefix: string; isActive: boolean }[K]
  ): void {
    this.cityForm.set({ ...this.cityForm(), [key]: value });
  }

  openCreateCityDialog(): void {
    this.cityEditTarget.set(null);
    this.cityForm.set({ name: '', zipPrefix: '', isActive: true });
    this.cityDialogOpen.set(true);
  }

  openEditCityDialog(city: ServiceCityDto): void {
    this.cityEditTarget.set(city);
    this.cityForm.set({
      name: city.name ?? '',
      zipPrefix: city.zipPrefix ?? '',
      isActive: city.isActive,
    });
    this.cityDialogOpen.set(true);
  }

  saveCity(): void {
    const target = this.cityEditTarget();
    const form = this.cityForm();
    const trimmedName = form.name.trim();
    const zipPrefix = form.zipPrefix.trim() || null;
    if (!trimmedName) return;

    if (target?.id) {
      this.facade.updateCity(
        target.id,
        trimmedName,
        zipPrefix,
        form.isActive,
        this.selectedCountryId() ?? undefined
      );
    } else {
      const countryId = this.selectedCountryId();
      if (!countryId) return;
      this.facade.createCity(countryId, trimmedName, zipPrefix);
    }
    this.cityDialogOpen.set(false);
  }

  confirmDeleteCity(city: ServiceCityDto): void {
    if (!city.id) return;
    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.service_area_management.cities.delete_confirm',
        { name: city.name }
      ),
      header: this.translate.instant(
        'pages.service_area_management.cities.delete_header'
      ),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        if (city.id) {
          this.facade.deleteCity(
            city.id,
            this.selectedCountryId() ?? undefined
          );
        }
      },
    });
  }
}
