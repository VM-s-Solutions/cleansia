import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminCurrencyListItem,
  ExtraListItem,
  SortDefinition,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Observable, catchError, finalize, map, of, switchMap, takeUntil, tap } from 'rxjs';
import { resolveExtraErrorKey } from './extra-management.models';

export interface ExtraFilterParams {
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class ExtraManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly extras = signal<ExtraListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<ExtraFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  readonly isActiveFilter = computed(() => this.currentFilter()?.isActive);

  /**
   * The currency the list's price column is in. The paged list shows the platform default currency's
   * row (ExtraListItem carries no code of its own -- it travels on both mobile specs), so the code is
   * read once from the currency overview rather than assumed. Null until known; the formatter then
   * prints the bare number rather than a currency it cannot name.
   */
  readonly defaultCurrencyCode = signal<string | null>(null);

  loadExtras(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    // The currency first, once, so the rows never render under a label that arrives later.
    const currency$: Observable<string | null> =
      this.defaultCurrencyCode() !== null
        ? of(this.defaultCurrencyCode())
        : this.adminClient.adminCurrencyClient.getOverview().pipe(
            catchError(() => of([] as AdminCurrencyListItem[])),
            map((currencies) => (currencies ?? []).find((c) => c.isDefault)?.code ?? null),
            tap((code) => this.defaultCurrencyCode.set(code))
          );

    currency$
      .pipe(
        switchMap(() =>
          this.adminClient.adminExtraClient.getPaged(
            filterParams?.searchTerm,
            filterParams?.isActive,
            this.currentSort(),
            this.currentOffset(),
            this.currentLimit()
          )
        ),
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.extras.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadExtras();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadExtras();
  }

  applyFilter(filter: ExtraFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadExtras();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadExtras();
  }

  formatCurrency(value: number | undefined): string {
    if (value === undefined || value === null) return '';
    const currencyCode = this.defaultCurrencyCode();
    if (!currencyCode) return String(value);
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: currencyCode,
    }).format(value);
  }

  navigateToCreateExtra(): void {
    this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT, 'create']);
  }

  navigateToEditExtra(extra: ExtraListItem): void {
    if (extra.id) {
      this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT, extra.id, 'edit']);
    }
  }

  deactivateExtra(extra: ExtraListItem): void {
    if (!extra.id) return;

    this.adminClient.adminExtraClient
      .deactivate(extra.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveExtraErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.extra_management.messages.deactivate_success'
            )
          );
          this.loadExtras();
        }
      });
  }

  activateExtra(extra: ExtraListItem): void {
    if (!extra.id) return;

    this.adminClient.adminExtraClient
      .activate(extra.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveExtraErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.extra_management.messages.activate_success'
            )
          );
          this.loadExtras();
        }
      });
  }

  deleteExtra(extra: ExtraListItem): void {
    if (!extra.id) return;

    this.adminClient.adminExtraClient
      .delete(extra.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveExtraErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.extra_management.messages.delete_success')
          );
          this.loadExtras();
        }
      });
  }
}
