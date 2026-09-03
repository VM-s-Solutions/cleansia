import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
  ResumeOrderCheckoutCommand,
} from '@cleansia/customer-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of, take } from 'rxjs';

/**
 * Where Stripe returns a customer who backed out.
 *
 * Nothing was charged and the ORDER STILL EXISTS — `CreateOrder` runs before the redirect to
 * Stripe, so backing out leaves a real, unpaid booking rather than nothing. But it exists for
 * exactly ONE HOUR: `CleanupStalePendingOrders` sweeps unpaid card orders older than that, on a
 * 15-minute timer. The copy says so, because the page used to promise the booking was kept and
 * that promise expires while the customer is still reading it.
 *
 * Stripe's own cancel URL carries `?orderId=`, so the page knows which booking it is talking about
 * without the `GuestOrderService` lookup the success page needs. That is what makes "pay now" —
 * `Payment/ResumeCheckout`, which replays the abandoned session rather than minting a second one —
 * possible here. It is offered only to a signed-in customer: the endpoint is `[Authorize]` like
 * every other mutation of an existing order, so a guest gets the booking wizard instead.
 */
@Component({
  selector: 'cleansia-customer-checkout-cancel',
  standalone: true,
  imports: [TranslatePipe, RouterLink],
  templateUrl: './checkout-cancel.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckoutCancelComponent {
  private readonly authService = inject(CustomerAuthService);
  private readonly customerClient = inject(CustomerClient);
  private readonly route = inject(ActivatedRoute);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly routes = CleansiaCustomerRoute;
  readonly ordersRoute = this.authService.isLoggedIn()
    ? '/' + CleansiaCustomerRoute.ORDERS
    : '/' + CleansiaCustomerRoute.TRACK_ORDER;

  /** Stripe's `CancelUrl` is built as `.../checkout/cancel?orderId={order.Id}`. */
  private readonly orderId = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('orderId'))),
    { initialValue: null },
  );

  readonly resuming = signal(false);

  /**
   * Whether to offer finishing the payment at all.
   *
   * Deliberately optimistic — it does not pre-check the order's state. The endpoint is the
   * authority on whether this booking can still be paid for, and asking it up front would put a
   * Stripe round trip behind every view of this page for the sake of a button that is usually
   * right. When it refuses, the customer is told why.
   */
  readonly canResume = computed(() => !!this.orderId() && this.authService.isLoggedIn());

  resumeCheckout(): void {
    const orderId = this.orderId();
    if (!orderId || this.resuming()) return;

    this.resuming.set(true);
    const command = new ResumeOrderCheckoutCommand();
    command.orderId = orderId;
    this.customerClient.paymentClient
      .resumeCheckout(command)
      .pipe(
        take(1),
        catchError(() => of(null)),
      )
      .subscribe((response) => {
        this.resuming.set(false);
        if (response?.checkoutUrl) {
          if (this.isBrowser) window.location.href = response.checkoutUrl;
          return;
        }
        // The commonest refusal by far is an order the hourly sweep has already
        // cancelled, which is precisely the case the lead warns about.
        this.snackbar.showError(
          this.translate.instant('pages.checkout.cancel.resume_failed'),
        );
      });
  }
}
