import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { PayConfigListItem } from './pay-config-management.models';
import { AdminPayConfigService } from '../admin-pay-config.service';

export interface PayConfigFilterParams {
  serviceId?: string;
  packageId?: string;
}

@Injectable()
export class PayConfigManagementFacade extends UnsubscribeControlDirective {
  private readonly payConfigService = inject(AdminPayConfigService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly payConfigs = signal<PayConfigListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<PayConfigFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);

  loadPayConfigs(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.payConfigService
      .getPaged(
        this.currentOffset(),
        this.currentLimit(),
        filterParams?.serviceId,
        filterParams?.packageId
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.payConfigs.set(response.data || []);
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
    this.loadPayConfigs();
  }

  applyFilter(filter: PayConfigFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadPayConfigs();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadPayConfigs();
  }

  formatCurrency(value: number | undefined): string {
    if (value === undefined || value === null) return '';
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: 'CZK',
    }).format(value);
  }

  navigateToCreate(): void {
    this.router.navigate([CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT, 'create']);
  }

  navigateToEdit(payConfig: PayConfigListItem): void {
    if (payConfig.id) {
      this.router.navigate([CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT, payConfig.id, 'edit']);
    }
  }

  deletePayConfig(payConfig: PayConfigListItem): void {
    if (!payConfig.id) return;

    this.payConfigService
      .delete(payConfig.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.pay_config_management.messages.delete_success')
          );
          this.loadPayConfigs();
        }
      });
  }
}
