import { isPlatformBrowser } from '@angular/common';
import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import {
  CustomerClient,
  GetMyServingCleanersResponse,
  PreferredCleanerOption,
  survivingPreferredSelection,
  toPreferredCleanerOptions,
} from '@cleansia/customer-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { OrderWizardFormData } from './order-wizard.models';

/** Dependencies the preferred-cleaner collaborator reads from the orchestrating wizard facade. */
interface PreferredCleanerConnection {
  isAuthenticated: () => boolean;
  hasMembership: () => boolean;
  currentFormData: () => OrderWizardFormData;
  patchFormData: (partial: Partial<OrderWizardFormData>) => void;
}

/**
 * The Plus-only picker that lets a customer ask for a cleaner who has already cleaned for them.
 *
 * The roster is `GET /api/Order/MyServingCleaners`, the same call both mobile clients make, asked
 * with the slot and the selection so the server can answer the availability question about the
 * booking actually being composed rather than about nothing. A cleaner it cannot take is shown and
 * unselectable; the server would not withhold a seat for them, and offering the row would produce a
 * booking whose preference is silently dropped.
 *
 * A failed read degrades to a hidden picker rather than to a banner: the perk is an enrichment on
 * the checkout screen, and a red box there costs more than the picker is worth.
 */
@Injectable()
export class OrderPreferredCleanerFacade extends UnsubscribeControlDirective {
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private deps: PreferredCleanerConnection | null = null;

  readonly loading = signal(false);
  readonly loadFailed = signal(false);
  readonly cleaners = signal<GetMyServingCleanersResponse[]>([]);

  readonly options = computed<PreferredCleanerOption[]>(() =>
    toPreferredCleanerOptions(
      this.cleaners(),
      this.translate.instant('preferred_cleaner.unavailable')
    )
  );

  readonly visible = computed(() => this.cleaners().length > 0);

  connect(deps: PreferredCleanerConnection): void {
    this.deps = deps;
  }

  selectedEmployeeId(): string | null {
    return this.deps?.currentFormData().preferredEmployeeId ?? null;
  }

  refresh(): void {
    const deps = this.deps;
    if (!this.isBrowser || !deps || !deps.isAuthenticated() || !deps.hasMembership()) {
      return;
    }

    const data = deps.currentFormData();
    this.loading.set(true);
    this.loadFailed.set(false);
    this.customerClient.orderClient
      .myServingCleaners(
        composeSlotInstant(data),
        data.selectedServiceIds,
        data.selectedPackageIds
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((roster) => {
        if (!roster) {
          this.loadFailed.set(true);
          this.cleaners.set([]);
          this.select(null);
          return;
        }
        this.cleaners.set(roster);
        this.select(survivingPreferredSelection(roster, this.selectedEmployeeId()));
      });
  }

  select(employeeId: string | null): void {
    this.deps?.patchFormData({ preferredEmployeeId: employeeId });
  }
}

/**
 * The slot as a local-clock instant, built the same way `submitOrder` builds the one it books, so
 * the availability answer is about the booking the customer is about to make. Undefined until a date
 * is picked — the endpoint's three slot fields are optional and an unanswered question renders as an
 * unmarked row rather than as a guess.
 */
function composeSlotInstant(data: OrderWizardFormData): Date | undefined {
  if (!data.cleaningDate) {
    return undefined;
  }

  const selectedDate = new Date(data.cleaningDate);
  const [hours, minutes] = data.cleaningTime.split(':').map(Number);

  return new Date(
    selectedDate.getFullYear(),
    selectedDate.getMonth(),
    selectedDate.getDate(),
    hours,
    minutes,
    0,
    0
  );
}
