import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { computed } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaScrollTopComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import {
  CustomerAuthService,
  LookupOrderResponse,
  LookupOrderBatchResponse,
} from '@cleansia/customer-services';
import { OrderStatus, PaymentStatus } from '@cleansia/models';
import { OrderStatusIconPipe, OrderStatusLabelPipe } from '@cleansia/pipes';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { formatMoney } from '@cleansia/utils';
import { GuestOrderService } from './guest-order.service';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';
import { TrackOrderFacade } from './track-order.facade';

@Component({
  selector: 'cleansia-customer-track-order',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaScrollTopComponent,
    CleansiaTextInputComponent,
    FoamEdgeComponent,
    OrderStatusLabelPipe,
    OrderStatusIconPipe,
  ],
  templateUrl: './track-order.component.html',
  providers: [TrackOrderFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrackOrderComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly guestOrderService = inject(GuestOrderService);
  private readonly facade = inject(TrackOrderFacade);
  private readonly authService = inject(CustomerAuthService);

  routes = CleansiaCustomerRoute;
  readonly isLoggedIn = this.authService.isLoggedIn;

  /**
   * The order's own axis, drawn as the whole journey rather than only the
   * stops it has already made — a reader wants to know what is still to come,
   * and a list that stops at "Confirmed" says nothing about what happens next.
   * `Pending` is deliberately absent: it has no production writer and the state
   * it used to describe lives on the payment axis. -> /domain/order-lifecycle
   */
  private static readonly ORDER_STEPS: readonly { key: string; value: OrderStatus }[] = [
    { key: 'new', value: OrderStatus.New },
    { key: 'confirmed', value: OrderStatus.Confirmed },
    { key: 'on_the_way', value: OrderStatus.OnTheWay },
    { key: 'in_progress', value: OrderStatus.InProgress },
    { key: 'completed', value: OrderStatus.Completed },
  ];

  private readonly fb = inject(FormBuilder);

  /**
   * A real form, not three loose signals. The fields accepted anything at all
   * — a lookup could be fired with `abc` as an e-mail — so every attempt cost
   * a round trip against a rate-limited endpoint to be told what the browser
   * already knew.
   *
   * The confirmation code is `Guid.NewGuid().ToString("N")[..6].ToUpper()`:
   * exactly six characters, hexadecimal, generated uppercase. The pattern
   * accepts either case because it is read off an e-mail and typed by hand,
   * and the server compares case-insensitively.
   */
  readonly form: FormGroup = this.fb.nonNullable.group({
    orderNumber: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    confirmationCode: [
      '',
      [
        // Length first: a short code is the common mistake and
        // "at least 6 characters" says what to do, where the pattern's
        // message can only say the field is wrong. The pattern still runs, for
        // the rarer case of six characters that are not hexadecimal.
        Validators.required,
        Validators.minLength(6),
        Validators.maxLength(6),
        Validators.pattern(/^[0-9a-fA-F]+$/),
      ],
    ],
  });

  // State
  loading = signal(false);
  recentOrders = signal<LookupOrderResponse[]>([]);
  manualResult = signal<LookupOrderResponse | null>(null);
  error = signal<string | null>(null);
  searched = signal(false);

  /**
   * The page's one branch. The form IS the page until an order is found, and
   * the order is the page after that — there is no third thing to toggle, so
   * the flag that used to gate the form went with the second screen.
   */
  readonly found = computed(() => this.manualResult());

  /**
   * Whether the three values are even worth sending. `form.valid` is not a
   * signal, so this is bumped by the form's own value stream — the button has
   * to react to typing.
   */
  private readonly formValue = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  });
  readonly canSubmit = computed(() => {
    this.formValue();
    return this.form.valid;
  });

  readonly isPaid = computed(
    () => this.manualResult()?.paymentStatus?.value === PaymentStatus.Paid,
  );

  /**
   * The sentence at the top. A status pill names the state; this says what it
   * MEANS for the person reading, which is the thing they came for.
   */
  readonly headline = computed(() => {
    const value = this.manualResult()?.orderStatus?.value;
    const key = TrackOrderComponent.ORDER_STEPS.find((s) => s.value === value)?.key;
    return this.translate.instant(
      value === OrderStatus.Cancelled
        ? 'pages.track_order.headline.cancelled'
        : `pages.track_order.headline.${key ?? 'new'}`,
    );
  });

  /**
   * Each step with the moment it happened, read off the order's own history
   * rather than assumed from its current state — an order can skip a state
   * (a cash job that is confirmed and started in one motion), and a timeline
   * that infers timestamps would invent them.
   */
  readonly orderSteps = computed(() => {
    const order = this.manualResult();
    const history = order?.statusHistory ?? [];
    const currentValue = order?.orderStatus?.value;
    const reached = new Set(history.map((h) => h.status?.value));

    return TrackOrderComponent.ORDER_STEPS.map((step) => {
      const entry = history.find((h) => h.status?.value === step.value);
      return {
        key: step.key,
        label: this.translate.instant(`pages.track_order.step.${step.key}`),
        when: entry ? this.formatDate(entry.createdOn) : '',
        done: reached.has(step.value) && step.value !== currentValue,
        current: step.value === currentValue,
      };
    });
  });

  /**
   * The money's own axis, drawn the same way as the order's so the two read as
   * two tracks rather than a list and a footnote.
   *
   * It carries no timestamps: the guest lookup returns `statusHistory` for the
   * ORDER only, and the payment has no history of its own on the response. The
   * rows say what state the money is in and what happens next, which is true;
   * borrowing the order's clock would date a different event.
   */
  readonly paymentSteps = computed(() => {
    const order = this.manualResult();
    if (!order) return [];

    const status = order.paymentStatus?.value;
    const paid = status === PaymentStatus.Paid;
    const refunded = status === PaymentStatus.Refunded;
    const failed = status === PaymentStatus.Failed;

    const steps = [
      // Done, never "now": choosing how to pay is a thing that HAPPENED. What
      // is still ahead is the money arriving, and that is the row below.
      {
        key: 'method',
        label: this.translate.instant(
          paid ? 'pages.track_order.pay_method_card' : 'pages.track_order.pay_method_cash',
        ),
        when: this.formatDate(order.createdOn),
        done: true,
        current: false,
      },
    ];

    if (failed) {
      steps.push({
        key: 'failed',
        label: this.translate.instant('pages.track_order.pay_failed'),
        when: '',
        done: true,
        current: true,
      });
    } else {
      steps.push({
        key: 'paid',
        label: this.translate.instant('pages.track_order.pay_paid'),
        when: this.translate.instant(
          paid ? 'pages.track_order.paid_done' : 'pages.track_order.paid_after',
        ),
        done: paid || refunded,
        current: paid,
      });
    }

    if (refunded) {
      steps.push({
        key: 'refunded',
        label: this.translate.instant('pages.track_order.pay_refunded'),
        when: '',
        done: true,
        current: true,
      });
    }

    return steps;
  });

  /** A cancelled order's pill is the one that is not the brand tint. */
  readonly isCancelled = computed(
    () => this.manualResult()?.orderStatus?.value === OrderStatus.Cancelled,
  );

  /** Every service and package on the order, as chips. */
  readonly lineItems = computed(() => {
    const order = this.manualResult();
    if (!order) return [] as string[];
    return [
      ...(order.selectedServices ?? []).map((s) => s.name ?? ''),
      ...(order.selectedPackages ?? []).map((p) => p.name ?? ''),
    ].filter((n) => n.length > 0);
  });

  /**
   * Cash is settled with the cleaner after the work; a card was charged when
   * the booking was made. Saying the wrong one is the difference between a
   * customer having their wallet ready and not.
   */
  readonly priceNote = computed(() =>
    this.isPaid()
      ? 'pages.track_order.price_note_paid'
      : 'pages.track_order.price_note_cash',
  );

  /** Back to the form, with the fields kept so a typo can be corrected. */
  reset(): void {
    this.manualResult.set(null);
    this.error.set(null);
    this.searched.set(false);
  }

  formatDuration(minutes: number | undefined): string {
    if (!minutes) return '—';
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    if (h === 0) return `${m} min`;
    return m === 0 ? `${h} h` : `${h} h ${m} min`;
  }

  ngOnInit(): void {
    const params = this.route.snapshot.queryParams;
    if (params['orderNumber'] || params['email'] || params['code']) {
      // A deep link prefills whatever it carries; the lookup only fires when
      // the three together are actually valid, rather than firing a request
      // that has to fail.
      this.form.patchValue({
        orderNumber: params['orderNumber'] ?? '',
        email: params['email'] ?? '',
        confirmationCode: params['code'] ?? '',
      });
      if (this.form.valid) this.lookup();
    } else {
      this.loadGuestOrders();
    }
  }

  private loadGuestOrders(): void {
    const guestOrders = this.guestOrderService.getAll();
    if (guestOrders.length === 0) return;

    this.loading.set(true);
    this.facade
      .lookupBatch(guestOrders.map((o) => ({ orderId: o.orderId, email: o.email })))
      .pipe(takeUntil(this.facade.destroyed$))
      .subscribe({
        next: (data: LookupOrderBatchResponse) => {
          this.recentOrders.set(data.orders || []);
          this.loading.set(false);
        },
        error: () => {
          // The remembered list is a convenience — the form below still works.
          this.loading.set(false);
        },
      });
  }

  lookup(): void {
    // Touch everything first: a field the customer never focused has no error
    // to show until it is marked, so an invalid submit would look like nothing
    // happened at all.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { orderNumber, email, confirmationCode } = this.form.getRawValue();

    this.loading.set(true);
    this.error.set(null);
    this.manualResult.set(null);
    this.searched.set(true);

    this.facade
      .lookup(orderNumber.trim(), email.trim(), confirmationCode.trim())
      .pipe(takeUntil(this.facade.destroyed$))
      .subscribe({
        next: (data) => {
          this.manualResult.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.error.set(
            this.translate.instant('pages.track_order.not_found')
          );
          this.loading.set(false);
        },
      });
  }

  /**
   * A remembered order opens where it is.
   *
   * It used to navigate to `/orders/lookup/:id`, a second screen that drew the
   * same order from a cache written on the way out. The board draws ONE page
   * with three states and the answer already renders here, so the row just
   * fills it in — no cache to keep in step, no second screen to style, and no
   * round trip: `lookupBatch` already returned the whole order.
   */
  showOrder(order: LookupOrderResponse): void {
    this.manualResult.set(order);
    this.error.set(null);
    this.searched.set(true);
  }

  private getLocale(): string {
    const localeMap: Record<string, string> = {
      cs: 'cs-CZ',
      en: 'en-US',
      sk: 'sk-SK',
      uk: 'uk-UA',
      ru: 'ru-RU',
    };
    return localeMap[this.translate.currentLang] || 'en-US';
  }

  formatDate(date: string | Date | undefined): string {
    if (!date) return '';
    const d = date instanceof Date ? date : new Date(date);
    return d.toLocaleDateString(this.getLocale(), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPriceFor(
    order: LookupOrderResponse,
    price: number | undefined
  ): string {
    if (price == null) return '';
    return formatMoney(price, order.currency?.code, this.getLocale());
  }
}
