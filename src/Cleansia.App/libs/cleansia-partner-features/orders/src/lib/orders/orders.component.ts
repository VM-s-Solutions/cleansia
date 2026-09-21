import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaHelpCardComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { OrderListItem } from '@cleansia/partner-services';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { OrdersFacade } from './orders.facade';
import {
  getAvailableOrdersTableDefinition,
  getMyOrdersTableDefinition,
  ORDERS_HELP_STEPS,
  ORDER_STATUS_FLOW,
  PAYMENT_STATUS_FLOW,
} from './orders.models';

@Component({
  selector: 'cleansia-partner-orders',
  standalone: true,
  imports: [
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaHelpCardComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    ReactiveFormsModule,
  ],
  templateUrl: './orders.component.html',
  providers: [OrdersFacade, DialogService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrdersComponent {
  private readonly router = inject(Router);
  protected readonly facade = inject(OrdersFacade);

  private readonly statusTemplate = viewChild<TemplateRef<OrderListItem>>('statusTemplate');
  private readonly orderStatusTemplate = viewChild<TemplateRef<OrderListItem>>('orderStatusTemplate');
  private readonly ordersHelpCard = viewChild<CleansiaHelpCardComponent>('ordersHelpCard');
  private readonly paymentHelpCard = viewChild<CleansiaHelpCardComponent>('paymentHelpCard');

  protected readonly availableOrdersTable = computed(() =>
    getAvailableOrdersTableDefinition(
      {
        onTakeOrder: (row) => this.takeOrder(row),
        isTakeInFlight: (row) => this.facade.isTakeInFlight(row.id),
      },
      this.facade.lang(),
      this.statusTemplate(),
      this.orderStatusTemplate()
    )
  );

  protected readonly myOrdersTable = computed(() =>
    getMyOrdersTableDefinition(
      {
        onStartOrder: (row) => this.startOrder(row),
        onCompleteOrder: (row) => this.facade.openCompleteOrderDialog(row),
      },
      this.facade.lang(),
      this.statusTemplate(),
      this.orderStatusTemplate()
    )
  );

  ordersHelpSteps = ORDERS_HELP_STEPS;
  orderStatusFlow = ORDER_STATUS_FLOW;
  paymentStatusFlow = PAYMENT_STATUS_FLOW;

  private readonly helpDismissedVersion = signal(0);
  readonly isOrdersHelpDismissed = computed(() => {
    this.helpDismissedVersion();
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-orders-help-dismissed');
  });
  readonly isPaymentHelpDismissed = computed(() => {
    this.helpDismissedVersion();
    return CleansiaHelpCardComponent.isHelpDismissed('cleansia-orders-payment-help-dismissed');
  });

  viewOrderDetails(order: OrderListItem): void {
    this.router.navigate([CleansiaPartnerRoute.ORDERS, order.id]);
  }

  takeOrder(order: OrderListItem): void {
    if (order.id) this.facade.takeOrder(order.id);
  }

  startOrder(order: OrderListItem): void {
    if (order.id) this.facade.startOrder(order.id);
  }

  onHelpDismissedChange(): void {
    this.helpDismissedVersion.update((v) => v + 1);
  }

  restoreAllHelp(): void {
    this.ordersHelpCard()?.restore();
    this.paymentHelpCard()?.restore();
    this.helpDismissedVersion.update((v) => v + 1);
  }
}
