import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  PayPeriodDto,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { ToastModule } from 'primeng/toast';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { PayPeriodManagementFacade } from './pay-period-management.facade';
import { getPayPeriodTableDefinition } from './pay-period-management.models';

@Component({
  selector: 'cleansia-admin-pay-period-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaSelectComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ReactiveFormsModule,
    ToastModule,
  ],
  templateUrl: './pay-period-management.component.html',
  providers: [PayPeriodManagementFacade, DialogService],
})
export class PayPeriodManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(PayPeriodManagementFacade);
  private readonly translate = inject(TranslateService);

  payPeriodTableDefinition!: TableDefinition<PayPeriodDto>;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  // Filter form
  filterForm = this.fb.group({
    status: [null as number | null],
    year: [null as number | null],
  });

  // Year options - generate last 5 years
  yearOptions = Array.from({ length: 5 }, (_, i) => {
    const year = new Date().getFullYear() - i;
    return { label: year.toString(), value: year };
  });

  ngAfterViewInit(): void {
    this.payPeriodTableDefinition = getPayPeriodTableDefinition(
      {
        onViewDetails: this.viewPayPeriodDetails.bind(this),
        onClose: this.closePayPeriod.bind(this),
      },
      this.translate
    );

    this.cd.detectChanges();

    // Setup automatic filtering with debounce
    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    // Load pay periods on init
    this.facade.loadPayPeriods();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewPayPeriodDetails(payPeriod: PayPeriodDto): void {
    this.router.navigate(['/pay-periods', payPeriod.id]);
  }

  closePayPeriod(payPeriod: PayPeriodDto): void {
    // TODO: Open dialog for confirmation and notes
    if (confirm('Are you sure you want to close this pay period?')) {
      this.facade.closePayPeriod(payPeriod.id!);
    }
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      status: formValues.status ?? undefined,
      year: formValues.year ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      status: null,
      year: null,
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: number }): void {
    // Check if sort actually changed to prevent duplicate requests
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    // Update last sort state
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
