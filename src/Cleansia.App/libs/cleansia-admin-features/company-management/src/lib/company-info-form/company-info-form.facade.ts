import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CompanyInfoDetailDto,
  CountryListItem,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  buildCreateCompanyInfoCommand,
  buildUpdateCompanyInfoCommand,
  CompanyInfoFormData,
} from '../company-info.models';

export type { CompanyInfoFormData };

@Injectable()
export class CompanyInfoFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly companyInfo = signal<CompanyInfoDetailDto | null>(null);
  readonly countries = signal<CountryListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadCompanyInfo(companyInfoId: string): void {
    this.loading.set(true);

    this.adminClient.adminCompanyClient
      .details(companyInfoId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((companyInfo) => {
        this.companyInfo.set(companyInfo);
      });
  }

  loadCountries(): void {
    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([]))
      )
      .subscribe((countries) => {
        this.countries.set(countries);
      });
  }

  createCompanyInfo(data: CompanyInfoFormData): void {
    this.saving.set(true);

    const command = buildCreateCompanyInfoCommand(data);

    this.adminClient.adminCompanyClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.company_info.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.COMPANY_INFO]);
        }
      });
  }

  updateCompanyInfo(companyInfoId: string, data: CompanyInfoFormData): void {
    this.saving.set(true);

    const command = buildUpdateCompanyInfoCommand(companyInfoId, data);

    this.adminClient.adminCompanyClient
      .update(companyInfoId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.company_info.messages.save_success')
          );
          this.router.navigate([CleansiaAdminRoute.COMPANY_INFO]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.COMPANY_INFO]);
  }
}
