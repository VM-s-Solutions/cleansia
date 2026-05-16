import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CreateCurrencyCommand,
  CurrencyDetailDto,
  UpdateCurrencyCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface CurrencyFormData {
  code: string;
  symbol: string;
  name: string;
  exchangeRate: number;
}

@Injectable()
export class CurrencyFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly currency = signal<CurrencyDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadCurrency(currencyId: string): void {
    this.loading.set(true);

    this.adminClient.adminCurrencyClient
      .details(currencyId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT]);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((currency) => {
        if (currency) {
          this.currency.set(currency);
        }
      });
  }

  createCurrency(data: CurrencyFormData): void {
    this.saving.set(true);

    const command = new CreateCurrencyCommand({
      code: data.code,
      symbol: data.symbol,
      name: data.name,
      exchangeRate: data.exchangeRate,
    });

    this.adminClient.adminCurrencyClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.currency_form.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT]);
        }
      });
  }

  updateCurrency(currencyId: string, data: CurrencyFormData): void {
    this.saving.set(true);

    const command = new UpdateCurrencyCommand({
      currencyId: currencyId,
      code: data.code,
      symbol: data.symbol,
      name: data.name,
      exchangeRate: data.exchangeRate,
    });

    this.adminClient.adminCurrencyClient
      .update(currencyId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.currency_form.messages.update_success')
          );
          this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.CURRENCY_MANAGEMENT]);
  }
}