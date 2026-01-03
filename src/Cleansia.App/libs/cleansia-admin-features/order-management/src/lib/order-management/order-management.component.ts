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
  OrderListItem,
  OrderStatus,
  PaymentStatus,
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
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DatePickerModule } from 'primeng/datepicker';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { OrderManagementFacade } from './order-management.facade';
import {
  getOrderStatusClass,
  getOrderTableDefinition,
  getPaymentStatusClass,
} from './order-management.models';

@Component({
  selector: 'cleansia-admin-order-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaMultiselectComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
    DatePickerModule,
  ],
  templateUrl: './order-management.component.html',
  styleUrl: './order-management.component.scss',
  providers: [OrderManagementFacade],
})
export class OrderManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(OrderManagementFacade);
  private readonly translate = inject(TranslateService);

  orderStatusTemplate = viewChild<TemplateRef<any>>('orderStatusTemplate');
  paymentStatusTemplate = viewChild<TemplateRef<any>>('paymentStatusTemplate');

  orderTableDefinition!: TableDefinition<OrderListItem>;

  readonly OrderStatus = OrderStatus;
  readonly PaymentStatus = PaymentStatus;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    orderStatus: [[] as OrderStatus[]],
    paymentStatus: [[] as PaymentStatus[]],
    searchTerm: [''],
    cleaningDateFrom: [null as Date | null],
    cleaningDateTo: [null as Date | null],
  });

  orderStatusMultiOptions = this.facade.orderStatusOptions;
  paymentStatusMultiOptions = this.facade.paymentStatusOptions;

  ngAfterViewInit(): void {
    this.orderTableDefinition = getOrderTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
      },
      this.translate,
      this.orderStatusTemplate(),
      this.paymentStatusTemplate()
    );

    this.cd.detectChanges();

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    this.facade.loadOrders();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewOrderDetails(order: OrderListItem): void {
    this.router.navigate(['/order-management', order.id]);
  }

  getOrderStatusClass(order: OrderListItem): string {
    return getOrderStatusClass(order);
  }

  getPaymentStatusClass(order: OrderListItem): string {
    return getPaymentStatusClass(order);
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      orderStatuses:
        formValues.orderStatus && formValues.orderStatus.length > 0
          ? formValues.orderStatus
          : undefined,
      paymentStatuses:
        formValues.paymentStatus && formValues.paymentStatus.length > 0
          ? formValues.paymentStatus
          : undefined,
      searchTerm: formValues.searchTerm?.trim() || undefined,
      cleaningDateFrom: formValues.cleaningDateFrom ?? undefined,
      cleaningDateTo: formValues.cleaningDateTo ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      orderStatus: [],
      paymentStatus: [],
      searchTerm: '',
      cleaningDateFrom: null,
      cleaningDateTo: null,
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
