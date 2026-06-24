import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AdminActionAuditDetailDto,
  AdminAuditLogClient,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { buildFieldDiff } from './audit-entry.models';

@Injectable()
export class AuditEntryFacade extends UnsubscribeControlDirective {
  private readonly auditClient = inject(AdminAuditLogClient);

  readonly entry = signal<AdminActionAuditDetailDto | null>(null);
  readonly loading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);

  readonly diff = computed(() => {
    const current = this.entry();
    if (!current) return [];
    return buildFieldDiff(current.beforeJson, current.afterJson);
  });

  readonly hasChanges = computed(() => this.diff().length > 0);

  loadEntry(auditId: string): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.auditClient
      .getById(auditId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          this.entry.set(null);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.entry.set(response);
        }
      });
  }
}
