import { CommonModule, isPlatformBrowser } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaButtonComponent } from '@cleansia/components';
import { OrderStatusLabelPipe } from '@cleansia/pipes';
import { OrderStatus, PaymentStatus } from '@cleansia/customer-services';
import {
  RECURRING_PREFILL_STORAGE_KEY,
  RecurringPrefillParams,
} from '@cleansia-customer/recurring-bookings';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
import { OrderPreferredOfferComponent } from './components/order-preferred-offer.component';
import { OrderDetailFacade } from './order-detail.facade';
import {
  buildReviewLineOptions,
  ReviewLineOption,
} from './order-review-lines.models';
import { OrderPreferredOfferFacade } from './order-preferred-offer.facade';

/** One row on either status axis. -> the Detail artboard's "Kde to je" card. */
interface TimelineStep {
  key: string;
  done: boolean;
  current: boolean;
  meta?: string;
  danger?: boolean;
}

/**
 * One row of the price card. A discount is negative and reads in the accent.
 *
 * A row is labelled by EITHER a name the server sent (a package, the services)
 * or a translation key the template pipes. Resolving the key here instead would
 * freeze the label at the language the order loaded in: a computed re-runs when
 * a signal it read changes, and `translate.instant` is not one.
 */
interface PriceLine {
  label?: string;
  labelKey?: string;
  amount: number;
  discount?: boolean;
}

/**
 * How the cleaner gets in. Two halves that are read together: the slug is the
 * SHAPE of the answer ("keys_handover") and the instructions are the DETAIL
 * ("with the neighbour in apt. 11"). -> Order.AccessMode
 */
interface EntryDetail {
  modeKey?: string;
  detail?: string;
}

@Component({
  selector: 'cleansia-customer-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    SkeletonModule,
    CleansiaButtonComponent,
    OrderStatusLabelPipe,
    OrderPreferredOfferComponent,
  ],
  providers: [OrderDetailFacade, OrderPreferredOfferFacade],
  templateUrl: './order-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly facade = inject(OrderDetailFacade);
  protected readonly preferredOffer = inject(OrderPreferredOfferFacade);

  // Re-expose facade signals so existing template bindings keep working.
  readonly order = this.facade.order;
  readonly loading = this.facade.loading;
  readonly error = this.facade.error;
  readonly membership = this.facade.membership;
  readonly reviewSubmitting = this.facade.reviewSubmitting;
  readonly downloading = this.facade.downloading;

  // Rating
  reviewRating = signal(0);

  /**
   * PER-ITEM SCORES — the second half of the original ask, and the half one overall number cannot
   * carry: "the oven was spotless, the bathroom was skipped" is two facts, and a single 3 stars says
   * neither of them.
   *
   * <p>Entirely optional and closed by default. The overall rating stays the required headline: a
   * customer who wants to leave five stars and go should never meet this list.</p>
   */
  readonly reviewLinesOpen = signal(false);

  readonly reviewLineOptions = computed(() =>
    buildReviewLineOptions(this.order(), (name, translations) => {
      const lang = this.translate.currentLang || this.translate.getDefaultLang();
      return translations?.[lang]?.name || name || '';
    }),
  );

  /** key -> 1..5. Absent means "not scored", which is different from scored badly. */
  readonly reviewLineScores = signal<ReadonlyMap<string, number>>(new Map());
  reviewComment = signal('');
  reviewHover = signal(0);
  isCompleted = computed(
    () => this.order()?.orderStatus?.value === OrderStatus.Completed
  );
  isInProgress = computed(
    () => this.order()?.orderStatus?.value === OrderStatus.InProgress
  );
  isCancelled = computed(
    () => this.order()?.orderStatus?.value === OrderStatus.Cancelled
  );
  hasReview = computed(() => !!this.order()?.review);

  // ---------------------------------------------------------------------------
  // The two axes. -> "Objednavky zakaznika" board, Detail artboard, note "axes"
  //
  // The board's whole argument: an order's state is TWO independent things, and
  // one timeline cannot say them. Order.CurrentStatus tracks the cleaning;
  // PaymentStatus x PaymentType tracks the money. Confirmed means EITHER "the
  // money arrived" OR "a cleaner took it" — a cash order is Confirmed without a
  // koruna moving. And "cleaner assigned" is driven by AssignedEmployees, never
  // by Confirmed: a card order is Confirmed the moment Stripe says so and may
  // have nobody on it. -> /domain/order-lifecycle
  // ---------------------------------------------------------------------------

  /** When each status was reached, from the order's own history. */
  private statusReachedAt(status: OrderStatus): Date | undefined {
    return this.order()?.statusHistory?.find((t) => t.status?.value === status)?.createdOn;
  }

  readonly cleaningSteps = computed<TimelineStep[]>(() => {
    const order = this.order();
    if (!order) return [];

    const current = order.orderStatus?.value;
    const cancelled = current === OrderStatus.Cancelled;
    const assignee = this.assignedCleaner();

    // A cancelled order stops where it stopped; the steps it never reached are
    // not "upcoming", they are not going to happen.
    const step = (
      status: OrderStatus,
      key: string,
      meta?: string,
    ): TimelineStep => {
      const at = this.statusReachedAt(status);
      const done = !!at || (current !== undefined && current > status && !cancelled);
      return {
        key,
        done,
        current: current === status,
        meta: meta ?? (at ? this.formatDate(at) : undefined),
      };
    };

    // Assignment is its own row, and it reads off the crew, not off Confirmed.
    // Both rows sit on OrderStatus.Confirmed, so exactly one of them is the
    // current one: with a crew on the order the furthest thing that happened is
    // the assignment, and Confirmed behind it is simply done.
    const assignedIsCurrent = !!assignee && current === OrderStatus.Confirmed;

    const confirmed = step(OrderStatus.Confirmed, 'confirmed');
    if (assignedIsCurrent) {
      confirmed.current = false;
      confirmed.done = true;
    }

    const steps: TimelineStep[] = [step(OrderStatus.New, 'received'), confirmed];

    steps.push({
      key: 'assigned',
      done: !!assignee,
      current: assignedIsCurrent,
      meta: assignee?.fullName,
    });

    steps.push(
      step(OrderStatus.OnTheWay, 'on_the_way'),
      step(OrderStatus.InProgress, 'in_progress'),
      step(OrderStatus.Completed, 'completed'),
    );

    if (cancelled) {
      steps.push({ key: 'cancelled', done: true, current: true, danger: true,
        meta: this.formatDate(this.statusReachedAt(OrderStatus.Cancelled)) });
    }

    return steps;
  });

  /**
   * The money's own axis. It has no timestamps of its own on the DTO, so the
   * rows carry state and no time rather than a time borrowed from the cleaning
   * axis, which would be a different event wearing the wrong label.
   */
  readonly paymentSteps = computed<TimelineStep[]>(() => {
    const order = this.order();
    if (!order) return [];

    const status = order.paymentStatus?.value;
    const paid = status === PaymentStatus.Paid;
    const refunded = status === PaymentStatus.Refunded;
    const failed = status === PaymentStatus.Failed;

    const steps: TimelineStep[] = [
      { key: 'awaiting', done: true, current: !paid && !refunded && !failed,
        meta: order.paymentType?.name },
    ];

    if (failed) {
      steps.push({ key: 'failed', done: true, current: true, danger: true });
    } else {
      steps.push({ key: 'paid', done: paid || refunded, current: paid });
    }

    if (refunded) {
      steps.push({ key: 'refunded', done: true, current: true });
    }

    return steps;
  });

  /** The board names one cleaner; the crew can be larger, so the rest are counted. */
  readonly assignedCleaner = computed(() => this.order()?.assignedEmployees?.[0]);

  readonly otherCrewCount = computed(() =>
    Math.max(0, (this.order()?.assignedEmployees?.length ?? 0) - 1),
  );

  /**
   * The price card. Packages carry their own price; SERVICES DO NOT — the DTO
   * has no per-service amount — so the services share one line for the balance
   * of the subtotal. Every figure here is one the server sent or the difference
   * between two of them; none is an allocation invented per service.
   */
  readonly priceLines = computed<PriceLine[]>(() => {
    const order = this.order();
    if (!order) return [];

    const lines: PriceLine[] = [];
    let packagesTotal = 0;

    for (const pkg of order.selectedPackages ?? []) {
      packagesTotal += pkg.price ?? 0;
      lines.push({ label: pkg.name ?? '', amount: pkg.price ?? 0 });
    }

    const serviceNames = (order.selectedServices ?? []).map((s) => s.name).filter(Boolean);
    const servicesTotal = (order.originalSubtotal ?? 0) - packagesTotal;
    if (serviceNames.length > 0) {
      lines.push({ label: serviceNames.join(', '), amount: servicesTotal });
    }

    const discount = (amount: number | undefined, labelKey: string) => {
      if (amount && amount > 0) {
        lines.push({ labelKey, amount: -amount, discount: true });
      }
    };
    discount(order.membershipDiscountAmount, 'pages.order_detail.discount_membership');
    discount(order.tierDiscountAmount, 'pages.order_detail.discount_tier');
    discount(order.promoDiscountAmount, 'pages.order_detail.discount_promo');

    return lines;
  });

  /**
   * Where the cleaner gets in.
   *
   * This used to be `accessInstructions || accessMode`, which was wrong in both
   * directions: with instructions present the mode vanished, and without them
   * the RAW SLUG reached the page — the customer read "keys_handover". The two
   * are halves of one answer and the domain says so; the slug's four values
   * have had copy under `pages.order.access_mode.*` all along, which is where
   * the wizard reads them from. -> Order.AccessMode
   */
  readonly entry = computed<EntryDetail | undefined>(() => {
    const order = this.order();
    if (!order) return undefined;

    const mode = order.accessMode?.trim();
    const detail = order.accessInstructions?.trim();
    if (!mode && !detail) return undefined;

    // No guard on the slug: CreateOrder's validator accepts exactly the four
    // that have copy, so an unknown one cannot be in the database. Checking
    // here with `instant` would also read a key before the locale file has
    // loaded and drop a mode that is perfectly fine.
    return {
      modeKey: mode ? `pages.order.access_mode.${mode}` : undefined,
      detail: detail || undefined,
    };
  });

  /**
   * How long free cancellation lasted. The window is a MEMBERSHIP benefit, so
   * it is read off the membership the customer actually has rather than
   * hardcoded — Plus shortens it. -> /product/business-rules
   */
  /** "Petra S." -> "PS". Same shape the profile rail uses for an avatar. */
  initialsOf(fullName: string | undefined): string {
    return (fullName ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join('');
  }

  /** Street, city and the floor/apartment the crew needs to find the door. */
  readonly addressLine = computed(() => {
    const order = this.order();
    if (!order) return '';
    const address = order.address;
    const base = [address?.street, address?.city, address?.zipCode].filter(Boolean).join(', ');
    const inside = [order.customerFloor, order.customerApartment].filter(Boolean).join(', ');
    return inside ? `${base} · ${inside}` : base;
  });

  readonly freeCancellationHours = computed(
    () => this.membership()?.freeCancellationWindowHours ?? 24,
  );
  stars = [1, 2, 3, 4, 5];

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    this.preferredOffer.connect({
      order: this.order,
      // The offer state is server-derived, so the only way to render what the choice produced is to
      // read the order again.
      onChosen: () => orderId && this.loadOrder(orderId),
    });
    if (orderId) {
      this.loadOrder(orderId);
    }
    this.facade.loadMembership();
  }

  /**
   * Path B entry — stash the order's services/packages/rooms/payment/time
   * in sessionStorage and navigate into the recurring wizard. Non-Plus
   * users are routed to the subscribe page first so they don't fill out a
   * schedule the server will reject; the entitlement itself is enforced in
   * CreateRecurringBooking.Handler, not here.
   */
  makeRecurring(): void {
    const order = this.order();
    if (!order?.id) return;

    const isPlus = this.membership()?.hasMembership === true;
    if (!isPlus) {
      this.facade.showRecurringPlusRequired();
      this.router.navigate([CleansiaCustomerRoute.PLUS]);
      return;
    }

    // Derive HH:mm from the order's cleaningDateTime in the user's local TZ.
    let timeOfDay: string | null = null;
    if (order.cleaningDateTime) {
      const dt = new Date(order.cleaningDateTime);
      if (!Number.isNaN(dt.getTime())) {
        const hh = String(dt.getHours()).padStart(2, '0');
        const mm = String(dt.getMinutes()).padStart(2, '0');
        timeOfDay = `${hh}:${mm}`;
      }
    }

    const prefill: RecurringPrefillParams = {
      selectedServiceIds: (order.selectedServices || [])
        .map((s) => s.id)
        .filter((id): id is string => !!id),
      selectedPackageIds: (order.selectedPackages || [])
        .map((p) => p.id)
        .filter((id): id is string => !!id),
      selectedServiceNames: (order.selectedServices || []).map((s) => s.name || ''),
      selectedPackageNames: (order.selectedPackages || []).map((p) => p.name || ''),
      rooms: order.rooms ?? 0,
      bathrooms: order.bathrooms ?? 0,
      paymentType: order.paymentType?.value ?? 1,
      timeOfDay,
    };

    if (this.isBrowser) {
      sessionStorage.setItem(RECURRING_PREFILL_STORAGE_KEY, JSON.stringify(prefill));
    }
    this.router.navigate(
      [CleansiaCustomerRoute.MEMBERSHIP, 'recurring', 'create'],
      { queryParams: { prefill: 'true' } },
    );
  }

  loadOrder(orderId: string): void {
    this.facade.loadOrder(orderId);
  }

  goBack(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDERS]);
  }

  downloadReceipt(): void {
    this.facade.downloadReceipt();
  }

  reportIssue(): void {
    const order = this.order();
    if (!order?.id) return;
    this.router.navigate([CleansiaCustomerRoute.DISPUTES], {
      queryParams: { orderId: order.id },
    });
  }

  setRating(star: number): void {
    this.reviewRating.set(star);
  }

  setReviewLineScore(option: ReviewLineOption, rating: number): void {
    const next = new Map(this.reviewLineScores());
    // Pressing the star you already chose clears it — the only way back to "not scored" once a row
    // has been touched, and without it a mis-click is permanent.
    if (next.get(option.key) === rating) {
      next.delete(option.key);
    } else {
      next.set(option.key, rating);
    }
    this.reviewLineScores.set(next);
  }

  reviewLineScoreOf(option: ReviewLineOption): number {
    return this.reviewLineScores().get(option.key) ?? 0;
  }

  toggleReviewLines(): void {
    this.reviewLinesOpen.update((open) => !open);
  }

  submitReview(): void {
    // Only rows the customer actually scored. An unscored row is not a zero — the server would reject
    // a rating outside 1..5, and "not scored" is a real answer that simply carries no line.
    const scores = this.reviewLineScores();
    const lines = this.reviewLineOptions()
      .filter((option) => scores.has(option.key))
      .map((option) => ({
        serviceId: option.serviceId,
        packageId: option.packageId,
        rating: scores.get(option.key)!,
      }));

    this.facade.submitReview(this.reviewRating(), this.reviewComment(), lines);
  }

  protected getLocale(): string {
    const localeMap: Record<string, string> = {
      cs: 'cs-CZ',
      en: 'en-US',
      sk: 'sk-SK',
      uk: 'uk-UA',
      ru: 'ru-RU',
    };
    return localeMap[this.translate.currentLang] || 'en-US';
  }

  formatDate(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString(this.getLocale(), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(price: number | undefined): string {
    if (price == null) return '';
    const code = this.order()?.currency?.code || 'CZK';
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: code,
      minimumFractionDigits: 0,
    }).format(price);
  }

  /**
   * The translation key for a PLATFORM cancellation, or null when there is nothing to say.
   *
   * Only the platform's own reasons arrive on the wire: the backend populates
   * `systemCancellationReason` solely when `CancelledBy` is `System`, because the same column also
   * carries an admin's free-text note written for other staff. That gating is server-side, so this
   * never has to judge whether a value is safe to render.
   *
   * An unrecognised key returns null rather than printing itself. The API ships independently of
   * this app, so a newer server can name a reason this build has never heard of — the customer
   * already sees the Cancelled status either way, and a raw `order.cancelled.something` on screen
   * costs more trust than a missing sentence.
   *
   * Mirrors `Cleansia.Core.Domain.Orders.OrderCancellationReasons`, iOS `CancellationReasonCopy` and
   * Android `cancellationReasonText`.
   */
  protected cancellationReasonKey(): string | null {
    const reason = this.order()?.systemCancellationReason;
    switch (reason) {
      case 'order.cancelled.payment_not_completed':
        return 'pages.order_detail.cancellation_reason.payment_not_completed';
      case 'order.cancelled.recurring_not_confirmed':
        return 'pages.order_detail.cancellation_reason.recurring_not_confirmed';
      default:
        return null;
    }
  }
}
