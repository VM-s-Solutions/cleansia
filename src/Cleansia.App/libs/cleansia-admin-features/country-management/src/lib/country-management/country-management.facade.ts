import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, CountryListItem } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class CountryManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly router = inject(Router);

  readonly countries = signal<CountryListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  loadCountries(): void {
    this.loading.set(true);

    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([])),
        finalize(() => this.loading.set(false))
      )
      .subscribe((countries) => {
        // `?? []` — the generated client can put a NULL in a signal typed as an array; reasoned out
        // in service-management/service-form.facade.ts. Nothing dereferences it here, so the null
        // would travel as far as the country table before it threw.
        this.countries.set(countries ?? []);
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  navigateToCreateCountry(): void {
    this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT, 'create']);
  }

  navigateToEditCountry(country: CountryListItem): void {
    if (country.id) {
      this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT, country.id, 'edit']);
    }
  }

  setDefaultMarket(country: CountryListItem): void {
    const id = country.id;
    if (!id || country.isDefaultMarket) return;

    this.dialog
      .confirmTranslated(
        'pages.country_management.set_default_market_confirm',
        'pages.country_management.set_default_market',
        { name: country.name },
        { icon: 'pi pi-star' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.setDefaultMarketConfirmed(id));
  }

  private setDefaultMarketConfirmed(id: string): void {
    this.adminClient.adminCountryClient
      .defaultMarket(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.country_management.messages.set_default_market_success'
          );
          this.loadCountries();
        }
      });
  }

  deleteCountry(country: CountryListItem): void {
    const id = country.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.country_management.delete_confirm',
        'pages.country_management.delete_country',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deleteCountryConfirmed(id));
  }

  private deleteCountryConfirmed(id: string): void {
    this.adminClient.adminCountryClient
      .delete(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.country_management.messages.delete_success'
          );
          this.loadCountries();
        }
      });
  }
}
