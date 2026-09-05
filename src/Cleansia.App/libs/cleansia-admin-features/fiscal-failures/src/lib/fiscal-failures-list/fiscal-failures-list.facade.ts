import { Injectable, inject, signal } from '@angular/core';
import {
  AdminFiscalFailureClient,
  ApiClient,
  FiscalFailureDto,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class FiscalFailuresListFacade extends UnsubscribeControlDirective {
  private readonly apiClient = inject(ApiClient);
  private readonly fiscalFailureClient = inject(AdminFiscalFailureClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly failures = signal<FiscalFailureDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  loadFailures(): void {
    this.loading.set(true);

    this.apiClient
      .adminFiscalFailure()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.failures.set(response);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  retryNow(receiptId: string): void {
    this.fiscalFailureClient
      .retry(receiptId)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.snackbarService.showSuccess(
            this.translate.instant('fiscal_failures.messages.retry_scheduled')
          );
          this.loadFailures();
        },
        // The error was swallowed with catchError(() => of(null)) and the SUCCESS snackbar then fired
        // unconditionally — these endpoints return 204, so there is no response body to test and the
        // facade could not tell the two apart. Any failure read as "done", and now that these actions
        // carry a rate-limit window a 429 would have read that way too.
        error: (err) =>
          this.snackbarService.showApiError(err, 'fiscal_failures.errors.retry_scheduled'),
      });
  }

  acknowledge(receiptId: string): void {
    this.fiscalFailureClient
      .acknowledge(receiptId)
      .pipe(takeUntil(this.destroyed$))
      .subscribe({
        next: () => {
          this.snackbarService.showSuccess(
            this.translate.instant('fiscal_failures.messages.acknowledged')
          );
          this.loadFailures();
        },
        // The error was swallowed with catchError(() => of(null)) and the SUCCESS snackbar then fired
        // unconditionally — these endpoints return 204, so there is no response body to test and the
        // facade could not tell the two apart. Any failure read as "done", and now that these actions
        // carry a rate-limit window a 429 would have read that way too.
        error: (err) =>
          this.snackbarService.showApiError(err, 'fiscal_failures.errors.acknowledged'),
      });
  }
}
