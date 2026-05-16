import { computed, inject, Injectable, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CustomerClient } from '@cleansia/customer-services';
import {
  loadCustomerDisputes,
  loadCustomerDisputeDetail,
  loadCustomerOrders,
  selectCustomerDisputes,
  selectCustomerDisputesTotal,
  selectCustomerDisputeDetail,
  selectCustomerDisputeLoading,
  selectCustomerOrders,
} from '@cleansia/customer-stores';
import {
  AddDisputeMessageCommand,
  CreateDisputeCommand,
  DisputeListItem,
  DisputeReason,
  OrderListItem,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { takeUntil } from 'rxjs';

@Injectable()
export class DisputesFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);

  readonly disputes = toSignal(this.store.select(selectCustomerDisputes), {
    initialValue: [] as DisputeListItem[],
  });
  readonly totalRecords = toSignal(this.store.select(selectCustomerDisputesTotal), {
    initialValue: 0,
  });
  readonly loading = toSignal(this.store.select(selectCustomerDisputeLoading('paged')), {
    initialValue: false,
  });
  readonly disputeDetail = toSignal(this.store.select(selectCustomerDisputeDetail));
  readonly detailLoading = toSignal(this.store.select(selectCustomerDisputeLoading('detail')), {
    initialValue: false,
  });

  private readonly orders = toSignal(this.store.select(selectCustomerOrders), {
    initialValue: [] as OrderListItem[],
  });
  readonly orderOptions = computed(() =>
    (this.orders() || []).map((o) => ({
      label: `#${o.displayOrderNumber}`,
      value: o.id,
    }))
  );

  readonly sendingMessage = signal(false);

  loadDisputes(offset: number, limit: number): void {
    this.store.dispatch(loadCustomerDisputes({ offset, limit }));
  }

  loadOrdersForSelect(): void {
    this.store.dispatch(loadCustomerOrders({ offset: 0, limit: 100 }));
  }

  loadDisputeDetail(disputeId: string): void {
    this.store.dispatch(loadCustomerDisputeDetail({ disputeId }));
  }

  createDispute(
    orderId: string,
    reason: DisputeReason,
    description: string,
    onSuccess: () => void
  ): void {
    this.customerClient.disputeClient
      .create(
        new CreateDisputeCommand({
          orderId,
          reason,
          description,
        })
      )
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.snackbar.showSuccess(
            this.translate.instant('pages.disputes.create_success')
          );
          onSuccess();
        },
        error: () => {
          this.snackbar.showError(
            this.translate.instant('pages.disputes.create_error')
          );
        },
      });
  }

  sendMessage(disputeId: string, message: string, onSuccess: () => void): void {
    this.sendingMessage.set(true);
    this.customerClient.disputeClient
      .addMessage(
        new AddDisputeMessageCommand({
          disputeId,
          message,
          isStaffMessage: false,
        })
      )
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.sendingMessage.set(false);
          onSuccess();
          this.loadDisputeDetail(disputeId);
        },
        error: () => {
          this.sendingMessage.set(false);
          this.snackbar.showError(
            this.translate.instant('pages.disputes.send_error')
          );
        },
      });
  }
}
