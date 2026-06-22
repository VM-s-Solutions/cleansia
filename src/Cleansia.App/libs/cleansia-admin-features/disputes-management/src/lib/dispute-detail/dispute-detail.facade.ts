import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AddDisputeMessageCommand,
  AdminDisputeClient,
  DisputeDetails,
  DisputeStatus,
  ResolveDisputeCommand,
  UpdateDisputeStatusCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService, extractApiErrorCode } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  DISPUTE_ERROR_KEY_MAP,
  DISPUTE_FALLBACK_ERROR_KEY,
} from '../disputes-management/disputes-management.models';

const TERMINAL_STATUSES: ReadonlySet<DisputeStatus> = new Set([
  DisputeStatus.Resolved,
  DisputeStatus.Closed,
]);

@Injectable()
export class DisputeDetailFacade extends UnsubscribeControlDirective {
  private readonly disputeClient = inject(AdminDisputeClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly dispute = signal<DisputeDetails | null>(null);
  readonly loading = signal<boolean>(false);
  readonly hasError = signal<boolean>(false);

  readonly resolving = signal<boolean>(false);
  readonly updatingStatus = signal<boolean>(false);
  readonly sendingMessage = signal<boolean>(false);

  readonly isTerminal = computed(() => {
    const status = this.dispute()?.status?.value;
    return status != null && TERMINAL_STATUSES.has(status);
  });

  loadDispute(disputeId: string): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.disputeClient
      .details(disputeId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.dispute.set(response);
        }
      });
  }

  resolve(
    disputeId: string,
    refundAmount: number | null,
    resolutionNotes: string | null
  ): void {
    if (!disputeId || this.resolving()) return;

    this.resolving.set(true);
    const command = new ResolveDisputeCommand({
      disputeId,
      refundAmount: refundAmount ?? undefined,
      resolutionNotes: resolutionNotes?.trim() || undefined,
    });

    this.disputeClient
      .resolve(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(this.resolveErrorKey(error))
          );
          return of('error' as const);
        }),
        finalize(() => this.resolving.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccess(
          this.translate.instant('pages.disputes_management.resolve.submitted')
        );
        this.loadDispute(disputeId);
      });
  }

  updateStatus(disputeId: string, newStatus: DisputeStatus): void {
    if (!disputeId || this.updatingStatus()) return;

    this.updatingStatus.set(true);
    const command = new UpdateDisputeStatusCommand({ disputeId, newStatus });

    this.disputeClient
      .updateStatus(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(this.resolveErrorKey(error))
          );
          return of('error' as const);
        }),
        finalize(() => this.updatingStatus.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccess(
          this.translate.instant(
            'pages.disputes_management.status_update.success'
          )
        );
        this.loadDispute(disputeId);
      });
  }

  addMessage(disputeId: string, message: string, onSuccess: () => void): void {
    const trimmed = message.trim();
    if (!disputeId || !trimmed || this.sendingMessage()) return;

    this.sendingMessage.set(true);
    const command = new AddDisputeMessageCommand({
      disputeId,
      message: trimmed,
      isStaffMessage: true,
    });

    this.disputeClient
      .addMessage(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showError(
            this.translate.instant(this.resolveErrorKey(error))
          );
          return of('error' as const);
        }),
        finalize(() => this.sendingMessage.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccess(
          this.translate.instant('pages.disputes_management.message.sent')
        );
        onSuccess();
        this.loadDispute(disputeId);
      });
  }

  private resolveErrorKey(error: unknown): string {
    const code = extractApiErrorCode(error);
    if (code && DISPUTE_ERROR_KEY_MAP[code]) {
      return DISPUTE_ERROR_KEY_MAP[code];
    }
    return DISPUTE_FALLBACK_ERROR_KEY;
  }
}
