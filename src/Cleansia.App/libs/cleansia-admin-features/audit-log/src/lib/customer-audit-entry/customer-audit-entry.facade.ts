import { Injectable, computed, inject, signal } from '@angular/core';
import {
  CustomerActionAuditDetailDto,
  CustomerAuditClient,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { buildPayloadView, formatPayloadJson } from './customer-audit-entry.models';

@Injectable()
export class CustomerAuditEntryFacade extends UnsubscribeControlDirective {
  private readonly customerAuditClient = inject(CustomerAuditClient);

  readonly entry = signal<CustomerActionAuditDetailDto | null>(null);
  readonly loading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);
  readonly showRawJson = signal<boolean>(false);

  readonly payload = computed(() => buildPayloadView(this.entry()?.payloadJson));
  readonly hasPayload = computed(() => !!this.entry()?.payloadJson?.trim());
  readonly rawJson = computed(() => formatPayloadJson(this.entry()?.payloadJson));

  loadEntry(auditId: string): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.customerAuditClient
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

  toggleRawJson(): void {
    this.showRawJson.update((shown) => !shown);
  }
}
