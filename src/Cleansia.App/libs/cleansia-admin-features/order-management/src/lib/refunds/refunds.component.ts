import { ChangeDetectionStrategy, Component, computed, inject, OnInit, TemplateRef, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RefundListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { RefundsFacade } from './refunds.facade';
import {
  buildRefundStateOptions,
  formatRefundAmount,
  getRefundsTableDefinition,
  refundCustomerLabel,
  refundOriginLabel,
  refundStateHint,
  refundStripeId,
} from './refunds.models';

@Component({
  selector: 'cleansia-admin-refunds',
  standalone: true,
  imports: [
    FormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './refunds.component.html',
  providers: [RefundsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RefundsComponent implements OnInit {
  protected readonly facade = inject(RefundsFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  private readonly stateTemplate = viewChild<TemplateRef<RefundListItem>>('stateTemplate');
  private readonly stripeRefundTemplate = viewChild<TemplateRef<RefundListItem>>('stripeRefundTemplate');

  protected readonly stateOptions = computed(() => {
    this.facade.lang();
    return buildRefundStateOptions(this.translate);
  });

  protected readonly table = computed(() => {
    this.facade.lang();
    return getRefundsTableDefinition(
      {
        onRetry: (row) => this.facade.retry(row),
        onOpenDispute: (row) => this.facade.openDispute(row),
        onRecordRepayment: (row) => this.facade.startRepayment(row),
        isBusy: () => this.facade.submitting(),
      },
      this.translate,
      this.permissions,
      this.stateTemplate(),
      this.stripeRefundTemplate()
    );
  });

  protected readonly hasOrder = (row: RefundListItem): boolean => !!row.orderId;

  ngOnInit(): void {
    this.facade.init();
  }

  protected hintOf(row: RefundListItem): string {
    this.facade.lang();
    return refundStateHint(row, this.translate);
  }

  protected readonly stripeIdOf = refundStripeId;

  protected originOf(row: RefundListItem): string {
    return refundOriginLabel(row, this.translate);
  }

  protected customerOf(row: RefundListItem): string {
    return refundCustomerLabel(row, this.translate);
  }

  protected amountOf(row: RefundListItem): string {
    return formatRefundAmount(row, this.facade.lang());
  }
}
