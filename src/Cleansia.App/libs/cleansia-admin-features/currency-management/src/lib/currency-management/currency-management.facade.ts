import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient, AdminCurrencyListItem } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class CurrencyManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
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
    const id = currency.id;
    if (!id || currency.isDefault) return;

    this.dialog
      .confirmTranslated(
        'pages.currency_management.set_default_confirm',
        'pages.currency_management.set_default',
        { code: currency.code },
        { icon: 'pi pi-star' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.setDefaultCurrencyConfirmed(id));
  }

  private setDefaultCurrencyConfirmed(id: string): void {
    this.adminClient.adminCurrencyClient
      .setDefault(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.currency_management.messages.set_default_success'
          );
          this.loadCurrencies();
        }
      });
  }

  deactivateCurrency(currency: AdminCurrencyListItem): void {
    const id = currency.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.currency_management.deactivate_confirm',
        'pages.currency_management.deactivate',
        { code: currency.code }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deactivateCurrencyConfirmed(id));
  }

  private deactivateCurrencyConfirmed(id: string): void {
    this.adminClient.adminCurrencyClient
      .deactivate(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.currency_management.messages.deactivate_success'
          );
          this.loadCurrencies();
        }
      });
  }

  activateCurrency(currency: AdminCurrencyListItem): void {
    const id = currency.id;
    if (!id) return;

    this.dialog
      .confirmTranslated(
        'pages.currency_management.activate_confirm',
        'pages.currency_management.activate',
        { code: currency.code },
        { icon: 'pi pi-check-circle' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.activateCurrencyConfirmed(id));
  }

  private activateCurrencyConfirmed(id: string): void {
    this.adminClient.adminCurrencyClient
      .activate(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.currency_management.messages.activate_success'
          );
          this.loadCurrencies();
        }
      });
  }

  deleteCurrency(currency: AdminCurrencyListItem): void {
    const id = currency.id;
    if (!id) return;
    if (currency.isDefault) {
      this.snackbarService.showErrorTranslated('pages.currency_management.cannot_delete_default');
      return;
    }

    this.dialog
      .confirmTranslated(
        'pages.currency_management.delete_confirm',
        'pages.currency_management.delete_currency',
        undefined,
        { danger: true, acceptLabelKey: 'global.actions.delete' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => this.deleteCurrencyConfirmed(id));
  }

  private deleteCurrencyConfirmed(id: string): void {
    this.adminClient.adminCurrencyClient
      .delete(id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response: unknown) => {
        if (response) {
          this.snackbarService.showSuccessTranslated(
            'pages.currency_management.messages.delete_success'
          );
          this.loadCurrencies();
        }
      });
  }
}
