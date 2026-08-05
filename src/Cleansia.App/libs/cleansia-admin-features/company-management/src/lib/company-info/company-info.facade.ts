import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  CompanyInfoDetailDto,
  CountryListItem,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  buildUpdateCompanyInfoCommand,
  CompanyInfoFormData,
} from '../company-info.models';

export type { CompanyInfoFormData };

@Injectable()
export class CompanyInfoFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly companyInfo = signal<CompanyInfoDetailDto | null>(null);
  readonly countries = signal<CountryListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadCompanyInfo(): void {
    this.loading.set(true);

    this.adminClient.adminCompanyClient
      .getCurrent()
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

  saveCompanyInfo(data: CompanyInfoFormData): void {
    const currentCompanyInfo = this.companyInfo();
    if (!currentCompanyInfo?.id) {
      console.error('No company info loaded to update');
      return;
    }

    this.saving.set(true);

    const command = buildUpdateCompanyInfoCommand(currentCompanyInfo.id, data);

    this.adminClient.adminCompanyClient
      .update(currentCompanyInfo.id, command)
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
          // Reload to get updated data
          this.loadCompanyInfo();
        }
      });
  }
}
