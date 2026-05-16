import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCountryCommand,
  CountryDetailDto,
  UpdateCountryCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface CountryFormData {
  isoCode: string;
  name: string;
}

@Injectable()
export class CountryFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly country = signal<CountryDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadCountry(countryId: string): void {
    this.loading.set(true);

    this.adminClient.adminCountryClient
      .details(countryId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((country) => {
        if (country) {
          this.country.set(country);
        }
      });
  }

  createCountry(data: CountryFormData): void {
    this.saving.set(true);

    const command = new CreateCountryCommand({
      isoCode: data.isoCode,
      name: data.name,
    });

    this.adminClient.adminCountryClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.country_form.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
        }
      });
  }

  updateCountry(countryId: string, data: CountryFormData): void {
    this.saving.set(true);

    const command = new UpdateCountryCommand({
      countryId: countryId,
      name: data.name,
    });

    this.adminClient.adminCountryClient
      .update(countryId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.country_form.messages.update_success')
          );
          this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
  }
}