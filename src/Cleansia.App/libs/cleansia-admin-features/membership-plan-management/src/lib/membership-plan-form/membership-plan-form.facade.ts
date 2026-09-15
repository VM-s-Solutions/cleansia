import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminCurrencyClient,
  AdminCurrencyListItem,
  AdminMembershipClient,
  CreateMembershipPlanCommand,
  MembershipPlanDetailDto,
  MembershipPlanPriceInput,
  UpdateMembershipPlanCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  BillingIntervalWireValue,
  resolveMembershipPlanErrorKey,
} from '../membership-plan-list/membership-plan-list.models';
import {
  MembershipPlanPriceEntry,
  PlanCurrencyOption,
} from './membership-plan-form.models';

export interface MembershipPlanCreateInput {
  code: string;
  name: string;
  billingInterval: BillingIntervalWireValue;
  /**
   * One entry per currency CODE the admin filled in. A currency left blank is absent rather than
   * present at zero — the backend upserts a row for every key it receives, and a plan with no row
   * in a currency is "Plus is not on sale in that market", which is a legal state.
   */
  prices: { [code: string]: MembershipPlanPriceEntry };
  discountPercentage: number;
  freeCancellationWindowHours: number;
  trialPeriodDays: number;
  allowsExpressUpgrade: boolean;
  expressUpgradesPerMonth: number;
}

export type MembershipPlanUpdateInput = Omit<
  MembershipPlanCreateInput,
  'code' | 'billingInterval'
>;

@Injectable()
export class MembershipPlanFormFacade extends UnsubscribeControlDirective {
  private readonly membershipClient = inject(AdminMembershipClient);
  private readonly currencyClient = inject(AdminCurrencyClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly plan = signal<MembershipPlanDetailDto | null>(null);
  readonly currencies = signal<PlanCurrencyOption[]>([]);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadPlan(id: string): void {
    this.loading.set(true);
    this.membershipClient
      .details(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.plan.set(response);
        } else {
          this.navigateBack();
        }
      });
  }

  loadCurrencies(): void {
    this.currencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as AdminCurrencyListItem[]))
      )
      .subscribe((currencies) => {
        // The generated client hands back null, not [], for a body that is not a JSON array.
        this.currencies.set(
          (currencies ?? [])
            .filter(
              (c): c is AdminCurrencyListItem & { code: string } => Boolean(c.code)
            )
            .map((c) => ({
              code: c.code,
              symbol: c.symbol ?? c.code,
              name: c.name ?? c.code,
              isDefault: c.isDefault,
              isActive: c.isActive,
            }))
        );
      });
  }

  create(input: MembershipPlanCreateInput): void {
    if (this.saving()) return;
    this.saving.set(true);

    const command = new CreateMembershipPlanCommand();
    command.code = input.code.trim().toUpperCase();
    command.name = input.name.trim();
    command.billingInterval = input.billingInterval;
    command.prices = this.buildPrices(input.prices);
    command.discountPercentage = input.discountPercentage;
    command.freeCancellationWindowHours = input.freeCancellationWindowHours;
    command.trialPeriodDays = input.trialPeriodDays;
    command.allowsExpressUpgrade = input.allowsExpressUpgrade;
    command.expressUpgradesPerMonth = input.expressUpgradesPerMonth;

    this.membershipClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(resolveMembershipPlanErrorKey(error))
          );
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccess(
            this.translate.instant('pages.membership_plans.form.success.created')
          );
          this.navigateBack();
        }
      });
  }

  update(id: string, input: MembershipPlanUpdateInput): void {
    if (this.saving()) return;
    this.saving.set(true);

    const command = new UpdateMembershipPlanCommand();
    command.membershipPlanId = id;
    command.name = input.name.trim();
    command.prices = this.buildPrices(input.prices);
    command.discountPercentage = input.discountPercentage;
    command.freeCancellationWindowHours = input.freeCancellationWindowHours;
    command.trialPeriodDays = input.trialPeriodDays;
    command.allowsExpressUpgrade = input.allowsExpressUpgrade;
    command.expressUpgradesPerMonth = input.expressUpgradesPerMonth;

    this.membershipClient
      .update(id, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(resolveMembershipPlanErrorKey(error))
          );
          return of(null);
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccess(
            this.translate.instant('pages.membership_plans.form.success.updated')
          );
          this.navigateBack();
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([`/${CleansiaAdminRoute.MEMBERSHIP_PLAN_MANAGEMENT}`]);
  }

  private buildPrices(source: { [code: string]: MembershipPlanPriceEntry }): {
    [code: string]: MembershipPlanPriceInput;
  } {
    const prices: { [code: string]: MembershipPlanPriceInput } = {};
    for (const [code, entry] of Object.entries(source)) {
      const input = new MembershipPlanPriceInput();
      input.price = entry.price;
      input.stripePriceId = entry.stripePriceId.trim();
      prices[code] = input;
    }
    return prices;
  }
}
