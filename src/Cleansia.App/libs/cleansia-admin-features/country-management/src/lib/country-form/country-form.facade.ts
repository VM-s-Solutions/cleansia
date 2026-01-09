import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCountryCommand,
  CountryDetailDto,
  UpdateCountryCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface CountryFormData {
  isoCode: string;
  name: string;
}

@Injectable()
export class CountryFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly country = signal<CountryDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadCountry(countryId: string): void {
    this.loading.set(true);

    this.adminClient.adminCountryClient
      .details(countryId)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.country_form.messages.load_error')
          );
          console.error('Error loading country:', error);
          this.router.navigate(['/country-management']);
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

    this.adminClient.apiClient
      .adminCountryPost(command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.country_form.messages.create_error')
          );
          console.error('Error creating country:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.country_form.messages.create_success')
          );
          this.router.navigate(['/country-management']);
        }
      });
  }

  updateCountry(countryId: string, data: CountryFormData): void {
    this.saving.set(true);

    const command = new UpdateCountryCommand({
      countryId: countryId,
      name: data.name,
    });

    this.adminClient.apiClient
      .adminCountryPut(countryId, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.country_form.messages.update_error')
          );
          console.error('Error updating country:', error);
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.country_form.messages.update_success')
          );
          this.router.navigate(['/country-management']);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/country-management']);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}