import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminMembershipClient,
  BillingInterval,
  CreateMembershipPlanCommand,
  MembershipPlanDetailDto,
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

export interface MembershipPlanCreateInput {
  code: string;
  name: string;
  billingInterval: BillingIntervalWireValue;
  monthlyPriceCzk: number;
  stripePriceId: string;
  discountPercentage: number;
  freeCancellationWindowHours: number;
  trialPeriodDays: number;
  allowsExpressUpgrade: boolean;
}

export type MembershipPlanUpdateInput = Omit<
  MembershipPlanCreateInput,
  'code' | 'billingInterval'
>;

@Injectable()
export class MembershipPlanFormFacade extends UnsubscribeControlDirective {
  private readonly membershipClient = inject(AdminMembershipClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly plan = signal<MembershipPlanDetailDto | null>(null);
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

  create(input: MembershipPlanCreateInput): void {
    if (this.saving()) return;
    this.saving.set(true);

    const command = new CreateMembershipPlanCommand({
      code: input.code.trim().toUpperCase(),
      name: input.name.trim(),
      // The wire value is an int (Monthly=1, Yearly=2); the generated string
      // enum type is stale until the admin client is regenerated.
      billingInterval: input.billingInterval as unknown as BillingInterval,
      monthlyPriceCzk: input.monthlyPriceCzk,
      stripePriceId: input.stripePriceId.trim(),
      discountPercentage: input.discountPercentage,
      freeCancellationWindowHours: input.freeCancellationWindowHours,
      trialPeriodDays: input.trialPeriodDays,
      allowsExpressUpgrade: input.allowsExpressUpgrade,
    });

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

    const command = new UpdateMembershipPlanCommand({
      membershipPlanId: id,
      name: input.name.trim(),
      monthlyPriceCzk: input.monthlyPriceCzk,
      stripePriceId: input.stripePriceId.trim(),
      discountPercentage: input.discountPercentage,
      freeCancellationWindowHours: input.freeCancellationWindowHours,
      trialPeriodDays: input.trialPeriodDays,
      allowsExpressUpgrade: input.allowsExpressUpgrade,
    });

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
}
