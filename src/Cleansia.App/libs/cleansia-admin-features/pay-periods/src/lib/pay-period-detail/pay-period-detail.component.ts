import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { PayPeriodStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, Policy } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { AdminPayPeriodOpsComponent } from './components';
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
    CleansiaStatusBadgeComponent,
    ToastModule,
    CleansiaPermissionDirective,
    AdminPayPeriodOpsComponent,
  ],
  templateUrl: './pay-period-detail.component.html',
  providers: [PayPeriodDetailFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PayPeriodDetailComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(PayPeriodDetailFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly PayPeriodStatus = PayPeriodStatus;
  protected readonly Policy = Policy;

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

  formatDate(date: string | Date | null | undefined): string {
    return this.facade.formatDate(date);
  }

  formatDateTime(date: string | Date | null | undefined): string {
    return this.facade.formatDateTime(date);
  }

  formatClosedBy(closedBy: string | undefined): string {
    return this.facade.formatClosedBy(closedBy);
  }

  formatDuration(days: number): string {
    return this.facade.formatDuration(days);
  }

  viewInvoices(payPeriodId: string | undefined): void {
    this.router.navigate([CleansiaAdminRoute.INVOICE_MANAGEMENT], {
      queryParams: { payPeriodId },
    });
  }

  onOpsChanged(): void {
    const payPeriodId = this.facade.payPeriod()?.id;
    if (payPeriodId) {
      this.facade.loadPayPeriodDetail(payPeriodId);
    }
  }
}
