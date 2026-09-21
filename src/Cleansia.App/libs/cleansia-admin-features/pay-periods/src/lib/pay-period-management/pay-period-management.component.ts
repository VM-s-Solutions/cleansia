import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PayPeriodDto, PayPeriodStatus } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaRadioComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { PayPeriodManagementFacade } from './pay-period-management.facade';
import {
  getPayPeriodTableColumns,
  getPayPeriodTableActions,
} from './pay-period-management.models';

@Component({
  selector: 'cleansia-admin-pay-period-management',
  standalone: true,
  imports: [
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaRadioComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    DialogModule,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
  ],
  templateUrl: './pay-period-management.component.html',
  providers: [PayPeriodManagementFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PayPeriodManagementComponent implements OnInit {
  private readonly router = inject(Router);
  protected readonly facade = inject(PayPeriodManagementFacade);
  private readonly translate = inject(TranslateService);

  private readonly statusTemplate = viewChild<TemplateRef<PayPeriodDto>>('statusTemplate');

  readonly PayPeriodStatus = PayPeriodStatus;

  protected readonly table = computed(() => {
    this.facade.lang();
    return {
      columns: getPayPeriodTableColumns(this.translate, this.statusTemplate()),
      actions: getPayPeriodTableActions(
        {
          onViewDetails: (row) => this.viewPayPeriodDetails(row),
          onClose: (row) => this.closePayPeriod(row),
        },
        this.translate
      ),
    };
  });

  showCreateDialog = signal(false);
  createStartDate = signal<Date | null>(null);
  createEndDate = signal<Date | null>(null);

  ngOnInit(): void {
    this.facade.loadPayPeriods();
  }

  viewPayPeriodDetails(payPeriod: PayPeriodDto): void {
    this.router.navigate([CleansiaAdminRoute.PAY_PERIODS, payPeriod.id]);
  }

  closePayPeriod(payPeriod: PayPeriodDto): void {
    const payPeriodId = payPeriod.id;
    if (!payPeriodId || !confirm(this.translate.instant('pay_periods.confirm_close'))) {
      return;
    }
    this.facade.closePayPeriod(payPeriodId);
  }

  openCreateDialog(): void {
    this.showCreateDialog.set(true);
    this.createStartDate.set(null);
    this.createEndDate.set(null);
  }

  closeCreateDialog(): void {
    this.showCreateDialog.set(false);
  }

  createPayPeriod(): void {
    const startDate = this.createStartDate();
    const endDate = this.createEndDate();
    if (!startDate || !endDate) return;

    // TODO: Wire up to admin client create pay period endpoint once available
    console.warn('Create pay period not yet wired to backend', { startDate, endDate });
    this.closeCreateDialog();
  }
}
