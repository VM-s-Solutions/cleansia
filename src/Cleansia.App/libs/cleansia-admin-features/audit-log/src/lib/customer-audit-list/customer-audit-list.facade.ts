import { Injectable, inject, signal } from '@angular/core';
import {
  CustomerActionAuditDto,
  CustomerAuditClient,
  SortDefinition,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface CustomerAuditFilterParams {
  userId?: string;
  action?: string;
  resourceType?: string;
  resourceId?: string;
  occurredFrom?: Date;
  occurredTo?: Date;
  success?: boolean;
  clientAudience?: string;
}

@Injectable()
export class CustomerAuditListFacade extends UnsubscribeControlDirective {
  private readonly customerAuditClient = inject(CustomerAuditClient);

  readonly audits = signal<CustomerActionAuditDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);

  private readonly currentFilter = signal<CustomerAuditFilterParams | null>(null);
  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);
  private readonly currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadAudits(): void {
    this.loading.set(true);
    this.hasError.set(false);

    const filter = this.currentFilter();

    this.customerAuditClient
      .getPaged(
        filter?.userId,
        filter?.action,
        filter?.resourceType,
        filter?.resourceId,
        filter?.occurredFrom,
        filter?.occurredTo,
        filter?.success,
        filter?.clientAudience,
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
          this.audits.set(response.data ?? []);
          this.totalRecords.set(response.total ?? 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadAudits();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadAudits();
  }

  applyFilter(filter: CustomerAuditFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadAudits();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadAudits();
  }
}
