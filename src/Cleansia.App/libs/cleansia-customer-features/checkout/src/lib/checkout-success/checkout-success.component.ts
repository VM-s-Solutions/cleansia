import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { GuestOrderService, TrackOrderFacade } from '@cleansia-customer/orders';
import {
  CustomerAuthService,
  LookupOrderResponse,
} from '@cleansia/customer-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';

/**
 * The page a customer lands on after paying — the owner picked the "Katalog"
 * board for it (2026-09-02): a page on the site rather than a receipt slip.
 * The slip's job is done by the confirmation e-mail.
 *
 * It shows the ORDER, which it has to find for itself. Neither path carries it
 * in the URL: the cash path navigates here with only `?type=cash`, and the card
 * path leaves for Stripe and comes back to a URL Stripe builds. But the wizard
 * writes `(orderId, email)` to `GuestOrderService` immediately before either
 * departure — for signed-in customers too — so the newest entry there IS this
 * order, and one anonymous lookup turns it into something worth showing.
 *
 * When that lookup finds nothing the page keeps its headline, its three steps
 * and its actions and simply states no figures. A confirmation that cannot
 * prove what it is confirming should not invent it.
 */
@Component({
  selector: 'cleansia-customer-checkout-success',
  standalone: true,
  imports: [TranslatePipe, RouterLink],
  templateUrl: './checkout-success.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [TrackOrderFacade],
})
export class CheckoutSuccessComponent implements OnInit {
  private readonly authService = inject(CustomerAuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly titleService = inject(Title);
  private readonly translate = inject(TranslateService);
  private readonly guestOrders = inject(GuestOrderService);
  // The same wrapper the tracking page uses. The customer order client has no
  // DI registration in this app, and building a second hand-wired copy of it
  // here is how the two would have drifted.
  private readonly lookupFacade = inject(TrackOrderFacade);

  readonly routes = CleansiaCustomerRoute;
  readonly ordersRoute = this.authService.isLoggedIn()
    ? '/' + CleansiaCustomerRoute.ORDERS
    : '/' + CleansiaCustomerRoute.TRACK_ORDER;

  private readonly paymentType = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('type') ?? 'card')),
    { initialValue: 'card' },
  );

  readonly isCash = computed(() => this.paymentType() === 'cash');

  readonly order = signal<LookupOrderResponse | null>(null);
  readonly hasOrder = computed(() => this.order() !== null);

  constructor() {
    effect(() => {
      const titleKey = this.isCash()
        ? 'pages.checkout.success.cash.title'
        : 'pages.checkout.success.card.title';
      this.titleService.setTitle(`${this.translate.instant(titleKey)} | Cleansia`);
    });
  }

  ngOnInit(): void {
    // Newest first — `GuestOrderService.save` unshifts.
    const latest = this.guestOrders.getAll()[0];
    if (!latest) return;

    // BATCH, not the single lookup. `GuestOrderService` stores the order's
    // ULID, and `LookupOrder` matches on DisplayOrderNumber — a ULID is never
    // a display number, so the single lookup could not have matched a real
    // order. `LookupOrderBatch` is keyed on `Order.Id`, which is what is
    // stored, and it needs no confirmation code precisely because that id is
    // itself the secret: 26 unguessable characters the browser only holds
    // because it placed the order.
    this.lookupFacade
      .lookupBatch([{ orderId: latest.orderId, email: latest.email }])
      .pipe(catchError(() => of(null)))
      .subscribe((result) => this.order.set(result?.orders?.[0] ?? null));
  }

  private getLocale(): string {
    const map: Record<string, string> = {
      cs: 'cs-CZ',
      en: 'en-US',
      sk: 'sk-SK',
      uk: 'uk-UA',
      ru: 'ru-RU',
    };
    return map[this.translate.currentLang] || 'en-US';
  }

  formatDateTime(date: Date | undefined): string {
    if (!date) return '';
    const d = date instanceof Date ? date : new Date(date);
    return d.toLocaleDateString(this.getLocale(), {
      day: 'numeric',
      month: 'long',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(order: LookupOrderResponse): string {
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: order.currency?.code || 'CZK',
      minimumFractionDigits: 0,
    }).format(order.totalPrice ?? 0);
  }

}
