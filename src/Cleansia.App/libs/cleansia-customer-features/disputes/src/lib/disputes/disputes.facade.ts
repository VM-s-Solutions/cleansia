import { isPlatformBrowser } from '@angular/common';
import {
  computed,
  inject,
  Injectable,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  CustomerClient,
  AddDisputeMessageCommand,
  CreateDisputeCommand,
  DisputeListItem,
  DisputeReason,
  FileParameter,
  OrderListItem,
} from '@cleansia/customer-services';
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
import { SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { catchError, finalize, map, of, takeUntil } from 'rxjs';
import {
  CustomerDisputeStatus,
  DISPUTE_UPLOAD_ERROR_KEY_MAP,
  DISPUTE_UPLOAD_FALLBACK_ERROR_KEY,
  hasUnreadStaffReply,
  latestStaffMessageTimestamp,
  validateEvidenceFile,
} from './disputes.models';

const LAST_VIEWED_STORAGE_KEY = 'cleansia.customer.disputes.last_viewed';
const STAFF_ACTIVITY_STORAGE_KEY = 'cleansia.customer.disputes.staff_activity';

interface ApiErrorResult {
  detail?: string;
  title?: string;
}

@Injectable()
export class DisputesFacade extends UnsubscribeControlDirective {
  private readonly store = inject(Store);
  private readonly customerClient = inject(CustomerClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

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
  readonly creatingDispute = signal(false);
  readonly uploadingEvidence = signal(false);
  readonly statusFilter = signal<CustomerDisputeStatus | null>(null);

  private readonly lastViewedMap = signal<Record<string, string>>(
    this.readStorageMap(LAST_VIEWED_STORAGE_KEY)
  );
  private readonly staffActivityMap = signal<Record<string, string>>(
    this.readStorageMap(STAFF_ACTIVITY_STORAGE_KEY)
  );

  readonly unreadDisputeIds = computed(() => {
    const staffActivity = this.staffActivityMap();
    const lastViewed = this.lastViewedMap();
    const unread = new Set<string>();
    for (const id of Object.keys(staffActivity)) {
      if (hasUnreadStaffReply(staffActivity[id], lastViewed[id])) {
        unread.add(id);
      }
    }
    return unread;
  });

  constructor() {
    super();
    this.store
      .select(selectCustomerDisputes)
      .pipe(takeUntil(this.destroyed$))
      .subscribe((disputes) => this.refreshStaffActivity(disputes ?? []));
  }

  loadDisputes(offset: number, limit: number): void {
    const status = this.statusFilter();
    this.store.dispatch(
      loadCustomerDisputes({
        offset,
        limit,
        statuses: status !== null ? [status] : undefined,
      })
    );
  }

  setStatusFilter(status: CustomerDisputeStatus | null): void {
    this.statusFilter.set(status);
  }

  loadOrdersForSelect(): void {
    this.store.dispatch(loadCustomerOrders({ offset: 0, limit: 100 }));
  }

  loadDisputeDetail(disputeId: string): void {
    this.store.dispatch(loadCustomerDisputeDetail({ disputeId }));
  }

  markViewed(disputeId: string): void {
    this.lastViewedMap.update((map) => ({
      ...map,
      [disputeId]: new Date().toISOString(),
    }));
    this.writeStorageMap(LAST_VIEWED_STORAGE_KEY, this.lastViewedMap());
  }

  uploadEvidence(disputeId: string, file: File, onSuccess?: () => void): void {
    if (this.uploadingEvidence()) return;

    const validationError = validateEvidenceFile(file);
    if (validationError) {
      this.snackbar.showErrorTranslated(
        `pages.disputes.evidence.${validationError}`
      );
      return;
    }

    this.uploadingEvidence.set(true);
    const fileParameter: FileParameter = { data: file, fileName: file.name };
    this.customerClient.disputeClient
      .uploadEvidence(disputeId, fileParameter)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showErrorTranslated(this.resolveUploadErrorKey(error));
          return of(null);
        }),
        finalize(() => this.uploadingEvidence.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated(
            'pages.disputes.evidence.upload_success'
          );
          onSuccess?.();
          this.loadDisputeDetail(disputeId);
        }
      });
  }

  createDispute(
    orderId: string,
    reason: DisputeReason,
    description: string,
    onSuccess: () => void
  ): void {
    if (this.creatingDispute()) return;
    this.creatingDispute.set(true);
    this.customerClient.disputeClient
      .create(new CreateDisputeCommand({ orderId, reason, description }))
      .pipe(
        takeUntil(this.destroyed$),
        map(() => true),
        catchError((error: unknown) => {
          this.snackbar.showApiError(error, 'pages.disputes.create_error');
          return of(false);
        }),
        finalize(() => this.creatingDispute.set(false))
      )
      .subscribe((succeeded) => {
        if (!succeeded) return;
        this.snackbar.showSuccessTranslated('pages.disputes.create_success');
        onSuccess();
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
      .pipe(
        takeUntil(this.destroyed$),
        map(() => true),
        catchError((error: unknown) => {
          this.snackbar.showApiError(error, 'pages.disputes.send_error');
          return of(false);
        }),
        finalize(() => this.sendingMessage.set(false))
      )
      .subscribe((succeeded) => {
        if (!succeeded) return;
        onSuccess();
        this.loadDisputeDetail(disputeId);
      });
  }

  // Client-side unread detection (no server-side last-read model yet): per
  // page of disputes, pull each detail and remember the newest staff-message
  // timestamp; the badge compares it against the locally persisted last view.
  private refreshStaffActivity(disputes: DisputeListItem[]): void {
    for (const dispute of disputes) {
      if (!dispute.id) continue;
      const disputeId = dispute.id;
      this.customerClient.disputeClient
        .getById(disputeId)
        .pipe(
          takeUntil(this.destroyed$),
          catchError(() => of(null))
        )
        .subscribe((detail) => {
          if (!detail) return;
          const latest = latestStaffMessageTimestamp(detail.messages);
          if (!latest) return;
          this.staffActivityMap.update((map) => ({
            ...map,
            [disputeId]: latest,
          }));
          this.writeStorageMap(
            STAFF_ACTIVITY_STORAGE_KEY,
            this.staffActivityMap()
          );
        });
    }
  }

  private resolveUploadErrorKey(error: unknown): string {
    const apiError = error as { result?: ApiErrorResult; response?: string };
    let code = apiError?.result?.detail || apiError?.result?.title;

    if (!code && apiError?.response) {
      try {
        const parsed = JSON.parse(apiError.response) as ApiErrorResult;
        code = parsed.detail || parsed.title;
      } catch {
        code = undefined;
      }
    }

    if (code && DISPUTE_UPLOAD_ERROR_KEY_MAP[code]) {
      return DISPUTE_UPLOAD_ERROR_KEY_MAP[code];
    }
    return DISPUTE_UPLOAD_FALLBACK_ERROR_KEY;
  }

  private readStorageMap(key: string): Record<string, string> {
    if (!this.isBrowser) return {};
    try {
      const raw = localStorage.getItem(key);
      return raw ? (JSON.parse(raw) as Record<string, string>) : {};
    } catch {
      return {};
    }
  }

  private writeStorageMap(key: string, map: Record<string, string>): void {
    if (!this.isBrowser) return;
    try {
      localStorage.setItem(key, JSON.stringify(map));
    } catch {
      // Storage may be unavailable (private mode) — badges degrade gracefully.
    }
  }
}
