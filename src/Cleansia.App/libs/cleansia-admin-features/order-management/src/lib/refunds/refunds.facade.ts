import { Injectable, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AdminRefundClient,
  RefundListItem,
  RefundRepaymentMethod,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, filter, finalize, Observable, of, takeUntil } from 'rxjs';
import {
  buildRecordRefundRepaymentCommand,
  buildRetryRefundCommand,
  formatRefundAmount,
  isOwedRefund,
  NEEDS_ACTION,
  RefundStateFilter,
  statesFor,
} from './refunds.models';

@Injectable()
export class RefundsFacade extends UnsubscribeControlDirective {
  private readonly refundClient = inject(AdminRefundClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly lang = currentLanguage(this.translate);
  readonly refunds = signal<RefundListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);
  readonly state = signal<RefundStateFilter | null>(NEEDS_ACTION);
  readonly requestedFrom = signal<Date | null>(null);
  readonly requestedTo = signal<Date | null>(null);
  readonly userId = signal<string | null>(null);
  readonly orderId = signal<string | null>(null);
  readonly submitting = signal<boolean>(false);

  readonly repaying = signal<RefundListItem | null>(null);
  readonly reference = signal<string>('');
  readonly creditChosen = signal<boolean>(false);
  readonly canRecordTransfer = computed(
    () => !!this.repaying() && this.reference().trim().length > 0 && !this.submitting()
  );
  readonly canRecordCredit = computed(
    () => !!this.repaying()?.hasAccount && this.creditChosen() && !this.submitting()
  );

  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);
  private readonly currentSort = signal<SortDefinition[] | undefined>(undefined);

  init(): void {
    const params = this.route.snapshot.queryParamMap;
    this.userId.set(params.get('userId'));
    this.orderId.set(params.get('orderId'));
    this.loadRefunds();
  }

  loadRefunds(): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.refundClient
      .getPaged(
        statesFor(this.state()),
        this.userId() ?? undefined,
        this.orderId() ?? undefined,
        this.requestedFrom() ?? undefined,
        endOfDay(this.requestedTo()),
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
          this.refunds.set(response.data ?? []);
          this.totalRecords.set(response.total ?? 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  selectState(state: RefundStateFilter | null): void {
    this.state.set(state);
    this.reloadFromFirstPage();
  }

  selectRequestedFrom(date: Date | null): void {
    this.requestedFrom.set(date);
    this.reloadFromFirstPage();
  }

  selectRequestedTo(date: Date | null): void {
    this.requestedTo.set(date);
    this.reloadFromFirstPage();
  }

  clearScope(): void {
    this.userId.set(null);
    this.orderId.set(null);
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
    this.reloadFromFirstPage();
  }

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadRefunds();
  }

  onSortChange(event: SortEvent): void {
    const sort = new SortDefinition();
    sort.field = event.field;
    sort.direction = event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    this.currentSort.set([sort]);
    this.loadRefunds();
  }

  viewOrder(row: RefundListItem): void {
    if (row.orderId) {
      this.router.navigate([CleansiaAdminRoute.ORDER_MANAGEMENT, row.orderId]);
    }
  }

  openDispute(row: RefundListItem): void {
    if (row.disputeId) {
      this.router.navigate([CleansiaAdminRoute.DISPUTE_MANAGEMENT, row.disputeId]);
    }
  }

  retry(row: RefundListItem): void {
    if (!row.canRetry || this.submitting()) return;
    this.dialog
      .confirmTranslated(
        'pages.refunds.retry_confirm.body',
        'pages.refunds.retry_confirm.title',
        { order: row.displayOrderNumber ?? '', amount: formatRefundAmount(row, this.lang()) },
        { acceptLabelKey: 'pages.refunds.retry_confirm.yes' }
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() =>
        this.submit(this.refundClient.retry(buildRetryRefundCommand(row)), 'pages.refunds.messages.retry_done')
      );
  }

  startRepayment(row: RefundListItem): void {
    if (!isOwedRefund(row) || this.submitting()) return;
    this.reference.set('');
    this.creditChosen.set(false);
    this.repaying.set(row);
  }

  setReference(value: string): void {
    this.reference.set(value);
  }

  setCreditChosen(value: boolean): void {
    this.creditChosen.set(value);
  }

  cancelRepayment(): void {
    this.repaying.set(null);
    this.reference.set('');
    this.creditChosen.set(false);
  }

  recordTransfer(): void {
    const row = this.repaying();
    if (!row || !this.canRecordTransfer()) return;
    this.submit(
      this.refundClient.recordRepayment(
        buildRecordRefundRepaymentCommand(row, RefundRepaymentMethod.BankTransfer, this.reference().trim())
      ),
      'pages.refunds.messages.repayment_recorded'
    );
  }

  recordCredit(): void {
    const row = this.repaying();
    if (!row || !this.canRecordCredit()) return;
    this.submit(
      this.refundClient.recordRepayment(buildRecordRefundRepaymentCommand(row, RefundRepaymentMethod.Credit, undefined)),
      'pages.refunds.messages.repayment_recorded'
    );
  }

  private submit(call: Observable<unknown>, successKey: string): void {
    this.submitting.set(true);
    call
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated(successKey);
          this.cancelRepayment();
        }
        this.loadRefunds();
      });
  }

  private reloadFromFirstPage(): void {
    this.currentOffset.set(0);
    this.cancelRepayment();
    this.loadRefunds();
  }
}

function endOfDay(date: Date | null): Date | undefined {
  if (!date) return undefined;
  const end = new Date(date);
  end.setHours(23, 59, 59, 999);
  return end;
}
