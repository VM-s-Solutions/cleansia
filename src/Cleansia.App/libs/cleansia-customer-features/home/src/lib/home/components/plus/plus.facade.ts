import { computed, inject, Injectable, signal } from '@angular/core';
import {
  CustomerAuthService,
  CustomerClient,
  GetMyMembershipResponse,
  MembershipPlanFactsService,
  offeredTrialDays,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { Store } from '@ngrx/store';
import { catchError, distinctUntilChanged, of, take, takeUntil } from 'rxjs';

@Injectable()
export class PlusFacade extends UnsubscribeControlDirective {
  private readonly facts = inject(MembershipPlanFactsService);
  private readonly membershipClient = inject(CustomerClient).membershipClient;
  private readonly authService = inject(CustomerAuthService);
  private readonly store = inject(Store);

  private readonly membership = signal<GetMyMembershipResponse | null>(null);

  readonly trialDays = computed(() =>
    offeredTrialDays(this.facts.monthlyPlan() ?? this.facts.plans()[0], this.membership()),
  );

  load(): void {
    this.store
      .select(selectMarketCountryId)
      .pipe(distinctUntilChanged(), takeUntil(this.destroyed$))
      .subscribe((countryId) => this.facts.load(countryId));

    if (!this.authService.isLoggedIn()) return;
    this.membershipClient
      .getMine()
      .pipe(take(1), takeUntil(this.destroyed$), catchError(() => of(null)))
      .subscribe((response) => this.membership.set(response));
  }
}
