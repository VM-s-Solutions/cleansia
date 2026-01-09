import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  CompanyInfoDetailDto,
  CountryListItem,
  UpdateCompanyInfoCommand,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface CompanyInfoFormData {
  legalName: string;
  tradingName: string;
  tagline: string | null;
  registrationNumber: string;
  vatNumber: string | null;
  street: string;
  city: string;
  zipCode: string;
  countryId: string;
  phone: string | null;
  email: string | null;
  website: string | null;
  bankName: string | null;
  bankAccountNumber: string | null;
  iban: string | null;
  swift: string | null;
}

@Injectable()
export class CompanyInfoFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly companyInfo = signal<CompanyInfoDetailDto | null>(null);
  readonly countries = signal<CountryListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadCompanyInfo(): void {
    this.loading.set(true);

    this.adminClient.apiClient
      .adminCompanyGet()
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.company_info.messages.load_error')
          );
          console.error('Error loading company info:', error);
          return of(null);
        }),
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
        takeUntil(this.destroy$),
        catchError((error) => {
          console.error('Error loading countries:', error);
          return of([]);
        })
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

    const command = new UpdateCompanyInfoCommand({
      companyInfoId: currentCompanyInfo.id,
      legalName: data.legalName,
      tradingName: data.tradingName,
      tagline: data.tagline ?? undefined,
      registrationNumber: data.registrationNumber,
      vatNumber: data.vatNumber ?? undefined,
      street: data.street,
      city: data.city,
      zipCode: data.zipCode,
      countryId: data.countryId,
      phone: data.phone ?? undefined,
      email: data.email ?? undefined,
      website: data.website ?? undefined,
      bankName: data.bankName ?? undefined,
      bankAccountNumber: data.bankAccountNumber ?? undefined,
      iban: data.iban ?? undefined,
      swift: data.swift ?? undefined,
    });

    this.adminClient.apiClient
      .adminCompanyPut(currentCompanyInfo.id, command)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant('pages.company_info.messages.save_error')
          );
          console.error('Error saving company info:', error);
          return of(null);
        }),
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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
