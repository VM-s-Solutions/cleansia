import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTextareaComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CompanyInfoFormData, CompanyInfoFacade } from './company-info.facade';

@Component({
  selector: 'cleansia-admin-company-info',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
    CleansiaSelectComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './company-info.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CompanyInfoFacade],
})
export class CompanyInfoComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(CompanyInfoFacade);

  readonly pageTitle = this.translate.instant('pages.company_info.title');

  readonly form = this.fb.nonNullable.group({
    legalName: ['', [Validators.required, Validators.maxLength(200)]],
    tradingName: ['', [Validators.required, Validators.maxLength(200)]],
    tagline: ['', [Validators.maxLength(500)]],
    registrationNumber: ['', [Validators.required, Validators.maxLength(50)]],
    vatNumber: ['', [Validators.maxLength(50)]],
    street: ['', [Validators.required, Validators.maxLength(100)]],
    city: ['', [Validators.required, Validators.maxLength(100)]],
    zipCode: ['', [Validators.required, Validators.maxLength(20)]],
    countryId: ['', [Validators.required]],
    phone: ['', [Validators.maxLength(50)]],
    email: ['', [Validators.maxLength(100), Validators.email]],
    website: ['', [Validators.maxLength(200)]],
    bankName: ['', [Validators.maxLength(100)]],
    bankAccountNumber: ['', [Validators.maxLength(50)]],
    iban: ['', [Validators.maxLength(50)]],
    swift: ['', [Validators.maxLength(20)]],
  });

  readonly countryOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade
      .countries()
      .filter((c) => c.id)
      .map((c) => ({
        value: c.id!,
        label: c.name ?? c.isoCode ?? '',
      }))
  );

  private companyInfoLoadEffect = effect(() => {
    const companyInfo = this.facade.companyInfo();
    if (companyInfo) {
      this.populateForm(companyInfo);
    }
  });

  ngOnInit(): void {
    this.facade.loadCountries();
    this.facade.loadCompanyInfo();
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private populateForm(companyInfo: {
    legalName?: string;
    tradingName?: string;
    tagline?: string | null;
    registrationNumber?: string;
    vatNumber?: string | null;
    street?: string;
    city?: string;
    zipCode?: string;
    countryId?: string;
    phone?: string | null;
    email?: string | null;
    website?: string | null;
    bankName?: string | null;
    bankAccountNumber?: string | null;
    iban?: string | null;
    swift?: string | null;
  }): void {
    this.form.patchValue({
      legalName: companyInfo.legalName ?? '',
      tradingName: companyInfo.tradingName ?? '',
      tagline: companyInfo.tagline ?? '',
      registrationNumber: companyInfo.registrationNumber ?? '',
      vatNumber: companyInfo.vatNumber ?? '',
      street: companyInfo.street ?? '',
      city: companyInfo.city ?? '',
      zipCode: companyInfo.zipCode ?? '',
      countryId: companyInfo.countryId ?? '',
      phone: companyInfo.phone ?? '',
      email: companyInfo.email ?? '',
      website: companyInfo.website ?? '',
      bankName: companyInfo.bankName ?? '',
      bankAccountNumber: companyInfo.bankAccountNumber ?? '',
      iban: companyInfo.iban ?? '',
      swift: companyInfo.swift ?? '',
    });
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();

    const data: CompanyInfoFormData = {
      legalName: formValue.legalName,
      tradingName: formValue.tradingName,
      tagline: formValue.tagline || null,
      registrationNumber: formValue.registrationNumber,
      vatNumber: formValue.vatNumber || null,
      street: formValue.street,
      city: formValue.city,
      zipCode: formValue.zipCode,
      countryId: formValue.countryId,
      phone: formValue.phone || null,
      email: formValue.email || null,
      website: formValue.website || null,
      bankName: formValue.bankName || null,
      bankAccountNumber: formValue.bankAccountNumber || null,
      iban: formValue.iban || null,
      swift: formValue.swift || null,
    };

    this.facade.saveCompanyInfo(data);
  }
}
