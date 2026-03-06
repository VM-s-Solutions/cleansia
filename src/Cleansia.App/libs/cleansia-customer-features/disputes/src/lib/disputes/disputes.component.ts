import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { CustomerClient } from '@cleansia/customer-services';
import {
  loadCustomerDisputes,
  loadCustomerDisputeDetail,
  selectCustomerDisputes,
  selectCustomerDisputesTotal,
  selectCustomerDisputeDetail,
  selectCustomerDisputeLoading,
} from '@cleansia/customer-stores';
import {
  AddDisputeMessageCommand,
  CreateDisputeCommand,
  DisputeDetails,
  DisputeListItem,
  DisputeReason,
  DisputeStatus,
} from '@cleansia/partner-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';

@Component({
  selector: 'cleansia-customer-disputes',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslateModule,
    TableModule,
    TagModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    DialogModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
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

  showCreateDialog = signal(false);
  showDetailDialog = signal(false);
  newMessage = signal('');
  sendingMessage = signal(false);

  createForm = {
    orderId: '',
    reason: DisputeReason.QualityIssue,
    description: '',
  };

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
    const orderId = this.route.snapshot.queryParamMap.get('orderId');
    if (orderId) {
      this.createForm.orderId = orderId;
      this.showCreateDialog.set(true);
    }
  }

  loadDisputes(): void {
    this.store.dispatch(loadCustomerDisputes({ offset: this.first, limit: this.rows }));
  }

  onPageChange(event: TableLazyLoadEvent): void {
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

  formatDate(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString('cs-CZ', {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  }
}
