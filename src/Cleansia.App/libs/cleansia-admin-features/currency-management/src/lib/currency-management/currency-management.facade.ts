import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, AdminCurrencyListItem } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { resolveCurrencyErrorKey } from './currency-management.models';

@Injectable()
export class CurrencyManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly currencies = signal<AdminCurrencyListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  loadCurrencies(): void {
    this.loading.set(true);

    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([])),
        finalize(() => this.loading.set(false))
      )
      .subscribe((currencies) => {
        // `?? []` — the generated client can put a NULL in a signal typed as an array; reasoned out
        // in service-management/service-form.facade.ts. Nothing dereferences it here, so the null
        // would travel as far as the currency table before it threw.
        this.currencies.set(currencies ?? []);
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  navigateToCreateCurrency(): void {
    this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT, 'create']);
  }

  navigateToEditCurrency(currency: AdminCurrencyListItem): void {
    if (currency.id) {
      this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT, currency.id, 'edit']);
    }
  }

  setDefaultCurrency(currency: AdminCurrencyListItem): void {
    if (!currency.id || currency.isDefault) return;

    this.adminClient.adminCurrencyClient
      .setDefault(currency.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveCurrencyErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.currency_management.messages.set_default_success'
            )
          );
          this.loadCurrencies();
        }
      });
  }

  deactivateCurrency(currency: AdminCurrencyListItem): void {
    if (!currency.id) return;

    this.adminClient.adminCurrencyClient
      .deactivate(currency.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveCurrencyErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.currency_management.messages.deactivate_success'
            )
          );
          this.loadCurrencies();
        }
      });
  }

  activateCurrency(currency: AdminCurrencyListItem): void {
    if (!currency.id) return;

    this.adminClient.adminCurrencyClient
      .activate(currency.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveCurrencyErrorKey(error))
          );
          return of(null);
        })
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.currency_management.messages.activate_success'
            )
          );
          this.loadCurrencies();
        }
      });
  }

  deleteCurrency(currency: AdminCurrencyListItem): void {
    if (!currency.id) return;

    this.adminClient.adminCurrencyClient
      .delete(currency.id)
      .pipe(
        takeUntil(this.destroyed$),
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
}
