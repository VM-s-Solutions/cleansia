import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CompanyInfoDetailDto,
  CountryListItem,
  CreateCompanyInfoCommand,
  UpdateCompanyInfoCommand,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
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
export class CompanyInfoFormFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly companyInfo = signal<CompanyInfoDetailDto | null>(null);
  readonly countries = signal<CountryListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadCompanyInfo(companyInfoId: string): void {
    this.loading.set(true);

    this.adminClient.adminCompanyClient
      .details(companyInfoId)
      .pipe(
        takeUntil(this.destroy$),
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
        takeUntil(this.destroy$),
        catchError(() => of([]))
      )
      .subscribe((countries) => {
        this.countries.set(countries);
      });
  }

  createCompanyInfo(data: CompanyInfoFormData): void {
    this.saving.set(true);

    const command = new CreateCompanyInfoCommand({
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

    this.adminClient.adminCompanyClient
      .create(command)
      .pipe(
        takeUntil(this.destroy$),
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

    const command = new UpdateCompanyInfoCommand({
      companyInfoId: companyInfoId,
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

    this.adminClient.adminCompanyClient
      .update(companyInfoId, command)
      .pipe(
        takeUntil(this.destroy$),
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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
