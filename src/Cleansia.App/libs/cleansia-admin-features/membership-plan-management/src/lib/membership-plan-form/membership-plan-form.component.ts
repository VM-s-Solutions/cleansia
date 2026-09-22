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
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  MembershipPlanDetailDto,
  MembershipPlanPriceDto,
} from '@cleansia/admin-services';
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
  MembershipPlanPriceBlockValue,
  PRICE_BLOCK_HALF_FILLED_ERROR,
  PlanCurrencyOption,
  collectFilledPriceBlocks,
  priceBlockCompleteValidator,
} from './membership-plan-form.models';
import {
  BILLING_INTERVAL_LABEL_KEYS,
  BILLING_INTERVAL_WIRE,
  BillingIntervalWireValue,
  toBillingIntervalWireValue,
} from '../membership-plan-list/membership-plan-list.models';

const CODE_PATTERN = /^[A-Z0-9_]{2,50}$/i;

type PriceBlockGroup = FormGroup<{
  price: FormControl<number | string | null>;
  stripePriceId: FormControl<string>;
}>;

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

  protected readonly halfFilledError = PRICE_BLOCK_HALF_FILLED_ERROR;

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
    prices: this.fb.nonNullable.group({}),
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
    expressUpgradesPerMonth: this.fb.nonNullable.control<number>(0, [
      Validators.required,
      Validators.min(0),
    ]),
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

  private readonly currenciesEffect = effect(() => {
    const currencies = this.facade.currencies();
    if (currencies.length === 0) return;
    this.buildPriceBlocks(currencies);
    // The plan may have landed before the currency list did, with no blocks to write into yet.
    const detail = this.facade.plan();
    if (detail && this.isEditMode()) {
      this.patchPrices(detail.prices);
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

  priceBlock(code: string): PriceBlockGroup {
    return this.form.controls.prices.get(code) as PriceBlockGroup;
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const prices = collectFilledPriceBlocks(
      v.prices as { [code: string]: MembershipPlanPriceBlockValue }
    );

    if (this.isEditMode() && this.planId) {
      this.facade.update(this.planId, {
        name: v.name,
        prices,
        discountPercentage: v.discountPercentage,
        freeCancellationWindowHours: v.freeCancellationWindowHours,
        trialPeriodDays: v.trialPeriodDays,
        allowsExpressUpgrade: v.allowsExpressUpgrade,
        expressUpgradesPerMonth: v.expressUpgradesPerMonth,
      });
    } else {
      this.facade.create({
        code: v.code,
        name: v.name,
        billingInterval: v.billingInterval,
        prices,
        discountPercentage: v.discountPercentage,
        freeCancellationWindowHours: v.freeCancellationWindowHours,
        trialPeriodDays: v.trialPeriodDays,
        allowsExpressUpgrade: v.allowsExpressUpgrade,
        expressUpgradesPerMonth: v.expressUpgradesPerMonth,
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

  private buildPriceBlocks(currencies: PlanCurrencyOption[]): void {
    const pricesGroup = this.form.controls.prices;
    for (const currency of currencies) {
      if (pricesGroup.contains(currency.code)) continue;
      pricesGroup.addControl(
        currency.code,
        new FormGroup(
          {
            price: new FormControl<number | string | null>(null, [
              Validators.min(0),
            ]),
            stripePriceId: new FormControl<string>('', {
              nonNullable: true,
              validators: [Validators.maxLength(64)],
            }),
          },
          { validators: priceBlockCompleteValidator() }
        )
      );
    }
  }

  private patchPrices(prices?: { [code: string]: MembershipPlanPriceDto }): void {
    if (!prices) return;
    for (const [code, entry] of Object.entries(prices)) {
      this.form.controls.prices.get(code)?.patchValue({
        price: entry.price ?? null,
        stripePriceId: entry.stripePriceId ?? '',
      });
    }
  }

  private populateFormFromDetail(detail: MembershipPlanDetailDto): void {
    this.form.patchValue({
      code: detail.code ?? '',
      name: detail.name ?? '',
      billingInterval: toBillingIntervalWireValue(detail.billingInterval),
      discountPercentage: detail.discountPercentage ?? 0,
      trialPeriodDays: detail.trialPeriodDays ?? 0,
      freeCancellationWindowHours: detail.freeCancellationWindowHours ?? 0,
      allowsExpressUpgrade: detail.allowsExpressUpgrade ?? false,
      expressUpgradesPerMonth: detail.expressUpgradesPerMonth ?? 0,
    });
    this.patchPrices(detail.prices);
  }
}
