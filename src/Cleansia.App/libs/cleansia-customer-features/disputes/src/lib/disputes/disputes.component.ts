import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CleansiaButtonComponent } from '@cleansia/components';
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
  DisputeStatus,
  OrderListItem,
} from '@cleansia/partner-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { SkeletonModule } from 'primeng/skeleton';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';

@Component({
  selector: 'cleansia-customer-disputes',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    TagModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    DialogModule,
    SkeletonModule,
    PaginatorModule,
    CleansiaButtonComponent,
  ],
  templateUrl: './disputes.component.html',
})
export class DisputesComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);

  disputes = toSignal(this.store.select(selectCustomerDisputes), { initialValue: [] as DisputeListItem[] });
  totalRecords = toSignal(this.store.select(selectCustomerDisputesTotal), { initialValue: 0 });
  loading = toSignal(this.store.select(selectCustomerDisputeLoading('paged')), { initialValue: false });
  disputeDetail = toSignal(this.store.select(selectCustomerDisputeDetail));
  detailLoading = toSignal(this.store.select(selectCustomerDisputeLoading('detail')), { initialValue: false });

  private readonly orders = toSignal(this.store.select(selectCustomerOrders), { initialValue: [] as OrderListItem[] });
  readonly orderOptions = computed(() =>
    (this.orders() || []).map(o => ({
      label: `#${o.displayOrderNumber}`,
      value: o.id,
    }))
  );

  showCreateDialog = signal(false);
  showDetailDialog = signal(false);
  newMessage = signal('');
  sendingMessage = signal(false);

  createForm = {
    orderId: '',
    reason: DisputeReason.QualityIssue,
    description: '',
  };
  createFormTouched: Record<string, boolean> = {};

  markCreateTouched(field: string): void {
    this.createFormTouched[field] = true;
  }

  createFieldError(field: string): string | null {
    if (!this.createFormTouched[field]) return null;
    switch (field) {
      case 'orderId':
        return !this.createForm.orderId ? this.translate.instant('global.validation.required') : null;
      case 'description':
        if (!this.createForm.description) return this.translate.instant('global.validation.required');
        if (this.createForm.description.length < 10) return this.translate.instant('global.validation.minlength', { min: 10 });
        if (this.createForm.description.length > 2000) return this.translate.instant('global.validation.maxlength', { max: 2000 });
        return null;
      default:
        return null;
    }
  }

  isCreateFormValid(): boolean {
    return !!(
      this.createForm.orderId &&
      this.createForm.description &&
      this.createForm.description.length >= 10 &&
      this.createForm.description.length <= 2000
    );
  }

  reasonOptions = [
    { label: this.translate.instant('pages.disputes.reasons.quality_issue'), value: DisputeReason.QualityIssue },
    { label: this.translate.instant('pages.disputes.reasons.service_not_provided'), value: DisputeReason.ServiceNotProvided },
    { label: this.translate.instant('pages.disputes.reasons.service_incomplete'), value: DisputeReason.ServiceIncomplete },
    { label: this.translate.instant('pages.disputes.reasons.damaged_property'), value: DisputeReason.DamagedProperty },
    { label: this.translate.instant('pages.disputes.reasons.unauthorized_charge'), value: DisputeReason.UnauthorizedCharge },
    { label: this.translate.instant('pages.disputes.reasons.incorrect_amount'), value: DisputeReason.IncorrectAmount },
    { label: this.translate.instant('pages.disputes.reasons.other'), value: DisputeReason.Other },
  ];

  rows = 10;
  first = 0;

  ngOnInit(): void {
    this.loadDisputes();
    this.store.dispatch(loadCustomerOrders({ offset: 0, limit: 100 }));
    const orderId = this.route.snapshot.queryParamMap.get('orderId');
    if (orderId) {
      this.createForm.orderId = orderId;
      this.showCreateDialog.set(true);
    }
  }

  loadDisputes(): void {
    this.store.dispatch(loadCustomerDisputes({ offset: this.first, limit: this.rows }));
  }

  onPageChange(event: PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    this.loadDisputes();
  }

  openDetail(dispute: DisputeListItem): void {
    if (!dispute.id) return;
    this.store.dispatch(loadCustomerDisputeDetail({ disputeId: dispute.id }));
    this.showDetailDialog.set(true);
  }

  createDispute(): void {
    if (!this.createForm.orderId || !this.createForm.description) return;

    this.customerClient.disputeClient
      .create(
        new CreateDisputeCommand({
          orderId: this.createForm.orderId,
          reason: this.createForm.reason,
          description: this.createForm.description,
          userId: undefined,
        })
      )
      .subscribe({
        next: () => {
          this.snackbar.showSuccess(this.translate.instant('pages.disputes.create_success'));
          this.showCreateDialog.set(false);
          this.createForm = { orderId: '', reason: DisputeReason.QualityIssue, description: '' };
          this.loadDisputes();
        },
        error: () => {
          this.snackbar.showError(this.translate.instant('pages.disputes.create_error'));
        },
      });
  }

  sendMessage(): void {
    const detail = this.disputeDetail();
    const message = this.newMessage();
    if (!detail?.id || !message) return;

    this.sendingMessage.set(true);
    this.customerClient.disputeClient
      .addMessage(
        new AddDisputeMessageCommand({
          disputeId: detail.id,
          message,
          isStaffMessage: false,
        })
      )
      .subscribe({
        next: () => {
          this.sendingMessage.set(false);
          this.newMessage.set('');
          this.store.dispatch(loadCustomerDisputeDetail({ disputeId: detail.id! }));
        },
        error: () => {
          this.sendingMessage.set(false);
          this.snackbar.showError(this.translate.instant('pages.disputes.send_error'));
        },
      });
  }

  getStatusSeverity(status: { value?: number }): string {
    switch (status?.value) {
      case DisputeStatus.Pending: return 'warn';
      case DisputeStatus.UnderReview: return 'info';
      case DisputeStatus.WaitingForResponse: return 'warn';
      case DisputeStatus.Resolved: return 'success';
      case DisputeStatus.Closed: return 'secondary';
      case DisputeStatus.Escalated: return 'danger';
      default: return 'info';
    }
  }

  private getLocale(): string {
    const localeMap: Record<string, string> = { cs: 'cs-CZ', en: 'en-US', pl: 'pl-PL' };
    return localeMap[this.translate.currentLang] || 'en-US';
  }

  formatDate(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString(this.getLocale(), {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  }
}
