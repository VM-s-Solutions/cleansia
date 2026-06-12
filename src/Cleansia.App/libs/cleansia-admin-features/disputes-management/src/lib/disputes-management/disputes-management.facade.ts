import { Injectable, inject, signal } from '@angular/core';
import {
  AdminDisputeClient,
  DisputeListItem,
  DisputeStatus,
  SortDefinition,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface DisputeFilterParams {
  statuses?: DisputeStatus[];
  customerName?: string;
}

@Injectable()
export class DisputesManagementFacade extends UnsubscribeControlDirective {
  private readonly disputeClient = inject(AdminDisputeClient);

  readonly disputes = signal<DisputeListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);

  private readonly currentFilter = signal<DisputeFilterParams | null>(null);
  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);
  private readonly currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadDisputes(): void {
    this.loading.set(true);
    this.hasError.set(false);
    const filter = this.currentFilter();

    this.disputeClient
      .getPaged(
        undefined,
        undefined,
        filter?.customerName,
        undefined,
        filter?.statuses,
        undefined,
        undefined,
        undefined,
        undefined,
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
          this.disputes.set(response.data ?? []);
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
    this.loadDisputes();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadDisputes();
  }

  applyFilter(filter: DisputeFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadDisputes();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadDisputes();
  }
}
