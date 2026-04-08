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

  readonly form = this.fb.nonNullable.group({
    isoCode: ['', [Validators.required, Validators.maxLength(3)]],
    name: ['', [Validators.required, Validators.maxLength(50)]],
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

  private populateForm(country: { isoCode?: string; name?: string }): void {
    this.form.patchValue({
      isoCode: country.isoCode ?? '',
      name: country.name ?? '',
    });

    // Disable isoCode field in edit mode (isoCode should not be editable)
    if (this.isEditMode()) {
      this.form.get('isoCode')?.disable();
    }
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();

    const data: CountryFormData = {
      isoCode: formValue.isoCode,
      name: formValue.name,
    };

    if (this.isEditMode()) {
      const countryId = this.route.snapshot.paramMap.get('countryId');
      if (countryId) {
        this.facade.updateCountry(countryId, data);
      }
    } else {
      this.facade.createCountry(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}