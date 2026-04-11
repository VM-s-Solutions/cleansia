import { Injectable, inject, signal } from '@angular/core';
import {
  AdminFiscalFailureClient,
  ApiClient,
  FiscalFailureDto,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class FiscalFailuresListFacade {
  private readonly apiClient = inject(ApiClient);
  private readonly fiscalFailureClient = inject(AdminFiscalFailureClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  private destroy$ = new Subject<void>();

  readonly failures = signal<FiscalFailureDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);

  loadFailures(): void {
    this.loading.set(true);

    this.apiClient
      .adminFiscalFailure()
      .pipe(
        takeUntil(this.destroy$),
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
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe(() => {
        this.snackbarService.showSuccess(
          this.translate.instant('fiscal_failures.messages.retry_scheduled')
        );
        this.loadFailures();
      });
  }

  acknowledge(receiptId: string): void {
    this.fiscalFailureClient
      .acknowledge(receiptId)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe(() => {
        this.snackbarService.showSuccess(
          this.translate.instant('fiscal_failures.messages.acknowledged')
        );
        this.loadFailures();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
