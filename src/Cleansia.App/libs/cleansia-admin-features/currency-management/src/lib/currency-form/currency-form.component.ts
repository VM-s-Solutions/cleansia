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
import { CurrencyFormData, CurrencyFormFacade } from './currency-form.facade';

@Component({
  selector: 'cleansia-admin-currency-form',
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
  templateUrl: './currency-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [CurrencyFormFacade],
})
export class CurrencyFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(CurrencyFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');

  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.currency_form.edit_title')
      : this.translate.instant('pages.currency_form.create_title')
  );

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(3)]],
    symbol: ['', [Validators.required, Validators.maxLength(5)]],
    name: ['', [Validators.required, Validators.maxLength(50)]],
    loyaltyPointsDivisor: this.fb.control<number | null>(null, [
      Validators.min(0.01),
    ]),
  });

  private currencyLoadEffect = effect(() => {
    const currency = this.facade.currency();
    if (currency && this.isEditMode()) {
      this.populateForm(currency);
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    if (this.isEditMode()) {
      const currencyId = this.route.snapshot.paramMap.get('currencyId');
      if (currencyId) {
        this.facade.loadCurrency(currencyId);
      } else {
        this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT]);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private populateForm(currency: {
    code?: string;
    symbol?: string;
    name?: string;
    loyaltyPointsDivisor?: number;
  }): void {
    this.form.patchValue({
      code: currency.code ?? '',
      symbol: currency.symbol ?? '',
      name: currency.name ?? '',
      loyaltyPointsDivisor: currency.loyaltyPointsDivisor ?? null,
    });
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();

    const data: CurrencyFormData = {
      code: formValue.code,
      symbol: formValue.symbol,
      name: formValue.name,
      loyaltyPointsDivisor: this.divisorOrNull(formValue.loyaltyPointsDivisor),
    };

    if (this.isEditMode()) {
      const currencyId = this.route.snapshot.paramMap.get('currencyId');
      if (currencyId) {
        this.facade.updateCurrency(currencyId, data);
      }
    } else {
      this.facade.createCurrency(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }

  // A cleared numeric text input arrives as the empty string at runtime, which is neither
  // null nor a number the server would parse.
  private divisorOrNull(raw: number | null): number | null {
    if (raw === null || (raw as unknown) === '') {
      return null;
    }
    const parsed = Number(raw);
    return Number.isNaN(parsed) ? null : parsed;
  }
}