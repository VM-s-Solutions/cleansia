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
  CUSTOMER_API_BASE_URL,
  CustomerAuthService,
  CustomerOrderClient,
  LookupOrderResponse,
} from '@cleansia/customer-services';
import {
  CleansiaCustomerRoute,
  errorToastSuppressingHttpClient,
} from '@cleansia/services';
import { formatMoney, localeFor } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, Observable, of } from 'rxjs';

/**
 * What this page reads off a booking. The guest lookup and the signed-in order detail are different
 * DTOs that answer these members identically, which is why one template serves both.
 */
type ConfirmedBooking = Pick<
  LookupOrderResponse,
  | 'displayOrderNumber'
  | 'cleaningDateTime'
  | 'totalPrice'
  | 'currency'
  | 'selectedServices'
  | 'selectedPackages'
>;

/**
 * The page a customer lands on after paying — the owner picked the "Katalog"
 * board for it (2026-09-02): a page on the site rather than a receipt slip.
 * The slip's job is done by the confirmation e-mail.
 *
 * It shows the ORDER when it can prove it. Both paths name the booking in the
 * URL — the wizard puts `?orderId=` on the cash navigation and Stripe returns
 * it on the card one — and an id in a URL proves nothing, so each visitor reads
 * it back with what they actually hold: a guest with the booking's access token,
 * which the wizard wrote to `GuestOrderService` from the create response, and a
 * signed-in customer with their session, through the same order detail their
 * orders page reads. A guest booking mints no token for an account and an
 * account booking mints none at all, so the two reads never cross.
 *
 * When neither can prove it — a browser that did not place this booking, a
 * session the booking does not belong to — the page keeps its headline, its
 * three steps and its actions and simply states no figures, saying nothing
 * about whether that booking exists. A confirmation that cannot prove what it
 * is confirming should not invent it.
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
  // The guest read borrows the tracking page's facade rather than a second hand-wired copy of the
  // client it holds — that is how the two would have drifted.
  private readonly lookupFacade = inject(TrackOrderFacade);
  // The signed-in read is the account's own order detail, built here rather than taken from
  // `CustomerClient` for one reason: that wrapper hands its sub-clients the ambient HttpClient,
  // which is the one the shared error snackbar rides, and a booking this session cannot open has to
  // be silence on the confirmation rather than a red toast over it.
  private readonly ownBookings = new CustomerOrderClient(
    errorToastSuppressingHttpClient(),
    inject(CUSTOMER_API_BASE_URL),
  );

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

  readonly order = signal<ConfirmedBooking | null>(null);
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
    // The page only ever shows the booking it was TOLD about. It used to fall back to the newest
    // remembered entry when the URL named nothing, which on a second tab confirmed a different
    // booking's figures.
    const fromUrl = this.orderIdFromUrl();
    if (!fromUrl) return;

    const found: Observable<ConfirmedBooking | null> | null = this.authService.isLoggedIn()
      ? this.ownBookings.getById(fromUrl)
      : this.rememberedBooking(fromUrl);
    if (!found) return;

    found
      .pipe(catchError(() => of(null)))
      .subscribe((booking) => this.order.set(booking ?? null));
  }

  /**
   * A refused read and an unknown booking answer the same way — null — so the page cannot be asked
   * whether somebody else's order exists.
   */
  private rememberedBooking(orderId: string): Observable<ConfirmedBooking | null> | null {
    const remembered = this.guestOrders.getAll().find((o) => o.orderId === orderId);
    if (!remembered) return null;
    return this.lookupFacade
      .lookupBatch([remembered.accessToken])
      .pipe(map((result) => result.orders?.[0] ?? null));
  }

  formatDateTime(date: Date | undefined): string {
    if (!date) return '';
    const d = date instanceof Date ? date : new Date(date);
    return d.toLocaleDateString(localeFor(this.translate.currentLang), {
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

  formatPrice(order: ConfirmedBooking): string {
    return formatMoney(
      order.totalPrice ?? 0,
      order.currency?.code,
      localeFor(this.translate.currentLang),
    );
  }

}
