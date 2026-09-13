import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CountryDetailDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { CountryFormData, CountryFormFacade } from './country-form.facade';

// Two letters in either case; the save uppercases, which is the shape the server insists on.
const ISO_ALPHA2_PATTERN = /^[A-Za-z]{2}$/;

@Component({
  selector: 'cleansia-admin-country-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './country-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CountryFormFacade],
})
export class CountryFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(CountryFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');

  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.country_form.edit_title')
      : this.translate.instant('pages.country_form.create_title')
  );

  // The insurance ceiling lives on the configuration row, which a new country does not have yet.
  readonly marketContentEnabled = computed(
    () => this.isEditMode() && (this.facade.country()?.hasConfiguration ?? false)
  );

  readonly form = this.fb.nonNullable.group({
    isoCode: ['', [Validators.required, Validators.maxLength(3)]],
    isoAlpha2: ['', [Validators.required, Validators.pattern(ISO_ALPHA2_PATTERN)]],
    name: ['', [Validators.required, Validators.maxLength(50)]],
    insuranceCoverageAmount: this.fb.control<number | null>(null, [
      Validators.min(0),
    ]),
  });

  private countryLoadEffect = effect(() => {
    const country = this.facade.country();
    if (country && this.isEditMode()) {
      this.populateForm(country);
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    if (this.isEditMode()) {
      // A country created before the alpha-2 code existed may still carry none; editing its name
      // must not be blocked on it, so the code is only checked when typed and omitted when blank.
      this.form.controls.isoAlpha2.setValidators([
        Validators.pattern(ISO_ALPHA2_PATTERN),
      ]);
      const countryId = this.route.snapshot.paramMap.get('countryId');
      if (countryId) {
        this.facade.loadCountry(countryId);
      } else {
        this.router.navigate([CleansiaAdminRoute.COUNTRY_MANAGEMENT]);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private populateForm(country: CountryDetailDto): void {
    this.form.patchValue({
      isoCode: country.isoCode ?? '',
      isoAlpha2: country.isoAlpha2 ?? '',
      name: country.name ?? '',
      insuranceCoverageAmount: country.insuranceCoverageAmount ?? null,
    });

    this.form.controls.isoCode.disable();
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();

    const data: CountryFormData = {
      isoCode: formValue.isoCode,
      isoAlpha2: formValue.isoAlpha2.trim().toUpperCase(),
      name: formValue.name,
    };

    if (this.isEditMode()) {
      const countryId = this.route.snapshot.paramMap.get('countryId');
      if (countryId) {
        this.facade.updateCountry(
          countryId,
          data,
          this.marketContentEnabled()
            ? {
                insuranceCoverageAmount: this.numberOrNull(
                  formValue.insuranceCoverageAmount
                ),
              }
            : null
        );
      }
    } else {
      this.facade.createCountry(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }

  // A cleared numeric text input arrives as the empty string at runtime, which is neither
  // null nor a number the server would parse.
  private numberOrNull(raw: number | null): number | null {
    if (raw === null || (raw as unknown) === '') {
      return null;
    }
    const parsed = Number(raw);
    return Number.isNaN(parsed) ? null : parsed;
  }
}
