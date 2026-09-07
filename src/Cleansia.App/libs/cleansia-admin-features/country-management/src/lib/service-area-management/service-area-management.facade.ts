import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  AdminCountryControllerSetCountryServicedRequest,
  CountryDetailDto,
  CountryListItem,
  CreateServiceCityCommand,
  ServiceCityDto,
  UpdateServiceCityCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, forkJoin, of, takeUntil } from 'rxjs';

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
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly countries = signal<CountryListItem[]>([]);
  readonly cities = signal<ServiceCityDto[]>([]);
  readonly servicedCountryIds = signal<Set<string>>(new Set());
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  /**
   * Loads the full country catalog AND the per-country IsServiced flag
   * (which lives on CountryDetailDto, not CountryListItem). One detail
   * call per country in parallel via forkJoin — acceptable for the v1
   * catalog size (~50 countries). If it grows past a few hundred, swap to
   * a dedicated `/admin-countries/serviced-ids` endpoint.
   */
  loadCountries(): void {
    this.loading.set(true);
    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as CountryListItem[]))
      )
      .subscribe((countries) => {
        // `?? []` because the generated client returns NULL, not an empty list, for a 200 whose body
        // is not a JSON array and for a 204, while its declared type promises an array — neither
        // `catchError` nor the compiler can see it. Reasoned out in full in
        // service-management/service-form.facade.ts. Coalesced ONCE into a local so the signal and
        // the `.map` below read the same list; the `.map` is what would throw.
        const list = countries ?? [];
        this.countries.set(list);
        const ids = list.map((c) => c.id).filter((id): id is string => !!id);
        if (ids.length === 0) {
          this.servicedCountryIds.set(new Set());
          this.loading.set(false);
          this.initialLoading.set(false);
          return;
        }
        const detailCalls = ids.map((id) =>
          this.adminClient.adminCountryClient
            .details(id)
            .pipe(catchError(() => of(null)))
        );
        forkJoin(detailCalls)
          .pipe(takeUntil(this.destroyed$))
          .subscribe((details) => {
            const next = new Set<string>();
            details.forEach((d: CountryDetailDto | null) => {
              if (d?.id && d.isServiced) next.add(d.id);
            });
            this.servicedCountryIds.set(next);
            this.loading.set(false);
            this.initialLoading.set(false);
          });
      });
  }

  setCountryServiced(countryId: string, isServiced: boolean): void {
    const body = new AdminCountryControllerSetCountryServicedRequest();
    body.isServiced = isServiced;
    this.adminClient.adminCountryClient
      .serviced(countryId, body)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (!response) return;
        const next = new Set(this.servicedCountryIds());
        if (response.isServiced) next.add(countryId);
        else next.delete(countryId);
        this.servicedCountryIds.set(next);
        this.snackbarService.showSuccess(
          this.translate.instant(
            'pages.service_area_management.messages.country_updated'
          )
        );
      });
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
      // Same generated-client null as `loadCountries` above. Nothing dereferences it here, so the
      // null would sit in the signal until the city table iterates it.
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
        this.snackbarService.showSuccess(
          this.translate.instant(
            'pages.service_area_management.messages.city_created'
          )
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
        this.snackbarService.showSuccess(
          this.translate.instant(
            'pages.service_area_management.messages.city_updated'
          )
        );
        this.loadCities(refreshCountryId);
      });
  }

  deleteCity(id: string, refreshCountryId?: string): void {
    this.adminClient.apiClient
      .adminServiceCityDelete(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (!response) return;
        this.snackbarService.showSuccess(
          this.translate.instant(
            'pages.service_area_management.messages.city_deleted'
          )
        );
        this.loadCities(refreshCountryId);
      });
  }
}
