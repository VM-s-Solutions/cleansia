import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, CountryListItem } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class CountryManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
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
        this.countries.set(countries);
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

  deleteCountry(country: CountryListItem): void {
    if (!country.id) return;

    this.adminClient.adminCountryClient
      .delete(country.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.country_management.messages.delete_success'
            )
          );
          this.loadCountries();
        }
      });
  }
}
