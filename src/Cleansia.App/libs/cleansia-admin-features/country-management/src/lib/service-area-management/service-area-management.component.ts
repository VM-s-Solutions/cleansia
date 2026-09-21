import { FormsModule } from '@angular/forms';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  TemplateRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { CountryListItem, ServiceCityDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { PermissionService, Policy } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { TabsModule } from 'primeng/tabs';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ServiceAreaManagementFacade } from './service-area-management.facade';
import {
  EMPTY_SERVICE_CITY_FORM,
  getServiceCityTableDefinition,
  getServicedCountryTableColumns,
  ServiceCityForm,
} from './service-area-management.models';

@Component({
  selector: 'cleansia-admin-service-area-management',
  standalone: true,
  imports: [
    CleansiaPermissionDirective,
    FormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    TabsModule,
    ToggleSwitchModule,
    DialogModule,
  ],
  templateUrl: './service-area-management.component.html',
  providers: [ServiceAreaManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceAreaManagementComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  protected readonly facade = inject(ServiceAreaManagementFacade);
  protected readonly Policy = Policy;

  private readonly servicedTemplate = viewChild<TemplateRef<CountryListItem>>('servicedTemplate');
  private readonly activeTemplate = viewChild<TemplateRef<ServiceCityDto>>('activeTemplate');
  private readonly lang = currentLanguage(this.translate);

  /** Tab index — 0 = Countries, 1 = Cities. */
  readonly activeTabIndex = signal(0);

  /** Selected country in the Cities tab. Drives the city table filter. */
  readonly selectedCountryId = signal<string | null>(null);

  readonly cityDialogOpen = signal(false);
  readonly cityEditTarget = signal<ServiceCityDto | null>(null);
  readonly cityForm = signal<ServiceCityForm>(EMPTY_SERVICE_CITY_FORM);

  protected readonly countryColumns = computed(() => {
    this.lang();
    return getServicedCountryTableColumns(this.translate, this.servicedTemplate());
  });

  protected readonly cityTable = computed(() => {
    this.lang();
    return getServiceCityTableDefinition(
      {
        canManage: this.permissions.hasPolicy(Policy.CanManageServiceCities),
        onEdit: (row) => this.openEditCityDialog(row),
        onDelete: (row) => this.confirmDeleteCity(row),
      },
      this.translate,
      this.activeTemplate()
    );
  });

  /** Cities narrowed to the selected country; the whole set is already loaded, so no re-read. */
  readonly visibleCities = computed(() => {
    const selected = this.selectedCountryId();
    const all = this.facade.cities();
    return selected ? all.filter((c) => c.countryId === selected) : all;
  });

  /** Only a serviced country can carry cities, so only those are offered. */
  readonly servicedCountryOptions = computed<ICleansiaSelectOption[]>(() => {
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
    // The first eligible country is pre-selected once it appears, so the city table fills without
    // a manual pick; a country that stops being serviced drops out of the selection with it.
    effect(() => {
      const opts = this.servicedCountryOptions();
      if (opts.length === 0) {
        this.selectedCountryId.set(null);
      } else if (!opts.find((o) => o.value === this.selectedCountryId())) {
        this.selectedCountryId.set(opts[0].value);
      }
    });
  }

  ngOnInit(): void {
    this.facade.loadCountries();
    this.facade.loadCities();
  }

  onTabChange(index: number | string | undefined): void {
    if (index === undefined) return;
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

  updateCityFormField<K extends keyof ServiceCityForm>(key: K, value: ServiceCityForm[K]): void {
    this.cityForm.set({ ...this.cityForm(), [key]: value });
  }

  openCreateCityDialog(): void {
    this.cityEditTarget.set(null);
    this.cityForm.set(EMPTY_SERVICE_CITY_FORM);
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
    this.facade.deleteCity(city, this.selectedCountryId() ?? undefined);
  }
}
