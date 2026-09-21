import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { OrderListItem, OrderStatus, PaymentStatus } from '@cleansia/admin-services';
import {
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { OrderManagementFacade } from './order-management.facade';
import { getOrderTableDefinition } from './order-management.models';

@Component({
  selector: 'cleansia-admin-order-management',
  standalone: true,
  imports: [
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    FormsModule,
    ReactiveFormsModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './order-management.component.html',
  providers: [OrderManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderManagementComponent implements OnInit {
  private readonly router = inject(Router);
  protected readonly facade = inject(OrderManagementFacade);
  private readonly translate = inject(TranslateService);

  private readonly orderStatusTemplate = viewChild<TemplateRef<OrderListItem>>('orderStatusTemplate');
  private readonly paymentStatusTemplate = viewChild<TemplateRef<OrderListItem>>('paymentStatusTemplate');

  readonly OrderStatus = OrderStatus;
  readonly PaymentStatus = PaymentStatus;

  protected readonly table = computed(() => {
    this.facade.lang();
    return getOrderTableDefinition(
      { onViewDetails: (row) => this.viewOrderDetails(row) },
      this.translate,
      this.orderStatusTemplate(),
      this.paymentStatusTemplate()
    );
  });

  readonly currencyOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.currencies().map((c) => ({ label: c.code, value: c.id }))
  );

  ngOnInit(): void {
    this.facade.loadCurrencies();
    this.facade.loadOrders();
  }

  viewOrderDetails(order: OrderListItem): void {
    this.router.navigate([CleansiaAdminRoute.ORDER_MANAGEMENT, order.id]);
  }

  toggleOrderStatus(status: OrderStatus): void {
    this.facade.setOrderStatus(status, !this.facade.isOrderStatusChecked(status));
  }

  togglePaymentStatus(status: PaymentStatus): void {
    this.facade.setPaymentStatus(status, !this.facade.isPaymentStatusChecked(status));
  }
}
