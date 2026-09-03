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
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { catchError, concatMap, finalize, from, map, of, takeUntil, tap } from 'rxjs';
import {
  CustomerDisputeStatus,
  DISPUTE_UPLOAD_ERROR_KEY_MAP,
  DISPUTE_UPLOAD_FALLBACK_ERROR_KEY,
  hasUnreadStaffReply,
  latestStaffMessageTimestamp,
  validateEvidenceFile,
  readCreatedDisputeId,
} from './disputes.models';

const LAST_VIEWED_STORAGE_KEY = 'cleansia.customer.disputes.last_viewed';
const STAFF_ACTIVITY_STORAGE_KEY = 'cleansia.customer.disputes.staff_activity';

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

  /**
   * Every file the customer picked, not just the first.
   *
   * Owner, 2026-09-03: "I'm able to attach only 1 photo of evidence. There can
   * be multiple." A dispute about a whole clean is rarely one photograph.
   *
   * The endpoint takes ONE file per call, so these go one after another rather
   * than at once — `concatMap`, not `mergeMap`. The guard below is a
   * single-flight latch on the whole batch, and firing them in parallel would
   * make it drop every file after the first.
   */
  uploadEvidence(disputeId: string, files: File[], onSuccess?: () => void): void {
    if (this.uploadingEvidence() || files.length === 0) return;

    for (const file of files) {
      const validationError = validateEvidenceFile(file);
      if (validationError) {
        this.snackbar.showErrorTranslated(
          `pages.disputes.evidence.${validationError}`
        );
        return;
      }
    }

    this.uploadingEvidence.set(true);
    let uploaded = 0;

    from(files)
      .pipe(
        concatMap((file) => {
          const fileParameter: FileParameter = { data: file, fileName: file.name };
          return this.customerClient.disputeClient
            .uploadEvidence(disputeId, fileParameter)
            .pipe(
              tap(() => uploaded++),
              // One bad file does not lose the rest of the batch.
              catchError((error: unknown) => {
                this.snackbar.showErrorTranslated(this.resolveUploadErrorKey(error));
                return of(null);
              })
            );
        }),
        takeUntil(this.destroyed$),
        finalize(() => {
          this.uploadingEvidence.set(false);
          if (uploaded === 0) return;
          this.snackbar.showSuccessTranslated(
            'pages.disputes.evidence.upload_success'
          );
          onSuccess?.();
          this.loadDisputeDetail(disputeId);
        })
      )
      .subscribe();
  }

  /**
   * Owner, 2026-09-03: a photo attached while FILING a dispute never arrived.
   *
   * `Dispute/Create` answers with the new dispute's id and this threw it away
   * — `map(() => true)` — so the caller had nothing to upload the evidence
   * against and the file was dropped without a word. The id reaches `onSuccess`
   * now, and the page uploads what it collected.
   */
  createDispute(
    orderId: string,
    reason: DisputeReason,
    description: string,
    onSuccess: (disputeId: string | null) => void
  ): void {
    if (this.creatingDispute()) return;
    this.creatingDispute.set(true);

    const command = new CreateDisputeCommand();
    command.orderId = orderId;
    command.reason = reason;
    command.description = description;

    this.customerClient.disputeClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        // The outcome is a WRAPPER, not the body. An empty 200 parses to `null`
        // and so did the failure path, so a successful create was
        // indistinguishable from a failed one: the loader stopped and nothing
        // else happened — no snackbar, no return to the list. Owner, 2026-09-03.
        map((response: unknown) => ({ created: true, response })),
        catchError((error: unknown) => {
          this.snackbar.showApiError(error, 'pages.disputes.create_error');
          return of({ created: false, response: null as unknown });
        }),
        finalize(() => this.creatingDispute.set(false))
      )
      .subscribe(({ created, response }) => {
        if (!created) return;
        // 200 means the dispute EXISTS, whatever the body held. It is filed, so
        // it is reported as filed; only the id — and therefore the photo — can
        // still be missing, and the caller is told which.
        this.snackbar.showSuccessTranslated('pages.disputes.create_success');
        onSuccess(readCreatedDisputeId(response));
      });
  }

  /**
   * The dispute was filed but the server returned no id, so the photo attached
   * to it could not be sent. Says so out loud rather than dropping it.
   */
  reportEvidenceOrphaned(): void {
    this.snackbar.showErrorTranslated('pages.disputes.evidence.orphaned');
  }

  sendMessage(disputeId: string, message: string, onSuccess: () => void): void {
    this.sendingMessage.set(true);

    const command = new AddDisputeMessageCommand();
    command.disputeId = disputeId;
    command.message = message;
    command.isStaffMessage = false;

    this.customerClient.disputeClient
      .addMessage(command)
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
    const code = extractApiErrorCode(error);
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
