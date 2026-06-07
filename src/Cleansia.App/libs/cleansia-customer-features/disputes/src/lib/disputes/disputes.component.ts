import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CleansiaButtonComponent } from '@cleansia/components';
import {
  DisputeListItem,
  DisputeReason,
  DisputeStatus,
} from '@cleansia/partner-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { SkeletonModule } from 'primeng/skeleton';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { DisputesFacade } from './disputes.facade';
import {
  DISPUTE_DESCRIPTION_MAX_LENGTH,
  DISPUTE_DESCRIPTION_MIN_LENGTH,
} from '../dispute.constants';

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
  providers: [DisputesFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DisputesComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(DisputesFacade);

  // Re-expose facade signals to keep template bindings unchanged.
  readonly disputes = this.facade.disputes;
  readonly totalRecords = this.facade.totalRecords;
  readonly loading = this.facade.loading;
  readonly disputeDetail = this.facade.disputeDetail;
  readonly detailLoading = this.facade.detailLoading;
  readonly orderOptions = this.facade.orderOptions;
  readonly sendingMessage = this.facade.sendingMessage;

  protected readonly descriptionMaxLength = DISPUTE_DESCRIPTION_MAX_LENGTH;

  showCreateDialog = signal(false);
  showDetailDialog = signal(false);
  newMessage = signal('');

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
        if (this.createForm.description.length < DISPUTE_DESCRIPTION_MIN_LENGTH)
          return this.translate.instant('global.validation.minlength', { min: DISPUTE_DESCRIPTION_MIN_LENGTH });
        if (this.createForm.description.length > DISPUTE_DESCRIPTION_MAX_LENGTH)
          return this.translate.instant('global.validation.maxlength', { max: DISPUTE_DESCRIPTION_MAX_LENGTH });
        return null;
      default:
        return null;
    }
  }

  isCreateFormValid(): boolean {
    return !!(
      this.createForm.orderId &&
      this.createForm.description &&
      this.createForm.description.length >= DISPUTE_DESCRIPTION_MIN_LENGTH &&
      this.createForm.description.length <= DISPUTE_DESCRIPTION_MAX_LENGTH
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
    this.facade.loadOrdersForSelect();
    const orderId = this.route.snapshot.queryParamMap.get('orderId');
    if (orderId) {
      this.createForm.orderId = orderId;
      this.showCreateDialog.set(true);
    }
  }

  loadDisputes(): void {
    this.facade.loadDisputes(this.first, this.rows);
  }

  onPageChange(event: PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    this.loadDisputes();
  }

  openDetail(dispute: DisputeListItem): void {
    if (!dispute.id) return;
    this.facade.loadDisputeDetail(dispute.id);
    this.showDetailDialog.set(true);
  }

  createDispute(): void {
    if (!this.createForm.orderId || !this.createForm.description) return;

    this.facade.createDispute(
      this.createForm.orderId,
      this.createForm.reason,
      this.createForm.description,
      () => {
        this.showCreateDialog.set(false);
        this.createForm = { orderId: '', reason: DisputeReason.QualityIssue, description: '' };
        this.loadDisputes();
      }
    );
  }

  sendMessage(): void {
    const detail = this.disputeDetail();
    const message = this.newMessage();
    if (!detail?.id || !message) return;

    this.facade.sendMessage(detail.id, message, () => {
      this.newMessage.set('');
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
