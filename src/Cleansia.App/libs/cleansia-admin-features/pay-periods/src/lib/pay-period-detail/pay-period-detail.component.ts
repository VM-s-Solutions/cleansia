import { CommonModule } from '@angular/common';
import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PayPeriodStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { PayPeriodDetailFacade } from './pay-period-detail.facade';

@Component({
  selector: 'cleansia-admin-pay-period-detail',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    ToastModule,
  ],
  templateUrl: './pay-period-detail.component.html',
  providers: [PayPeriodDetailFacade, DialogService],
})
export class PayPeriodDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(PayPeriodDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  protected readonly PayPeriodStatus = PayPeriodStatus;

  ngOnInit(): void {
    const payPeriodId = this.route.snapshot.paramMap.get('id');
    if (payPeriodId) {
      this.facade.loadPayPeriodDetail(payPeriodId);
    } else {
      this.router.navigate([CleansiaAdminRoute.PAY_PERIODS]);
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.PAY_PERIODS]);
  }

  onClosePayPeriod(): void {
    const payPeriod = this.facade.payPeriod();
    if (!payPeriod?.id) return;

    if (confirm(this.translate.instant('pay_periods.confirm_close'))) {
      this.facade.closePayPeriod(payPeriod.id);
    }
  }

  formatDate(date: string | Date | null | undefined): string {
    return this.facade.formatDate(date);
  }

  formatDateTime(date: string | Date | null | undefined): string {
    return this.facade.formatDateTime(date);
  }

  getStatusClass(status: string | null | undefined): string {
    return this.facade.getStatusClass(status);
  }
}
