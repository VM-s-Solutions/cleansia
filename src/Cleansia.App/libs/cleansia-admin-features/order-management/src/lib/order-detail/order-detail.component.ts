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
import { CleansiaPermissionDirective } from '@cleansia/directives';
import {
  AuditResourceType,
  buildAuditResourceHistoryRoute,
  CleansiaAdminRoute,
  Policy,
} from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import {
  AdminOrderOpsComponent,
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
    AdminOrderOpsComponent,
    AdminOrderPhotosComponent,
    AdminOrderRefundComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './order-detail.component.html',
  providers: [OrderDetailFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly facade = inject(OrderDetailFacade);

  protected readonly Policy = Policy;

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      this.facade.loadOrderDetail(orderId);
    }
  }

  goBack(): void {
    this.router.navigate([CleansiaAdminRoute.ORDER_MANAGEMENT]);
  }

  viewAuditHistory(): void {
    const orderId = this.facade.order()?.id;
    if (!orderId) return;
    this.router.navigate(
      buildAuditResourceHistoryRoute(AuditResourceType.Order, orderId)
    );
  }

  onRefunded(): void {
    this.reloadOrder();
  }

  onOrderChanged(): void {
    this.reloadOrder();
  }

  private reloadOrder(): void {
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
      case OrderStatus.New:
        return 'status-history-item status-new';
      case OrderStatus.Pending:
        return 'status-history-item status-pending';
      case OrderStatus.Confirmed:
        return 'status-history-item status-confirmed';
      case OrderStatus.OnTheWay:
        return 'status-history-item status-ontheway';
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
