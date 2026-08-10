import { isPlatformBrowser } from '@angular/common';
import {
  computed,
  inject,
  Injectable,
  PLATFORM_ID,
  Signal,
  signal,
} from '@angular/core';
import {
  ChoosePreferredCleanerCommand,
  CustomerClient,
  GetMyServingCleanersResponse,
  OrderItem,
  PreferredCleanerOption,
  PreferredOfferState,
  toPreferredCleanerOptions,
} from '@cleansia/customer-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { resolvePreferredOfferView } from './order-preferred-offer.models';

interface PreferredOfferConnection {
  order: Signal<OrderItem | null>;
  onChosen: () => void;
}

/**
 * The customer's side of the reservation on their own order: what state it is in, and — once it has
 * ended — asking one more cleaner.
 *
 * The offer state is read verbatim off the order; `canChooseAnother` is the server's own gate
 * evaluated read-side, so a button offered here and a request the server accepts cannot disagree.
 * It is a snapshot and may go stale between render and tap, and the refusal is expected: the
 * command is the gate and the shared interceptor renders its key.
 */
@Injectable()
export class OrderPreferredOfferFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private deps: PreferredOfferConnection | null = null;
  private readonly order = signal<Signal<OrderItem | null> | null>(null);

  readonly picking = signal(false);
  readonly listLoading = signal(false);
  readonly listFailed = signal(false);
  readonly submitting = signal(false);
  readonly selectedEmployeeId = signal<string | null>(null);
  readonly cleaners = signal<GetMyServingCleanersResponse[]>([]);

  readonly offer = computed(() => resolvePreferredOfferView(this.order()?.() ?? null));

  readonly canChooseAnother = computed(() => this.offer().canChooseAnother);

  /** Silent for an order this perk never touched and can no longer touch. */
  readonly visible = computed(
    () => this.offer().state !== PreferredOfferState.None || this.canChooseAnother()
  );

  readonly options = computed<PreferredCleanerOption[]>(() =>
    toPreferredCleanerOptions(
      this.cleaners(),
      this.translate.instant('preferred_cleaner.unavailable')
    )
  );

  connect(deps: PreferredOfferConnection): void {
    this.deps = deps;
    this.order.set(deps.order);
  }

  openPicker(): void {
    if (!this.isBrowser || !this.canChooseAnother()) {
      return;
    }

    this.picking.set(true);
    this.selectedEmployeeId.set(null);
    this.loadRoster();
  }

  closePicker(): void {
    this.picking.set(false);
    this.selectedEmployeeId.set(null);
  }

  loadRoster(): void {
    const order = this.order()?.();
    if (!this.isBrowser || !order) {
      return;
    }

    this.listLoading.set(true);
    this.listFailed.set(false);
    this.customerClient.orderClient
      .myServingCleaners(
        order.cleaningDateTime,
        collectIds(order.selectedServices),
        collectIds(order.selectedPackages)
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.listLoading.set(false))
      )
      .subscribe((roster) => {
        if (!roster) {
          this.listFailed.set(true);
          this.cleaners.set([]);
          return;
        }
        this.cleaners.set(roster);
      });
  }

  select(employeeId: string | null): void {
    this.selectedEmployeeId.set(employeeId);
  }

  submit(): void {
    const order = this.order()?.();
    const employeeId = this.selectedEmployeeId();
    if (!order?.id || !employeeId || this.submitting()) {
      return;
    }

    const command = new ChoosePreferredCleanerCommand();
    command.orderId = order.id;
    command.employeeId = employeeId;

    this.submitting.set(true);
    this.customerClient.orderClient
      .choosePreferredCleaner(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (!response) {
          return;
        }
        this.closePicker();
        this.snackbar.showSuccess(
          this.translate.instant('preferred_cleaner.choose_success')
        );
        this.deps?.onChosen();
      });
  }
}

function collectIds(items: { id?: string }[] | undefined): string[] {
  return (items ?? []).map((item) => item.id).filter((id): id is string => !!id);
}
