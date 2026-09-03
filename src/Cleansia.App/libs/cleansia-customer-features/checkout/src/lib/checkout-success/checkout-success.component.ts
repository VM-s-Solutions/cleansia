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

  /**
   * Stripe's success URL carries `?orderId=`. Read through the same param map
   * as the payment type rather than off `route.snapshot`, so the two agree and
   * neither depends on when in the lifecycle it is asked.
   */
  private readonly orderIdFromUrl = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('orderId'))),
    { initialValue: null },
  );

  private readonly currentLang = toSignal(
    this.translate.onLangChange.pipe(map((e) => e.lang)),
    { initialValue: this.translate.currentLang || this.translate.getDefaultLang() },
  );

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
    // Stripe's success URL carries `?orderId=`, so on the card path the page is TOLD which order
    // it is confirming. Match that against the remembered entries rather than assuming the newest
    // is the right one — a customer with two bookings open in two tabs would otherwise be shown
    // the wrong figures. The cash path navigates here without an orderId and still falls back to
    // the newest, which is correct there because it was written moments earlier.
    const remembered = this.guestOrders.getAll();
    const fromUrl = this.orderIdFromUrl();
    const latest = (fromUrl && remembered.find((o) => o.orderId === fromUrl)) || remembered[0];
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

  /**
   * Every service and package on the order, named in the reader's language.
   *
   * The name resolution is inlined rather than imported: the helper that does
   * this lives in the order-wizard library, and four other features already
   * carry their own copy of these six lines rather than reach across a feature
   * boundary for them.
   */
  readonly bookedItems = computed<
    readonly { key: string; name: string; isPackage: boolean }[]
  >(() => {
    const order = this.order();
    if (!order) return [];
    // Read the signal so the list re-resolves when the language changes.
    const lang = this.currentLang();
    const named = (
      item: { id?: string; name?: string; translations?: Record<string, unknown> },
      isPackage: boolean,
    ) => {
      const bundle = item.translations?.[lang] as Record<string, string> | undefined;
      return {
        key: (isPackage ? 'p:' : 's:') + (item.id ?? item.name ?? ''),
        name: bundle?.['name'] || item.name || '',
        isPackage,
      };
    };
    return [
      ...(order.selectedPackages ?? []).map((p) => named(p, true)),
      ...(order.selectedServices ?? []).map((s) => named(s, false)),
    ].filter((i) => i.name !== '');
  });

  formatPrice(order: LookupOrderResponse): string {
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: order.currency?.code || 'CZK',
      minimumFractionDigits: 0,
    }).format(order.totalPrice ?? 0);
  }

}
