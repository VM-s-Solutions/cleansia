import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import {
  AdminGdprClient,
  GdprExportDto,
  GdprRequestDto,
  UserConsentDto,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

@Injectable()
export class DataProtectionFacade extends UnsubscribeControlDirective {
  private readonly gdprClient = inject(AdminGdprClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly requests = signal<GdprRequestDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);

  readonly consents = signal<UserConsentDto[]>([]);
  readonly consentsUserId = signal<string | null>(null);
  readonly consentsLoading = signal<boolean>(false);

  readonly exporting = signal<boolean>(false);
  readonly erasing = signal<boolean>(false);

  private currentPage = 1;
  private currentPageSize = 20;

  loadRequests(): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.gdprClient
      .requests(this.currentPage, this.currentPageSize)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.hasError.set(true);
          this.snackbar.showApiError(
            error,
            'pages.data_protection.requests.load_error'
          );
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((rows) => {
        if (rows) {
          this.requests.set(rows);
          // The endpoint returns a bare page without a total count; expose
          // "one row beyond the current window" while pages come back full so
          // the paginator keeps offering a next page.
          const offset = (this.currentPage - 1) * this.currentPageSize;
          const hasMore = rows.length === this.currentPageSize;
          this.totalRecords.set(offset + rows.length + (hasMore ? 1 : 0));
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(offset: number, limit: number): void {
    this.currentPageSize = limit;
    this.currentPage = Math.floor(offset / limit) + 1;
    this.loadRequests();
  }

  loadConsents(userId: string): void {
    const trimmed = userId.trim();
    if (!trimmed) return;

    this.consentsUserId.set(trimmed);
    this.consentsLoading.set(true);

    this.gdprClient
      .consents(trimmed)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showApiError(
            error,
            'pages.data_protection.consents.load_error'
          );
          return of(null);
        }),
        finalize(() => this.consentsLoading.set(false))
      )
      .subscribe((rows) => {
        if (rows) {
          this.consents.set(rows);
        }
      });
  }

  exportUserData(userId: string): void {
    const trimmed = userId.trim();
    if (!trimmed || this.exporting()) return;

    this.exporting.set(true);
    this.gdprClient
      .export(trimmed)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showApiError(
            error,
            'pages.data_protection.export.error'
          );
          return of(null);
        }),
        finalize(() => this.exporting.set(false))
      )
      .subscribe((data: GdprExportDto | null) => {
        if (data) {
          this.downloadJson(data, `user-data-export-${trimmed}.json`);
          this.snackbar.showSuccess(
            this.translate.instant('pages.data_protection.export.success')
          );
          this.loadRequests();
        }
      });
  }

  eraseUserAccount(userId: string): void {
    const trimmed = userId.trim();
    if (!trimmed || this.erasing()) return;

    this.erasing.set(true);
    this.gdprClient
      .deleteAccount(trimmed)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbar.showApiError(
            error,
            'pages.data_protection.erase.error'
          );
          return of('error' as const);
        }),
        finalize(() => this.erasing.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccess(
          this.translate.instant('pages.data_protection.erase.success')
        );
        this.loadRequests();
      });
  }

  private downloadJson(data: unknown, fileName: string): void {
    if (!this.isBrowser) return;
    const json = JSON.stringify(data, null, 2);
    const blob = new Blob([json], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
