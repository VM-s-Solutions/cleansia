import { Injectable, inject, signal } from '@angular/core';
import { CustomerAuditClient, TimelineEntryDto } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export type TimelineKey =
  | { userId: string }
  | { resourceType: string; resourceId: string };

@Injectable()
export class TimelineFacade extends UnsubscribeControlDirective {
  private readonly customerAuditClient = inject(CustomerAuditClient);

  readonly entries = signal<TimelineEntryDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);

  private readonly key = signal<TimelineKey | null>(null);
  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);

  loadForUser(userId: string): void {
    this.key.set({ userId });
    this.currentOffset.set(0);
    this.loadEntries();
  }

  loadForResource(resourceType: string, resourceId: string): void {
    this.key.set({ resourceType, resourceId });
    this.currentOffset.set(0);
    this.loadEntries();
  }

  onPageChange(offset: number, limit: number): void {
    if (!this.key()) return;
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadEntries();
  }

  private loadEntries(): void {
    const key = this.key();
    if (!key) return;

    this.loading.set(true);
    this.hasError.set(false);

    // The query wants exactly one key; the generated client omits an undefined parameter entirely.
    const userId = 'userId' in key ? key.userId : undefined;
    const resourceType = 'resourceType' in key ? key.resourceType : undefined;
    const resourceId = 'resourceId' in key ? key.resourceId : undefined;

    this.customerAuditClient
      .timeline(
        userId,
        resourceType,
        resourceId,
        undefined,
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
          this.entries.set(response.data ?? []);
          this.totalRecords.set(response.total ?? 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }
}
