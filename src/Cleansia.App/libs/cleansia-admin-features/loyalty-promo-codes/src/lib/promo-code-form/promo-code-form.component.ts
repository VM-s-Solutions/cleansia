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
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PromoCodeType } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { PromoCodeFormFacade } from './promo-code-form.facade';

const CODE_PATTERN = /^[A-Z0-9]{3,20}$/;

@Component({
  selector: 'cleansia-admin-promo-code-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTextareaComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './promo-code-form.component.html',
  providers: [PromoCodeFormFacade],
})
export class PromoCodeFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(PromoCodeFormFacade);

  protected readonly PromoCodeType = PromoCodeType;

  private readonly mode = signal<'create' | 'edit'>('create');
  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.promo_codes.form.edit_title')
      : this.translate.instant('pages.promo_codes.form.create_title')
  );

  private promoCodeId: string | null = null;

  // ----------------------------------------------------------
  // Form definition. Discount sub-fields are conditionally validated by
  // toggling Validators on `type` change (see `applyTypeValidation`).
  // The cross-field validity-range constraint lives on the FormGroup.
  // ----------------------------------------------------------
  readonly form = this.fb.group(
    {
      code: this.fb.control<string>('', {
        nonNullable: true,
        validators: [Validators.required, Validators.pattern(CODE_PATTERN)],
      }),
      type: this.fb.control<PromoCodeType>(PromoCodeType.PercentDiscount, {
        nonNullable: true,
      }),
      discountPercentUi: this.fb.control<number | null>(null, {
        validators: [Validators.min(1), Validators.max(100)],
      }),
      discountAmount: this.fb.control<number | null>(null, {
        validators: [Validators.min(0.01)],
      }),
      currencyId: this.fb.control<string | null>(null),
      minimumOrderAmount: this.fb.control<number | null>(null, {
        validators: [Validators.min(0)],
      }),
      maxRedemptionsPerUser: this.fb.control<number>(1, {
        nonNullable: true,
        validators: [Validators.required, Validators.min(1)],
      }),
      globalMaxEnabled: this.fb.control<boolean>(false, {
        nonNullable: true,
      }),
      globalMaxRedemptions: this.fb.control<number | null>(null, {
        validators: [Validators.min(1)],
      }),
      validFrom: this.fb.control<Date | null>(null),
      validUntil: this.fb.control<Date | null>(null),
      description: this.fb.control<string>('', {
        nonNullable: true,
        validators: [Validators.maxLength(500)],
      }),
      isActive: this.fb.control<boolean>(true, { nonNullable: true }),
    },
    { validators: [validityRangeValidator] }
  );

  readonly currencyOptions = computed(() =>
    this.facade.currencies().map((c) => ({
      label: c.code,
      value: c.id,
    }))
  );

  readonly typeOptions = computed(() => [
    {
      label: this.translate.instant('pages.promo_codes.type.percent'),
      value: PromoCodeType.PercentDiscount,
    },
    {
      label: this.translate.instant('pages.promo_codes.type.fixed'),
      value: PromoCodeType.FixedDiscount,
    },
  ]);

  readonly isPercent = signal<boolean>(true);

  // Detail data load -> populate form once available (edit mode only).
  private populateEffect = effect(() => {
    const detail = this.facade.promoCode();
    if (detail && this.isEditMode()) {
      this.populateFormFromDetail(detail);
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as
      | 'create'
      | 'edit'
      | undefined;
    if (routeMode) {
      this.mode.set(routeMode);
    }

    this.facade.loadCurrencies();

    // Apply initial type validation, then delegate ongoing form reactivity
    // (type-validator switching, code uppercasing, global-max toggle) to
    // the facade.
    this.applyTypeValidation(this.form.controls.type.value);
    this.facade.bindFormReactivity(this.form, (t) =>
      this.applyTypeValidation(t)
    );

    if (this.isEditMode()) {
      const id = this.route.snapshot.paramMap.get('id');
      if (id) {
        this.promoCodeId = id;
        this.facade.loadPromoCode(id);
        this.disableImmutableFields();
      } else {
        this.router.navigate(['/loyalty/promos']);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  /** Switch validators when type changes; clears the now-irrelevant value. */
  private applyTypeValidation(type: PromoCodeType): void {
    const isPct = type === PromoCodeType.PercentDiscount;
    this.isPercent.set(isPct);
    const pct = this.form.controls.discountPercentUi;
    const amt = this.form.controls.discountAmount;
    const cur = this.form.controls.currencyId;

    pct.clearValidators();
    amt.clearValidators();
    cur.clearValidators();

    if (isPct) {
      pct.addValidators([
        Validators.required,
        Validators.min(1),
        Validators.max(100),
      ]);
      amt.setValue(null, { emitEvent: false });
      cur.setValue(null, { emitEvent: false });
    } else {
      amt.addValidators([Validators.required, Validators.min(0.01)]);
      cur.addValidators([Validators.required]);
      pct.setValue(null, { emitEvent: false });
    }

    pct.updateValueAndValidity({ emitEvent: false });
    amt.updateValueAndValidity({ emitEvent: false });
    cur.updateValueAndValidity({ emitEvent: false });
  }

  /** In edit mode the immutable fields are read-only display. */
  private disableImmutableFields(): void {
    this.form.controls.code.disable({ emitEvent: false });
    this.form.controls.type.disable({ emitEvent: false });
    this.form.controls.discountPercentUi.disable({ emitEvent: false });
    this.form.controls.discountAmount.disable({ emitEvent: false });
    this.form.controls.currencyId.disable({ emitEvent: false });
  }

  private populateFormFromDetail(detail: {
    code?: string;
    type?: PromoCodeType;
    discountPercent?: number;
    discountAmount?: number;
    currencyId?: string;
    minimumOrderAmount?: number;
    maxRedemptionsPerUser?: number;
    globalMaxRedemptions?: number;
    validFrom?: Date;
    validUntil?: Date;
    description?: string;
    isActive?: boolean;
  }): void {
    const isPct = detail.type === PromoCodeType.PercentDiscount;
    this.applyTypeValidation(detail.type ?? PromoCodeType.PercentDiscount);

    this.form.patchValue({
      code: detail.code ?? '',
      type: detail.type ?? PromoCodeType.PercentDiscount,
      discountPercentUi:
        isPct && detail.discountPercent != null
          ? Math.round(detail.discountPercent * 100)
          : null,
      discountAmount: !isPct ? detail.discountAmount ?? null : null,
      currencyId: detail.currencyId ?? null,
      minimumOrderAmount: detail.minimumOrderAmount ?? null,
      maxRedemptionsPerUser: detail.maxRedemptionsPerUser ?? 1,
      globalMaxEnabled: detail.globalMaxRedemptions != null,
      globalMaxRedemptions: detail.globalMaxRedemptions ?? null,
      validFrom: detail.validFrom ?? null,
      validUntil: detail.validUntil ?? null,
      description: detail.description ?? '',
      isActive: detail.isActive ?? true,
    });
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();

    if (this.isEditMode() && this.promoCodeId) {
      this.facade.update(this.promoCodeId, {
        isActive: v.isActive,
        validFrom: v.validFrom ?? undefined,
        validUntil: v.validUntil ?? undefined,
        minimumOrderAmount: v.minimumOrderAmount ?? undefined,
        maxRedemptionsPerUser: v.maxRedemptionsPerUser,
        globalMaxRedemptions:
          v.globalMaxEnabled && v.globalMaxRedemptions != null
            ? v.globalMaxRedemptions
            : undefined,
        description: v.description?.trim() || undefined,
      });
    } else {
      this.facade.create({
        code: v.code.toUpperCase(),
        type: v.type,
        discountPercentUi:
          v.type === PromoCodeType.PercentDiscount
            ? v.discountPercentUi ?? undefined
            : undefined,
        discountAmount:
          v.type === PromoCodeType.FixedDiscount
            ? v.discountAmount ?? undefined
            : undefined,
        currencyId:
          v.type === PromoCodeType.FixedDiscount
            ? v.currencyId ?? undefined
            : undefined,
        minimumOrderAmount: v.minimumOrderAmount ?? undefined,
        maxRedemptionsPerUser: v.maxRedemptionsPerUser,
        globalMaxRedemptions:
          v.globalMaxEnabled && v.globalMaxRedemptions != null
            ? v.globalMaxRedemptions
            : undefined,
        validFrom: v.validFrom ?? undefined,
        validUntil: v.validUntil ?? undefined,
        description: v.description?.trim() || undefined,
      });
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}

/** Cross-field validator: validFrom must be <= validUntil when both set. */
function validityRangeValidator(
  group: AbstractControl
): ValidationErrors | null {
  const from = group.get('validFrom')?.value as Date | null;
  const until = group.get('validUntil')?.value as Date | null;
  if (from && until && from.getTime() > until.getTime()) {
    return { validityRange: true };
  }
  return null;
}
