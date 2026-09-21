import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaCalendarComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
  TableColumn,
} from '@cleansia/components';
import {
  EmployeePayrollSummary,
  MonthlyPayroll,
  PayrollByStatus,
  RevenueByPackage,
  RevenueByPaymentType,
  RevenueByService,
} from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { TranslatePipe } from '@ngx-translate/core';
import { Tabs } from 'primeng/tabs';
import { TabList } from 'primeng/tabs';
import { Tab } from 'primeng/tabs';
import { TabPanels } from 'primeng/tabs';
import { TabPanel } from 'primeng/tabs';
import { TooltipModule } from 'primeng/tooltip';
import { ReportsFacade, ReportType } from './reports.facade';

@Component({
  selector: 'cleansia-admin-reports',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    TooltipModule,
    CleansiaCalendarComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
  ],
  templateUrl: './reports.component.html',
  providers: [ReportsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReportsComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(ReportsFacade);

  readonly currencyOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.currencies().map((c) => ({ label: c.code, value: c.id }))
  );

  protected readonly tables = computed(() => {
    this.facade.lang();
    return this.buildTables();
  });

  ngOnInit(): void {
    this.facade.loadCurrencies();
    this.facade.loadRevenueReport();
  }

  private buildTables() {
    const revenueByService: TableColumn<RevenueByService>[] = [
      {
        id: 'serviceName',
        field: 'serviceName',
        header: this.translate.instant('pages.reports.service_name'),
      },
      {
        id: 'orderCount',
        field: 'orderCount',
        header: this.translate.instant('pages.reports.order_count'),
      },
      {
        id: 'totalRevenue',
        field: 'totalRevenue',
        header: this.translate.instant('pages.reports.total_revenue'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.totalRevenue),
      },
    ];

    const revenueByPackage: TableColumn<RevenueByPackage>[] = [
      {
        id: 'packageName',
        field: 'packageName',
        header: this.translate.instant('pages.reports.package_name'),
      },
      {
        id: 'orderCount',
        field: 'orderCount',
        header: this.translate.instant('pages.reports.order_count'),
      },
      {
        id: 'totalRevenue',
        field: 'totalRevenue',
        header: this.translate.instant('pages.reports.total_revenue'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.totalRevenue),
      },
    ];

    const revenueByPaymentType: TableColumn<RevenueByPaymentType>[] = [
      {
        id: 'paymentTypeName',
        field: 'paymentTypeName',
        header: this.translate.instant('pages.reports.payment_type'),
      },
      {
        id: 'orderCount',
        field: 'orderCount',
        header: this.translate.instant('pages.reports.order_count'),
      },
      {
        id: 'totalRevenue',
        field: 'totalRevenue',
        header: this.translate.instant('pages.reports.total_revenue'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.totalRevenue),
      },
      // Revenue is the SALE and stays gross: credit is a tender, not a discount. The next two say how
      // the sale was SETTLED, the two after how much of it went back, and the last one — what the
      // tender took less what it gave back, the card leg only — is the line that reconciles against
      // a Stripe statement. The gateway never saw the credit, so the credit leg is shown beside it
      // and netted only in the headline.
      {
        id: 'settledFromCredit',
        field: 'settledFromCredit',
        header: this.translate.instant('pages.reports.settled_from_credit'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.settledFromCredit),
      },
      {
        id: 'settledOnTender',
        field: 'settledOnTender',
        header: this.translate.instant('pages.reports.settled_on_tender'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.settledOnTender),
      },
      {
        id: 'refundedToCard',
        field: 'refundedToCard',
        header: this.translate.instant('pages.reports.refunded_to_card'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.refundedToCard),
      },
      {
        id: 'returnedToCredit',
        field: 'returnedToCredit',
        header: this.translate.instant('pages.reports.returned_to_credit'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.returnedToCredit),
      },
      {
        id: 'netOnTender',
        field: 'netOnTender',
        header: this.translate.instant('pages.reports.net_on_tender'),
        getValue: (row) => this.facade.formatRevenueAmount(row?.netOnTender),
      },
    ];

    const employeeSummaries: TableColumn<EmployeePayrollSummary>[] = [
      {
        id: 'employeeName',
        field: 'employeeName',
        header: this.translate.instant('pages.reports.employee_name'),
      },
      {
        id: 'totalOrders',
        field: 'totalOrders',
        header: this.translate.instant('pages.reports.total_orders'),
      },
      {
        id: 'invoiceCount',
        field: 'invoiceCount',
        header: this.translate.instant('pages.reports.invoice_count'),
      },
      {
        id: 'subTotal',
        field: 'subTotal',
        header: this.translate.instant('pages.reports.subtotal'),
        getValue: (row) => this.facade.formatPayrollAmount(row?.subTotal),
      },
      {
        id: 'bonusAmount',
        field: 'bonusAmount',
        header: this.translate.instant('pages.reports.bonus'),
        getValue: (row) => this.facade.formatPayrollAmount(row?.bonusAmount),
      },
      {
        id: 'deductionAmount',
        field: 'deductionAmount',
        header: this.translate.instant('pages.reports.deductions'),
        getValue: (row) => this.facade.formatPayrollAmount(row?.deductionAmount),
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: this.translate.instant('pages.reports.total_amount'),
        getValue: (row) => this.facade.formatPayrollAmount(row?.totalAmount),
      },
    ];

    const payrollByStatus: TableColumn<PayrollByStatus>[] = [
      {
        id: 'statusName',
        field: 'statusName',
        header: this.translate.instant('pages.reports.status'),
      },
      {
        id: 'invoiceCount',
        field: 'invoiceCount',
        header: this.translate.instant('pages.reports.invoice_count'),
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: this.translate.instant('pages.reports.total_amount'),
        getValue: (row) => this.facade.formatPayrollAmount(row?.totalAmount),
      },
    ];

    const monthlyPayroll: TableColumn<MonthlyPayroll>[] = [
      {
        id: 'month',
        field: 'monthName',
        header: this.translate.instant('pages.reports.month'),
        getValue: (row) => `${row?.monthName} ${row?.year}`,
      },
      {
        id: 'invoiceCount',
        field: 'invoiceCount',
        header: this.translate.instant('pages.reports.invoice_count'),
      },
      {
        id: 'totalAmount',
        field: 'totalAmount',
        header: this.translate.instant('pages.reports.total_amount'),
        getValue: (row) => this.facade.formatPayrollAmount(row?.totalAmount),
      },
    ];

    return {
      revenueByService,
      revenueByPackage,
      revenueByPaymentType,
      employeeSummaries,
      payrollByStatus,
      monthlyPayroll,
    };
  }

  activeTab: ReportType = 'revenue';

  onTabChange(value: string | number | undefined): void {
    if (value === undefined) return;
    const tab = value as ReportType;
    this.activeTab = tab;
    this.facade.setActiveTab(tab);
  }
}
