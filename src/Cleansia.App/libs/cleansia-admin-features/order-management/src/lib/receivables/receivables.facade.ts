import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminReceivableClient,
  ReceivableListItem,
  ReceivableStatus,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { buildWriteOffReceivableCommand, isOpenReceivable } from './receivables.models';

@Injectable()
export class ReceivablesFacade extends UnsubscribeControlDirective {
  private readonly receivableClient = inject(AdminReceivableClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly lang = currentLanguage(this.translate);
  readonly receivables = signal<ReceivableListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);
  readonly status = signal<ReceivableStatus | null>(ReceivableStatus.Open);

  readonly writingOff = signal<ReceivableListItem | null>(null);
  readonly writeOffNote = signal<string>('');
  readonly submitting = signal<boolean>(false);
  readonly canSubmitWriteOff = computed(
    () => !!this.writingOff() && this.writeOffNote().trim().length > 0 && !this.submitting()
  );

  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);
  private readonly currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadReceivables(): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.receivableClient
      .getPaged(
        this.status() ?? undefined,
        undefined,
        undefined,
        undefined,
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
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
          this.receivables.set(response.data ?? []);
          this.totalRecords.set(response.total ?? 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  selectStatus(status: ReceivableStatus | null): void {
    this.status.set(status);
    this.currentOffset.set(0);
    this.cancelWriteOff();
    this.loadReceivables();
  }

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadReceivables();
  }

  onSortChange(event: SortEvent): void {
    const sort = new SortDefinition();
    sort.field = event.field;
    sort.direction = event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    this.currentSort.set([sort]);
    this.loadReceivables();
  }

  viewOrder(row: ReceivableListItem): void {
    if (row.orderId) {
      this.router.navigate([CleansiaAdminRoute.ORDER_MANAGEMENT, row.orderId]);
    }
  }

  startWriteOff(row: ReceivableListItem): void {
    if (!row.id || !isOpenReceivable(row) || this.submitting()) return;
    this.writeOffNote.set('');
    this.writingOff.set(row);
  }

  setWriteOffNote(value: string): void {
    this.writeOffNote.set(value);
  }

  cancelWriteOff(): void {
    this.writingOff.set(null);
    this.writeOffNote.set('');
  }

  writeOff(): void {
    const receivableId = this.writingOff()?.id;
    const note = this.writeOffNote().trim();
    if (!receivableId || !note || this.submitting()) return;

    this.submitting.set(true);
    this.receivableClient
      .writeOff(buildWriteOffReceivableCommand(receivableId, note))
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated('pages.receivables.messages.write_off_success');
          this.cancelWriteOff();
        }
        this.loadReceivables();
      });
  }
}
