import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaFileComponent } from '@cleansia/components';
import {
  Code,
  DisputeListItem,
  DisputeReason,
} from '@cleansia/customer-services';
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
  CustomerDisputeStatus,
  DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES,
  DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES,
  DISPUTE_STATUS_LABEL_KEYS,
  getDisputeReasonLabelKey,
  getDisputeStatusSeverity,
  isDisputeOpen,
} from './disputes.models';
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
    ReactiveFormsModule,
    TranslatePipe,
    TagModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    DialogModule,
    SkeletonModule,
    PaginatorModule,
    CleansiaButtonComponent,
    CleansiaFileComponent,
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
  protected readonly evidenceAccept =
    DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES.join(',');
  protected readonly evidenceMaxFileSize = DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES;

  showCreateDialog = signal(false);
  showDetailDialog = signal(false);
  newMessage = signal('');

  evidenceControl = new FormControl<File[]>([], { nonNullable: true });

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

  statusFilterOptions = Object.entries(DISPUTE_STATUS_LABEL_KEYS).map(
    ([value, labelKey]) => ({
      label: this.translate.instant(labelKey),
      value: Number(value) as CustomerDisputeStatus,
    })
  );

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

  onStatusFilterChange(status: CustomerDisputeStatus | null): void {
    this.facade.setStatusFilter(status);
    this.first = 0;
    this.loadDisputes();
  }

  onPageChange(event: PaginatorState): void {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    this.loadDisputes();
  }

  openDetail(dispute: DisputeListItem): void {
    if (!dispute.id) return;
    this.facade.markViewed(dispute.id);
    this.facade.loadDisputeDetail(dispute.id);
    this.evidenceControl.setValue([]);
    this.showDetailDialog.set(true);
  }

  isUnread(dispute: DisputeListItem): boolean {
    return !!dispute.id && this.facade.unreadDisputeIds().has(dispute.id);
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

  uploadEvidence(): void {
    const detail = this.disputeDetail();
    const file = this.evidenceControl.value[0];
    if (!detail?.id || !file) return;

    this.facade.uploadEvidence(detail.id, file, () => {
      this.evidenceControl.setValue([]);
    });
  }

  canUploadEvidence(): boolean {
    return isDisputeOpen(this.disputeDetail()?.status?.value);
  }

  getStatusSeverity(status: Code | undefined): string {
    return getDisputeStatusSeverity(status?.value);
  }

  statusLabel(status: Code | undefined): string {
    const value = status?.value as CustomerDisputeStatus | undefined;
    const labelKey = value != null ? DISPUTE_STATUS_LABEL_KEYS[value] : undefined;
    return labelKey ? this.translate.instant(labelKey) : status?.name ?? '';
  }

  reasonLabel(reason: Code | undefined): string {
    return this.translate.instant(getDisputeReasonLabelKey(reason?.value));
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

  formatPrice(price: number): string {
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }
}
