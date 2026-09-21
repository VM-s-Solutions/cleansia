import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCountryCommand,
  CountryDetailDto,
  UpdateCountryCommand,
  UpdateCountryMarketContentCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface CountryFormData {
  isoCode: string;
  isoAlpha2: string;
  name: string;
}

export interface CountryMarketContentData {
  insuranceCoverageAmount: number | null;
}

@Injectable()
export class CountryFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
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

    const command = new CreateCountryCommand();
    command.isoCode = data.isoCode;
    command.isoAlpha2 = data.isoAlpha2;
    command.name = data.name;

    this.adminClient.adminCountryClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated('pages.country_form.messages.create_success');
          this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
        }
      });
  }

  /**
   * The market content rides a second PUT because it lives on the configuration row, not the
   * country; it runs only once the country update has landed, and only when the form had the
   * section to save (a country without a configuration has nothing to write to).
   */
  updateCountry(
    countryId: string,
    data: CountryFormData,
    marketContent: CountryMarketContentData | null
  ): void {
    this.saving.set(true);

    const command = new UpdateCountryCommand();
    command.countryId = countryId;
    command.name = data.name;
    command.isoAlpha2 = data.isoAlpha2.trim() || undefined;

    this.adminClient.adminCountryClient
      .update(countryId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (!response) {
          this.saving.set(false);
          return;
        }
        this.snackbarService.showSuccessTranslated('pages.country_form.messages.update_success');
        if (marketContent) {
          this.updateMarketContent(countryId, marketContent);
        } else {
          this.saving.set(false);
          this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
        }
      });
  }

  updateMarketContent(countryId: string, data: CountryMarketContentData): void {
    this.saving.set(true);

    const command = new UpdateCountryMarketContentCommand();
    command.countryId = countryId;
    command.insuranceCoverageAmount = data.insuranceCoverageAmount ?? undefined;

    this.adminClient.adminCountryClient
      .marketContent(countryId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.country_form.messages.market_content_success'
          );
          this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
  }
}
