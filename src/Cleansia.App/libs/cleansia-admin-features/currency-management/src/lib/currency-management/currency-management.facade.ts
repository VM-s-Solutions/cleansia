import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, CurrencyListItem } from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class CurrencyManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly currencies = signal<CurrencyListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  loadCurrencies(): void {
    this.loading.set(true);

    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of([])),
        finalize(() => this.loading.set(false))
      )
      .subscribe((currencies) => {
        this.currencies.set(currencies);
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  navigateToCreateCurrency(): void {
    this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT, 'create']);
  }

  navigateToEditCurrency(currency: CurrencyListItem): void {
    if (currency.id) {
      this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT, currency.id, 'edit']);
    }
  }

  deleteCurrency(currency: CurrencyListItem): void {
    if (!currency.id) return;

    this.adminClient.adminCurrencyClient
      .delete(currency.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.currency_management.messages.delete_success'
            )
          );
          this.loadCurrencies();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
