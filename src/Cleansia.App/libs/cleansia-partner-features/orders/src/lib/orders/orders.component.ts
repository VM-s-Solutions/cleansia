import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  inject,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  TableDefinition,
} from '@cleansia/components';
import {
  OrderListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { ToastModule } from 'primeng/toast';
import { OrdersFacade } from './orders.facade';
import {
  getAvailableOrdersTableDefinition,
  getMyOrdersTableDefinition,
} from './orders.models';

@Component({
  selector: 'cleansia-partner-orders',
  standalone: true,
  imports: [
    TableModule,
    ToastModule,
    CommonModule,
    ButtonModule,
    TranslatePipe,
    TabsModule,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './orders.component.html',
  providers: [OrdersFacade],
})
export class OrdersComponent implements AfterViewInit {
  private readonly router = inject(Router);
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(OrdersFacade);
  private readonly translate = inject(TranslateService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');
  orderStatusTemplate = viewChild<TemplateRef<any>>('orderStatusTemplate');

  availableOrdersTableDefinition!: TableDefinition<OrderListItem>;
  myOrdersTableDefinition!: TableDefinition<OrderListItem>;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;

  ngAfterViewInit(): void {
    this.availableOrdersTableDefinition = getAvailableOrdersTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
        onTakeOrder: this.takeOrder.bind(this),
      },
      this.translate,
      this.statusTemplate(),
      this.orderStatusTemplate()
    );

    this.myOrdersTableDefinition = getMyOrdersTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
      },
      this.translate,
      this.statusTemplate(),
      this.orderStatusTemplate()
    );

    this.cd.detectChanges();
  }

  onTabChange(tabIndex: string | number): void {
    tabIndex = Number(tabIndex);
    this.lastSortField = null;
    this.lastSortOrder = null;

    if (tabIndex === 0) {
      this.facade.setActiveTab('available');
      this.facade.loadAvailableOrders();
    } else if (tabIndex === 1) {
      this.facade.setActiveTab('my');
      this.facade.loadMyOrders();
    }
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

    const sortDef = [
      new SortDefinition({
        field: event.field,
        direction:
          event.order === 1
            ? SortDirection.Ascending
            : SortDirection.Descending,
      }),
    ];
    this.facade.updateSort(sortDef);
  }

  viewOrderDetails(order: OrderListItem): void {
    this.router.navigate(['/orders', order.id]);
  }

  takeOrder(order: OrderListItem): void {
    this.facade.takeOrder(order.id!);
  }

  getStatusClass(order: OrderListItem): string {
    const statusName =
      order.paymentStatus?.name?.toLowerCase().replace(/\s+/g, '-') ||
      'pending';
    return `status-badge status-${statusName}`;
  }

  getOrderStatusClass(order: OrderListItem): string {
    const statusName =
      order.orderStatus?.name?.toLowerCase().replace(/\s+/g, '-') || 'pending';
    return `order-status-badge status-${statusName}`;
  }
}
