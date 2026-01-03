import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';
import { TranslatePipe } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { Tabs } from 'primeng/tabs';
import { TabList } from 'primeng/tabs';
import { Tab } from 'primeng/tabs';
import { TabPanels } from 'primeng/tabs';
import { TabPanel } from 'primeng/tabs';
import { ReportsFacade, ReportType } from './reports.facade';

@Component({
  selector: 'cleansia-admin-reports',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaLanguageSwitcherComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './reports.component.html',
  providers: [ReportsFacade],
})
export class ReportsComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(ReportsFacade);

  dateRangeForm = this.fb.group({
    startDate: [this.facade.dateRange().startDate],
    endDate: [this.facade.dateRange().endDate],
  });

  // Revenue Tables
  revenueByServiceTableDef: TableDefinition = {
    columns: [
      {
        id: 'serviceName',
        headerName: this.translate.instant('pages.reports.service_name'),
        value: 'serviceName',
      },
      {
        id: 'orderCount',
        headerName: this.translate.instant('pages.reports.order_count'),
        value: 'orderCount',
      },
      {
        id: 'totalRevenue',
        headerName: this.translate.instant('pages.reports.total_revenue'),
        value: (row) => this.facade.formatCurrency(row?.totalRevenue),
      },
    ],
  };

  revenueByPackageTableDef: TableDefinition = {
    columns: [
      {
        id: 'packageName',
        headerName: this.translate.instant('pages.reports.package_name'),
        value: 'packageName',
      },
      {
        id: 'orderCount',
        headerName: this.translate.instant('pages.reports.order_count'),
        value: 'orderCount',
      },
      {
        id: 'totalRevenue',
        headerName: this.translate.instant('pages.reports.total_revenue'),
        value: (row) => this.facade.formatCurrency(row?.totalRevenue),
      },
    ],
  };

  revenueByPaymentTypeTableDef: TableDefinition = {
    columns: [
      {
        id: 'paymentTypeName',
        headerName: this.translate.instant('pages.reports.payment_type'),
        value: 'paymentTypeName',
      },
      {
        id: 'orderCount',
        headerName: this.translate.instant('pages.reports.order_count'),
        value: 'orderCount',
      },
      {
        id: 'totalRevenue',
        headerName: this.translate.instant('pages.reports.total_revenue'),
        value: (row) => this.facade.formatCurrency(row?.totalRevenue),
      },
    ],
  };

  // Payroll Tables
  employeeSummariesTableDef: TableDefinition = {
    columns: [
      {
        id: 'employeeName',
        headerName: this.translate.instant('pages.reports.employee_name'),
        value: 'employeeName',
      },
      {
        id: 'totalOrders',
        headerName: this.translate.instant('pages.reports.total_orders'),
        value: 'totalOrders',
      },
      {
        id: 'invoiceCount',
        headerName: this.translate.instant('pages.reports.invoice_count'),
        value: 'invoiceCount',
      },
      {
        id: 'subTotal',
        headerName: this.translate.instant('pages.reports.subtotal'),
        value: (row) => this.facade.formatCurrency(row?.subTotal),
      },
      {
        id: 'bonusAmount',
        headerName: this.translate.instant('pages.reports.bonus'),
        value: (row) => this.facade.formatCurrency(row?.bonusAmount),
      },
      {
        id: 'deductionAmount',
        headerName: this.translate.instant('pages.reports.deductions'),
        value: (row) => this.facade.formatCurrency(row?.deductionAmount),
      },
      {
        id: 'totalAmount',
        headerName: this.translate.instant('pages.reports.total_amount'),
        value: (row) => this.facade.formatCurrency(row?.totalAmount),
        columnClass: 'total-cell',
      },
    ],
  };

  payrollByStatusTableDef: TableDefinition = {
    columns: [
      {
        id: 'statusName',
        headerName: this.translate.instant('pages.reports.status'),
        value: 'statusName',
      },
      {
        id: 'invoiceCount',
        headerName: this.translate.instant('pages.reports.invoice_count'),
        value: 'invoiceCount',
      },
      {
        id: 'totalAmount',
        headerName: this.translate.instant('pages.reports.total_amount'),
        value: (row) => this.facade.formatCurrency(row?.totalAmount),
      },
    ],
  };

  monthlyPayrollTableDef: TableDefinition = {
    columns: [
      {
        id: 'month',
        headerName: this.translate.instant('pages.reports.month'),
        value: (row) => `${row?.monthName} ${row?.year}`,
      },
      {
        id: 'invoiceCount',
        headerName: this.translate.instant('pages.reports.invoice_count'),
        value: 'invoiceCount',
      },
      {
        id: 'totalAmount',
        headerName: this.translate.instant('pages.reports.total_amount'),
        value: (row) => this.facade.formatCurrency(row?.totalAmount),
      },
    ],
  };

  ngOnInit(): void {
    this.facade.loadRevenueReport();
    this.setupAutoFilter();
  }

  private setupAutoFilter(): void {
    this.dateRangeForm.valueChanges
      .pipe(
        debounceTime(500),
        distinctUntilChanged(
          (prev, curr) =>
            prev.startDate?.getTime() === curr.startDate?.getTime() &&
            prev.endDate?.getTime() === curr.endDate?.getTime()
        ),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((value) => {
        if (value.startDate && value.endDate) {
          this.facade.setDateRange(value.startDate, value.endDate);
        }
      });
  }

  activeTab: ReportType = 'revenue';

  onTabChange(value: string | number): void {
    const tab = value as ReportType;
    this.activeTab = tab;
    this.facade.setActiveTab(tab);
  }

  refreshReport(): void {
    this.facade.refreshCurrentReport();
  }
}