import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnInit,
  signal,
  TemplateRef,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import {
  FormBuilder,
  FormControl,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaFileComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextareaComponent,
  ICleansiaSelectOption,
  PaginationState,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import {
  Code,
  DisputeListItem,
  DisputeReason,
} from '@cleansia/customer-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { SkeletonModule } from 'primeng/skeleton';
import { DisputesFacade } from './disputes.facade';
import {
  CustomerDisputeStatus,
  DISPUTE_EVIDENCE_ALLOWED_CONTENT_TYPES,
  DISPUTE_EVIDENCE_MAX_FILE_SIZE_BYTES,
  DISPUTE_STATUS_LABEL_KEYS,
  getDisputeReasonLabelKey,
  getDisputeStatusSeverity,
  getDisputesTableDefinition,
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
    ReactiveFormsModule,
    TranslatePipe,
    TagModule,
    DialogModule,
    SkeletonModule,
    CleansiaButtonComponent,
    CleansiaFileComponent,
    CleansiaTableComponent,
    CleansiaSelectComponent,
    CleansiaTextareaComponent,
  ],
  templateUrl: './disputes.component.html',
  providers: [DisputesFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DisputesComponent implements OnInit, AfterViewInit {
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);
  protected readonly facade = inject(DisputesFacade);

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

  messageControl = new FormControl('', { nonNullable: true });
  evidenceControl = new FormControl<File[]>([], { nonNullable: true });
  statusFilterControl = new FormControl<CustomerDisputeStatus | null>(null);

  readonly createForm = this.fb.nonNullable.group({
    orderId: ['', Validators.required],
    reason: [DisputeReason.QualityIssue, Validators.required],
    description: [
      '',
      [
        Validators.required,
        Validators.minLength(DISPUTE_DESCRIPTION_MIN_LENGTH),
        Validators.maxLength(DISPUTE_DESCRIPTION_MAX_LENGTH),
      ],
    ],
  });

  columns: TableColumn<DisputeListItem>[] = [];
  actions: TableAction<DisputeListItem>[] = [];

  @ViewChild('orderCell') orderCell?: TemplateRef<DisputeListItem>;
  @ViewChild('reasonCell') reasonCell?: TemplateRef<DisputeListItem>;
  @ViewChild('statusCell') statusCell?: TemplateRef<DisputeListItem>;
  @ViewChild('createdCell') createdCell?: TemplateRef<DisputeListItem>;

  readonly reasonOptions: ICleansiaSelectOption[] = [
    { label: this.translate.instant('pages.disputes.reasons.quality_issue'), value: DisputeReason.QualityIssue },
    { label: this.translate.instant('pages.disputes.reasons.service_not_provided'), value: DisputeReason.ServiceNotProvided },
    { label: this.translate.instant('pages.disputes.reasons.service_incomplete'), value: DisputeReason.ServiceIncomplete },
    { label: this.translate.instant('pages.disputes.reasons.damaged_property'), value: DisputeReason.DamagedProperty },
    { label: this.translate.instant('pages.disputes.reasons.unauthorized_charge'), value: DisputeReason.UnauthorizedCharge },
    { label: this.translate.instant('pages.disputes.reasons.incorrect_amount'), value: DisputeReason.IncorrectAmount },
    { label: this.translate.instant('pages.disputes.reasons.other'), value: DisputeReason.Other },
  ];

  readonly statusFilterOptions: ICleansiaSelectOption[] = Object.entries(
    DISPUTE_STATUS_LABEL_KEYS
  ).map(([value, labelKey]) => ({
    label: this.translate.instant(labelKey),
    value: Number(value) as CustomerDisputeStatus,
  }));

  rows = 10;
  first = 0;

  ngOnInit(): void {
    this.loadDisputes();
    this.facade.loadOrdersForSelect();
    const orderId = this.route.snapshot.queryParamMap.get('orderId');
    if (orderId) {
      this.createForm.patchValue({ orderId });
      this.showCreateDialog.set(true);
    }
  }

  ngAfterViewInit(): void {
    const definition = getDisputesTableDefinition(
      { onOpen: (row) => this.openDetail(row) },
      this.translate,
      {
        order: this.orderCell,
        reason: this.reasonCell,
        status: this.statusCell,
        created: this.createdCell,
      }
    );
    this.columns = definition.columns;
    this.actions = definition.actions;
    this.cdr.detectChanges();
  }

  loadDisputes(): void {
    this.facade.loadDisputes(this.first, this.rows);
  }

  onStatusFilterChange(status: CustomerDisputeStatus | null): void {
    this.facade.setStatusFilter(status ?? null);
    this.first = 0;
    this.loadDisputes();
  }

  onPageChange(event: PaginationState): void {
    this.first = event.first;
    this.rows = event.rows;
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
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const { orderId, reason, description } = this.createForm.getRawValue();
    this.facade.createDispute(orderId, reason, description, () => {
      this.showCreateDialog.set(false);
      this.createForm.reset({
        orderId: '',
        reason: DisputeReason.QualityIssue,
        description: '',
      });
      this.loadDisputes();
    });
  }

  sendMessage(): void {
    const detail = this.disputeDetail();
    const message = this.messageControl.value.trim();
    if (!detail?.id || !message) return;

    this.facade.sendMessage(detail.id, message, () => {
      this.messageControl.reset('');
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
