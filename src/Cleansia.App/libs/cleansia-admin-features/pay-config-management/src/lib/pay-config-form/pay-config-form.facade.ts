import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { AdminClient } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  AdminPayConfigService,
  CreatePayConfigCommand,
  UpdatePayConfigCommand,
} from '../admin-pay-config.service';
import { PayConfigListItem } from '../pay-config-management/pay-config-management.models';

export interface ServiceOption {
  id: string;
  name: string;
}

export interface PackageOption {
  id: string;
  name: string;
}

export interface CurrencyOption {
  id: string;
  code: string;
}

export interface PayConfigFormData {
  serviceId?: string;
  packageId?: string;
  basePay: number;
  extraPerRoom: number;
  extraPerBathroom: number;
  distanceRatePerKm: number;
  minimumPay: number;
  maximumPay: number;
  currencyId: string;
  description?: string;
}

@Injectable()
export class PayConfigFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly payConfigService = inject(AdminPayConfigService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly payConfig = signal<PayConfigListItem | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly services = signal<ServiceOption[]>([]);
  readonly packages = signal<PackageOption[]>([]);
  readonly currencies = signal<CurrencyOption[]>([]);

  loadPayConfig(payConfigId: string): void {
    this.loading.set(true);

    this.payConfigService
      .getById(payConfigId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.payConfig.set(response);
        } else {
          this.router.navigate([CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT]);
        }
      });
  }

  loadServices(): void {
    this.adminClient.adminServiceClient
      .getPaged(undefined, undefined, 0, 100)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response?.data) {
          this.services.set(
            response.data
              .filter((s) => s.id && s.name)
              .map((s) => ({ id: s.id!, name: s.name! }))
          );
        }
      });
  }

  loadPackages(): void {
    this.adminClient.adminPackageClient
      .getPaged(undefined, undefined, 0, 100)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response?.data) {
          this.packages.set(
            response.data
              .filter((p) => p.id && p.name)
              .map((p) => ({ id: p.id!, name: p.name! }))
          );
        }
      });
  }

  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([]))
      )
      .subscribe((currencies) => {
        if (currencies) {
          this.currencies.set(
            currencies
              .filter((c) => c.id && c.code)
              .map((c) => ({ id: c.id!, code: c.code! }))
          );
        }
      });
  }

  createPayConfig(data: PayConfigFormData): void {
    this.saving.set(true);

    const command: CreatePayConfigCommand = {
      serviceId: data.serviceId || undefined,
      packageId: data.packageId || undefined,
      basePay: data.basePay,
      extraPerRoom: data.extraPerRoom,
      extraPerBathroom: data.extraPerBathroom,
      distanceRatePerKm: data.distanceRatePerKm,
      minimumPay: data.minimumPay,
      maximumPay: data.maximumPay,
      currencyId: data.currencyId,
      description: data.description || undefined,
    };

    this.payConfigService
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.pay_config_form.messages.create_success')
          );
          this.router.navigate([CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT]);
        }
      });
  }

  updatePayConfig(payConfigId: string, data: PayConfigFormData): void {
    this.saving.set(true);

    const command: UpdatePayConfigCommand = {
      payConfigId,
      basePay: data.basePay,
      extraPerRoom: data.extraPerRoom,
      extraPerBathroom: data.extraPerBathroom,
      distanceRatePerKm: data.distanceRatePerKm,
      minimumPay: data.minimumPay,
      maximumPay: data.maximumPay,
      description: data.description || undefined,
    };

    this.payConfigService
      .update(payConfigId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.pay_config_form.messages.update_success')
          );
          this.router.navigate([CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.PAY_CONFIG_MANAGEMENT]);
  }
}
