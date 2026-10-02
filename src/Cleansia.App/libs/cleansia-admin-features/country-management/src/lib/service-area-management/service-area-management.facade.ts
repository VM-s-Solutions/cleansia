import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminCountryControllerSetCountryServicedRequest,
  CreateServiceCityCommand,
  ServiceAreaCountryDto,
  ServiceCityDto,
  UpdateServiceCityCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { DialogService, SnackbarService } from '@cleansia/services';
import { catchError, filter, of, takeUntil } from 'rxjs';

/**
 * Facade for the admin "Service area" management page. Two concerns
 * sharing one page:
 *  1. Country.IsServiced toggle — drives every customer/partner-facing
 *     country picker via /Country/GetServiced.
 *  2. ServiceCity CRUD — customer-only city allow-list per serviced
 *     country. Backend's CreateOrder rejects bookings whose address city
 *     doesn't match a row here.
 *
 * Both share a facade because they're conceptually one setting (where do
 * we operate), even though they hit different endpoints.
 */
@Injectable()
export class ServiceAreaManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);

  readonly countries = signal<ServiceAreaCountryDto[]>([]);
  readonly cities = signal<ServiceCityDto[]>([]);
  readonly servicedCountryIds = signal<Set<string>>(new Set());
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  loadCountries(): void {
    this.loading.set(true);
    this.adminClient.adminCountryClient
      .serviceAreaOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((countries) => {
        if (countries) {
          this.countries.set(countries);
          this.servicedCountryIds.set(
            new Set(
              countries
                .filter((c) => c.isServiced)
                .map((c) => c.id)
                .filter((id): id is string => !!id)
            )
          );
        }
        this.loading.set(false);
        this.initialLoading.set(false);
      });
  }

  // The switch is bound one-way to the serviced set, so the set flips before the server answers
  // and flips back on a refusal (`country.market_not_ready`); that is what pulls the control back.
  setCountryServiced(countryId: string, isServiced: boolean): void {
    const before = this.servicedCountryIds();
    this.servicedCountryIds.set(this.withServiced(before, countryId, isServiced));

    const body = new AdminCountryControllerSetCountryServicedRequest();
    body.isServiced = isServiced;
    this.adminClient.adminCountryClient
      .serviced(countryId, body)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (!response) {
          this.servicedCountryIds.set(before);
          return;
        }
        this.servicedCountryIds.set(this.withServiced(before, countryId, !!response.isServiced));
        this.snackbarService.showSuccessTranslated(
          'pages.service_area_management.messages.country_updated'
        );
      });
  }

  private withServiced(ids: Set<string>, countryId: string, isServiced: boolean): Set<string> {
    const next = new Set(ids);
    if (isServiced) next.add(countryId);
    else next.delete(countryId);
    return next;
  }

  loadCities(countryId?: string): void {
    // ServiceCity CRUD hangs off the catch-all `ApiClient` (no dedicated
    // controller in the generated wrapper) — see admin-base-client.ts.
    this.adminClient.apiClient
      .adminServiceCityGet(countryId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as ServiceCityDto[]))
      )
      // The generated client answers a 200 whose body is not a JSON array, and a 204, with NULL
      // although its type promises an array. → service-management/service-form.facade.ts
      .subscribe((cities) => this.cities.set(cities ?? []));
  }

  createCity(countryId: string, name: string, zipPrefix: string | null): void {
    const command = new CreateServiceCityCommand();
    command.countryId = countryId;
    command.name = name;
    command.zipPrefix = zipPrefix ?? undefined;
    this.adminClient.apiClient
      .adminServiceCityPost(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (!response) return;
        this.snackbarService.showSuccessTranslated(
          'pages.service_area_management.messages.city_created'
        );
        this.loadCities(countryId);
      });
  }

  updateCity(
    id: string,
    name: string,
    zipPrefix: string | null,
    isActive: boolean,
    refreshCountryId?: string
  ): void {
    const command = new UpdateServiceCityCommand();
    command.id = id;
    command.name = name;
    command.zipPrefix = zipPrefix ?? undefined;
    command.isActive = isActive;
    this.adminClient.apiClient
      .adminServiceCityPut(id, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (!response) return;
        this.snackbarService.showSuccessTranslated(
          'pages.service_area_management.messages.city_updated'
        );
        this.loadCities(refreshCountryId);
      });
  }

  deleteCity(city: ServiceCityDto, refreshCountryId?: string): void {
    const id = city.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.service_area_management.cities.delete_confirm',
        'pages.service_area_management.cities.delete_header',
        { name: city.name },
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deleteCityConfirmed(id, refreshCountryId));
  }

  private deleteCityConfirmed(id: string, refreshCountryId?: string): void {
    this.adminClient.apiClient
      .adminServiceCityDelete(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (!response) return;
        this.snackbarService.showSuccessTranslated(
          'pages.service_area_management.messages.city_deleted'
        );
        this.loadCities(refreshCountryId);
      });
  }
}
