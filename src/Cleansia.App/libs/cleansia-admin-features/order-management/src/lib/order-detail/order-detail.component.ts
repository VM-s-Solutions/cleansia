import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { OrderStatus, OrderStatusTrackDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import {
  AdminOrderPhotosComponent,
  AdminOrderRefundComponent,
} from './components';
import { OrderDetailFacade } from './order-detail.facade';

@Component({
  selector: 'cleansia-admin-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    TranslatePipe,
    AdminOrderPhotosComponent,
    AdminOrderRefundComponent,
  ],
  templateUrl: './order-detail.component.html',
  providers: [OrderDetailFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly facade = inject(OrderDetailFacade);

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      this.facade.loadOrderDetail(orderId);
    }
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.ORDER_MANAGEMENT]);
  }

  onRefunded(): void {
    const orderId = this.facade.order()?.id;
    if (orderId) {
      this.facade.loadOrderDetail(orderId);
    }
  }

  getStatusHistoryIcon(status: OrderStatusTrackDto): string {
    return this.facade.getOrderStatusIcon(status.status);
  }

  getStatusHistoryClass(status: OrderStatusTrackDto): string {
    if (!status.status) return 'status-history-item status-pending';
    switch (status.status.value) {
      case OrderStatus.Pending:
        return 'status-history-item status-pending';
      case OrderStatus.Confirmed:
        return 'status-history-item status-confirmed';
      case OrderStatus.InProgress:
        return 'status-history-item status-inprogress';
      case OrderStatus.Completed:
        return 'status-history-item status-completed';
      case OrderStatus.Cancelled:
        return 'status-history-item status-cancelled';
      default:
        return 'status-history-item status-pending';
    }
  }
}
