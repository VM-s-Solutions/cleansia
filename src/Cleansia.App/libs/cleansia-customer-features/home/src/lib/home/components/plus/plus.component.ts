import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
} from '@angular/core';
import { RouterModule } from '@angular/router';
import {
  CustomerAuthService,
  MembershipPlanFactsService,
} from '@cleansia/customer-services';
import { selectMarketCountryId } from '@cleansia/customer-stores';
import { formatMoney, localeFor } from '@cleansia/utils';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { distinctUntilChanged } from 'rxjs';

/**
 * The home page's argument for Cleansia Plus.
 *
 * Its figures used to be COPY — 199 CZK, 5 %, 4 hours, one express clean,
 * 2 030 CZK and 15 %, written into five locales each — while the public
 * `/plus` page read the same six off `Membership/GetPlans`. An admin moving a
 * price would have left the two pages on the same site disagreeing, and only
 * one of them would have been right. Both read the catalogue now.
 *
 * One of those numbers was also simply wrong: the cancellation perk said the
 * window was "4 hours longer", but 4 is the WINDOW (a member cancels free up
 * to four hours before) against a standard of twenty-four — so it is twenty
 * hours longer, not four.
 */
@Component({
  selector: 'cleansia-plus',
  templateUrl: './plus.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, RouterModule],
})
export class PlusComponent implements OnInit {
  private readonly authService = inject(CustomerAuthService);
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  readonly facts = inject(MembershipPlanFactsService);

  /**
   * This CTA pointed at `/membership/subscribe` unconditionally, which is
   * behind `customerAuthGuard` — so the home page's argument for a paid
   * membership ended, for every visitor without an account, at a login form.
   * They get the public page instead; a signed-in visitor still goes straight
   * to checkout.
   *
   * Bound as an attribute rather than branched: `isLoggedIn()` resolves
   * differently on the server than at hydration, and changing the node count
   * here is an NG0500.
   */
  readonly ctaLink = computed(() =>
    this.authService.isLoggedIn() ? '/membership/subscribe' : '/plus',
  );

  /**
   * The band renders only once there is a plan to read it from — priced in the
   * chosen market (ADR-0059 D3). Not a set of fallback constants: a fallback
   * matching today's seed would be right until the day it mattered, which is
   * the failure this replaces. With no plan — not on sale in this market, or no
   * answer — the band is omitted: an upsell that cannot state a price and
   * cannot be bought is not an upsell.
   */
  readonly canStateFigures = this.facts.hasPlans;

  ngOnInit(): void {
    this.store
      .select(selectMarketCountryId)
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((countryId) => this.facts.load(countryId));
  }

  /** Labelled with the plans' own currency code, never a platform default. */
  formatPrice(amount: number): string {
    return formatMoney(amount, this.facts.currencyCode(), localeFor(this.translate.currentLang));
  }
}
