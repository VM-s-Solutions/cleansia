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
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { MembershipPlanDetailDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { MembershipPlanFormFacade } from './membership-plan-form.facade';
import {
  BILLING_INTERVAL_LABEL_KEYS,
  BILLING_INTERVAL_WIRE,
  BillingIntervalWireValue,
  toBillingIntervalWireValue,
} from '../membership-plan-list/membership-plan-list.models';

const CODE_PATTERN = /^[A-Z0-9_]{2,50}$/i;

@Component({
  selector: 'cleansia-admin-membership-plan-form',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './membership-plan-form.component.html',
  providers: [MembershipPlanFormFacade],
})
export class MembershipPlanFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(MembershipPlanFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');
  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.membership_plans.form.edit_title')
      : this.translate.instant('pages.membership_plans.form.create_title')
  );

  private planId: string | null = null;

  readonly form = this.fb.nonNullable.group({
    code: this.fb.nonNullable.control<string>('', [
      Validators.required,
      Validators.maxLength(50),
      Validators.pattern(CODE_PATTERN),
    ]),
    name: this.fb.nonNullable.control<string>('', [
      Validators.required,
      Validators.maxLength(100),
    ]),
    billingInterval: this.fb.nonNullable.control<BillingIntervalWireValue>(
      BILLING_INTERVAL_WIRE.monthly
    ),
    monthlyPriceCzk: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0),
    ]),
    stripePriceId: this.fb.nonNullable.control<string>('', [
      Validators.required,
      Validators.maxLength(64),
    ]),
    discountPercentage: this.fb.nonNullable.control<number>(0, [
      Validators.required,
      Validators.min(0),
      Validators.max(100),
    ]),
    trialPeriodDays: this.fb.nonNullable.control<number>(0, [
      Validators.required,
      Validators.min(0),
    ]),
    freeCancellationWindowHours: this.fb.nonNullable.control<number>(0, [
      Validators.required,
      Validators.min(0),
    ]),
    allowsExpressUpgrade: this.fb.nonNullable.control<boolean>(false),
  });

  readonly intervalOptions = computed(() =>
    [BILLING_INTERVAL_WIRE.monthly, BILLING_INTERVAL_WIRE.yearly].map(
      (value) => ({
        label: this.translate.instant(BILLING_INTERVAL_LABEL_KEYS[value]),
        value,
      })
    )
  );

  private readonly populateEffect = effect(() => {
    const detail = this.facade.plan();
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

    if (this.isEditMode()) {
      const id = this.route.snapshot.paramMap.get('id');
      if (id) {
        this.planId = id;
        this.facade.loadPlan(id);
        this.disableImmutableFields();
      } else {
        this.facade.navigateBack();
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();

    if (this.isEditMode() && this.planId) {
      this.facade.update(this.planId, {
        name: v.name,
        monthlyPriceCzk: v.monthlyPriceCzk ?? 0,
        stripePriceId: v.stripePriceId,
        discountPercentage: v.discountPercentage,
        freeCancellationWindowHours: v.freeCancellationWindowHours,
        trialPeriodDays: v.trialPeriodDays,
        allowsExpressUpgrade: v.allowsExpressUpgrade,
      });
    } else {
      this.facade.create({
        code: v.code,
        name: v.name,
        billingInterval: v.billingInterval,
        monthlyPriceCzk: v.monthlyPriceCzk ?? 0,
        stripePriceId: v.stripePriceId,
        discountPercentage: v.discountPercentage,
        freeCancellationWindowHours: v.freeCancellationWindowHours,
        trialPeriodDays: v.trialPeriodDays,
        allowsExpressUpgrade: v.allowsExpressUpgrade,
      });
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }

  private disableImmutableFields(): void {
    this.form.controls.code.disable({ emitEvent: false });
    this.form.controls.billingInterval.disable({ emitEvent: false });
  }

  private populateFormFromDetail(detail: MembershipPlanDetailDto): void {
    this.form.patchValue({
      code: detail.code ?? '',
      name: detail.name ?? '',
      billingInterval: toBillingIntervalWireValue(detail.billingInterval),
      monthlyPriceCzk: detail.monthlyPriceCzk ?? null,
      stripePriceId: detail.stripePriceId ?? '',
      discountPercentage: detail.discountPercentage ?? 0,
      trialPeriodDays: detail.trialPeriodDays ?? 0,
      freeCancellationWindowHours: detail.freeCancellationWindowHours ?? 0,
      allowsExpressUpgrade: detail.allowsExpressUpgrade ?? false,
    });
  }
}
