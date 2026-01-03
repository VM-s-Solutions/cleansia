import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  EmployeeInvoiceDto,
  EmployeeInvoiceStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaMultiselectComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { InvoiceManagementFacade } from './invoice-management.facade';
import {
  getInvoiceStatusClass,
  getInvoiceTableDefinition,
} from './invoice-management.models';

@Component({
  selector: 'cleansia-admin-invoice-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaMultiselectComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './invoice-management.component.html',
  styleUrl: './invoice-management.component.scss',
  providers: [InvoiceManagementFacade],
})
export class InvoiceManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(InvoiceManagementFacade);
  private readonly translate = inject(TranslateService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');

  invoiceTableDefinition!: TableDefinition<EmployeeInvoiceDto>;

  readonly EmployeeInvoiceStatus = EmployeeInvoiceStatus;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    status: [[] as EmployeeInvoiceStatus[]],
  });

  invoiceStatusMultiOptions = this.facade.invoiceStatusOptions;

  ngAfterViewInit(): void {
    this.invoiceTableDefinition = getInvoiceTableDefinition(
      {
        onViewDetails: this.viewInvoiceDetails.bind(this),
        onDownload: this.downloadInvoice.bind(this),
      },
      this.translate,
      this.statusTemplate()
    );

    this.cd.detectChanges();

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    this.facade.loadInvoices();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewInvoiceDetails(invoice: EmployeeInvoiceDto): void {
    this.router.navigate(['/invoice-management', invoice.id]);
  }

  downloadInvoice(invoice: EmployeeInvoiceDto): void {
    this.facade.downloadInvoice(invoice);
  }

  getInvoiceStatusClass(invoice: EmployeeInvoiceDto): string {
    return getInvoiceStatusClass(invoice.status);
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      statuses:
        formValues.status && formValues.status.length > 0
          ? formValues.status
          : undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      status: [],
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: number }): void {
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDirection =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    const sort = [
      new SortDefinition({
        field: event.field,
        direction: sortDirection,
      }),
    ];
    this.facade.onSortChange(sort);
  }
}
