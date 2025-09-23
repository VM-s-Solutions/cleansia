import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  inject,
  TemplateRef,
  viewChild,
  ViewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
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
import { OrderListItem } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { ToastModule } from 'primeng/toast';
import { OrdersFacade } from './orders.facade';
import { getOrderTableDefinition } from './orders.models';

@Component({
  selector: 'cleansia-partner-orders',
  standalone: true,
  imports: [
    TableModule,
    ToastModule,
    CommonModule,
    ButtonModule,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss'],
  providers: [OrdersFacade],
})
export class OrdersComponent implements AfterViewInit {
  private readonly router = inject(Router);
  private readonly cd = inject(ChangeDetectorRef);
  protected readonly facade = inject(OrdersFacade);
  private readonly translate = inject(TranslateService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');
  orderStatusTemplate = viewChild<TemplateRef<any>>('orderStatusTemplate');

  ordersTableDefinition!: TableDefinition<OrderListItem>;

  ngAfterViewInit(): void {
    this.ordersTableDefinition = getOrderTableDefinition(
      {
        onViewDetails: this.viewOrderDetails.bind(this),
      },
      this.translate,
      this.statusTemplate(),
      this.orderStatusTemplate()
    );
    this.cd.detectChanges();
  }

  viewOrderDetails(order: OrderListItem): void {
    this.router.navigate(['/orders', order.id]);
  }

  getStatusClass(order: OrderListItem): string {
    const statusName =
      order.paymentStatus?.name?.toLowerCase().replace(/\s+/g, '-') ||
      'pending';
    return `status-badge status-${statusName}`;
  }

  getOrderStatusClass(order: OrderListItem): string {
    const statusName =
      order.orderStatus?.name?.toLowerCase().replace(/\s+/g, '-') ||
      'pending';
    return `order-status-badge status-${statusName}`;
  }
}
