import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  DeletionRequestsClient,
  DocumentDeletionRequestDto,
  DocumentDeletionRequestStatus,
  ResolveDocumentDeletionRequestRequest,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

/**
 * The queue of cleaners asking for a document to be removed.
 *
 * It opens on Pending because it is a to-do list: an admin coming here wants what is still waiting
 * on them, not a history. Answered requests stay reachable by choosing a status, so the record is
 * not hidden — it is just not what the screen opens on.
 */
@Injectable()
export class DeletionRequestsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  /**
   * Injected directly rather than through `AdminClient`. NSwag split the
   * `deletion-requests/{id}/resolve` route into its own generated client, and `AdminClient` — which
   * is hand-written — does not carry it. Both are `providedIn: 'root'`, so this resolves the same
   * base URL either way.
   */
  private readonly deletionRequestsClient = inject(DeletionRequestsClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly requests = signal<DocumentDeletionRequestDto[]>([]);
  readonly status = signal<DocumentDeletionRequestStatus | null>(
    DocumentDeletionRequestStatus.Pending,
  );

  readonly initialLoading = signal(true);
  readonly loading = signal(false);
  readonly resolving = signal(false);

  load(): void {
    this.loading.set(true);
    const requested = this.status();
    this.adminClient.adminEmployeeDocumentClient
      .deletionRequests(requested ?? undefined)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => {
          this.loading.set(false);
          this.initialLoading.set(false);
        }),
      )
      .subscribe((requests) => {
        // Drop a response the filter has already moved past.
        if (requests && this.status() === requested) {
          this.requests.set(requests);
        }
      });
  }

  selectStatus(status: DocumentDeletionRequestStatus | null): void {
    this.status.set(status);
    this.requests.set([]);
    this.load();
  }

  /**
   * Answering is the ONLY thing that removes the document — the request itself never touched it,
   * which is why one left unanswered costs the cleaner nothing and why this is the action gated on
   * `CanApproveEmployeeDocument` rather than on the weaker read permission.
   */
  resolve(requestId: string, approve: boolean, notes: string | null): void {
    // Constructed then assigned — see the note in the requirements facade.
    const request = new ResolveDocumentDeletionRequestRequest();
    request.approve = approve;
    request.notes = notes ?? undefined;

    this.resolving.set(true);
    this.deletionRequestsClient
      .resolve(requestId, request)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.resolving.set(false)),
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              approve
                ? 'pages.document_deletion_requests.messages.approve_success'
                : 'pages.document_deletion_requests.messages.reject_success',
            ),
          );
          this.load();
        }
      });
  }
}
