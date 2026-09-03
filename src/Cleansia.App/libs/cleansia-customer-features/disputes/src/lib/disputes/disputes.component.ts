import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
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
  CleansiaTextareaComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import {
  Code,
  DisputeListItem,
  DisputeReason,
} from '@cleansia/customer-services';
import { FoamEdgeComponent } from '@cleansia-customer/home';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { TagSeverity } from '@cleansia/types';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { SkeletonModule } from 'primeng/skeleton';
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
    ReactiveFormsModule,
    RouterLink,
    TranslatePipe,
    SkeletonModule,
    CleansiaButtonComponent,
    CleansiaFileComponent,
    FoamEdgeComponent,
    CleansiaSelectComponent,
    CleansiaTextareaComponent,
  ],
  templateUrl: './disputes.component.html',
  providers: [DisputesFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DisputesComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);
  protected readonly facade = inject(DisputesFacade);
  protected readonly routes = CleansiaCustomerRoute;

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

  /**
   * Two states of one page, as the board draws them: the list beside the thread,
   * or the "new dispute" form. Not two modal dialogs over a table — a support
   * conversation is the page's content, and a dialog is a thing you dismiss.
   */
  readonly mode = signal<'list' | 'new'>('list');

  /** Which thread the right-hand panel is showing. */
  readonly selectedId = signal<string | null>(null);

  /**
   * Whether the composer is showing its dropzone. The shared file component is
   * a drag-and-drop PANEL, not a button, so leaving it open would make it the
   * largest thing in the thread. The board draws an "attach photo" action, so
   * that is what the row holds until it is pressed.
   */
  readonly attaching = signal(false);

  /** Which reason chip is on. Mirrors the form control, which submits. */
  readonly pickedReason = signal<DisputeReason>(DisputeReason.QualityIssue);

  messageControl = new FormControl('', { nonNullable: true });
  evidenceControl = new FormControl<File[]>([], { nonNullable: true });

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

  readonly reasonOptions: ICleansiaSelectOption[] = [
    { label: this.translate.instant('pages.disputes.reasons.quality_issue'), value: DisputeReason.QualityIssue },
    { label: this.translate.instant('pages.disputes.reasons.service_not_provided'), value: DisputeReason.ServiceNotProvided },
    { label: this.translate.instant('pages.disputes.reasons.service_incomplete'), value: DisputeReason.ServiceIncomplete },
    { label: this.translate.instant('pages.disputes.reasons.damaged_property'), value: DisputeReason.DamagedProperty },
    { label: this.translate.instant('pages.disputes.reasons.unauthorized_charge'), value: DisputeReason.UnauthorizedCharge },
    { label: this.translate.instant('pages.disputes.reasons.incorrect_amount'), value: DisputeReason.IncorrectAmount },
    { label: this.translate.instant('pages.disputes.reasons.other'), value: DisputeReason.Other },
  ];

  /**
   * One page, no paginator. The board draws a short list of one customer's own
   * disputes beside the thread, and a customer with fifty of them is not a
   * case this platform has — nor one a paginator would improve.
   */
  rows = 50;
  first = 0;

  /**
   * The first dispute opens itself. The right-hand panel is the page's content,
   * not a detail you go and fetch, so arriving on an empty one would be a
   * half-drawn page — and the customer's newest dispute is the one they came
   * for. It only ever fires when nothing is selected, so it does not fight a
   * click.
   */
  private readonly openFirstByDefault = effect(() => {
    const list = this.disputes();
    if (this.selectedId() || list.length === 0) return;
    const first = list[0];
    if (first?.id) this.openThread(first);
  });

  ngOnInit(): void {
    this.loadDisputes();
    this.facade.loadOrdersForSelect();
    // Arriving from an order's "something was wrong": straight into the form,
    // with the order it is about already chosen.
    const orderId = this.route.snapshot.queryParamMap.get('orderId');
    if (orderId) {
      this.createForm.patchValue({ orderId });
      this.mode.set('new');
    }
  }

  loadDisputes(): void {
    this.facade.loadDisputes(this.first, this.rows);
  }

  openThread(dispute: DisputeListItem): void {
    if (!dispute.id || this.selectedId() === dispute.id) return;
    this.selectedId.set(dispute.id);
    this.facade.markViewed(dispute.id);
    this.facade.loadDisputeDetail(dispute.id);
    this.evidenceControl.setValue([]);
    this.messageControl.reset('');
    this.attaching.set(false);
  }

  /** Evidence with somewhere to point. A row with no URL renders nothing. */
  readonly evidence = computed(() =>
    (this.disputeDetail()?.evidence ?? []).filter((e) => !!e.blobUrl),
  );

  /** Whether to draw the file back as a picture or as a document. */
  isImage(fileName: string | undefined): boolean {
    return /\.(jpe?g|png|webp|gif|avif)$/i.test(fileName ?? '');
  }

  toggleAttaching(): void {
    this.attaching.update((on) => !on);
  }

  startNew(): void {
    this.mode.set('new');
  }

  cancelNew(): void {
    this.mode.set('list');
    this.createForm.reset({
      orderId: '',
      reason: DisputeReason.QualityIssue,
      description: '',
    });
    this.pickedReason.set(DisputeReason.QualityIssue);
    this.evidenceControl.setValue([]);
  }

  /**
   * The board draws the reasons as chips rather than a select, so the form
   * picks one by pressing. The control stays the source of truth for the
   * submit; the signal is what the chips read to know which one is on.
   */
  pickReason(reason: DisputeReason): void {
    this.createForm.patchValue({ reason });
    this.pickedReason.set(reason);
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
    // The photo goes with the dispute it is about, so it can only be sent once
    // the dispute exists and has an id. Reading it BEFORE cancelNew clears the
    // form is the whole point — the reset empties the control.
    const evidence = this.evidenceControl.value[0];

    this.facade.createDispute(orderId, reason, description, (disputeId) => {
      this.cancelNew();
      // A brand-new dispute is the one to be looking at.
      this.selectedId.set(null);
      this.loadDisputes();
      if (evidence) {
        this.facade.uploadEvidence(disputeId, evidence);
      }
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
      this.attaching.set(false);
    });
  }

  canUploadEvidence(): boolean {
    return isDisputeOpen(this.disputeDetail()?.status?.value);
  }

  getStatusSeverity(status: Code | undefined): TagSeverity {
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

  /**
   * All five the app ships, not three. This listed cs, en and `pl` — a locale
   * this app does not have — so a Slovak, Russian or Ukrainian customer read
   * their dispute dates in US format.
   */
  private getLocale(): string {
    const localeMap: Record<string, string> = {
      cs: 'cs-CZ',
      sk: 'sk-SK',
      en: 'en-US',
      ru: 'ru-RU',
      uk: 'uk-UA',
    };
    return localeMap[this.translate.currentLang] || 'en-US';
  }

  /**
   * How long the customer has been waiting. Recent is a COUNT — "2 days" is the
   * thing you feel — and older than a month is a date, because "47 days" stops
   * meaning anything. `Intl.RelativeTimeFormat` carries every locale's plural
   * rules, which ngx-translate does not.
   */
  ageLabel(createdOn: Date | undefined): string {
    if (!createdOn) return '';
    const days = Math.floor((Date.now() - new Date(createdOn).getTime()) / 86400000);
    if (days > 30) {
      return new Date(createdOn).toLocaleDateString(this.getLocale(), {
        day: 'numeric',
        month: 'numeric',
      });
    }
    return new Intl.RelativeTimeFormat(this.getLocale(), { numeric: 'auto' }).format(
      -days,
      'day',
    );
  }

  /** The moment a message was written, to the minute — a thread is a sequence. */
  formatMoment(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleString(this.getLocale(), {
      weekday: 'short',
      day: 'numeric',
      month: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatDate(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString(this.getLocale(), {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
  }

  /**
   * An agreed refund is money off a specific order, and that order has a
   * currency — but `DisputeDetails` does not carry one, so there is nothing
   * here to read. The fallback matches every other customer screen; the real
   * fix is a `Currency` on the DTO, and until it exists this figure is right
   * only while CZ is the only market.
   */
  formatPrice(price: number, currency?: { code?: string }): string {
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: currency?.code || 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }
}
