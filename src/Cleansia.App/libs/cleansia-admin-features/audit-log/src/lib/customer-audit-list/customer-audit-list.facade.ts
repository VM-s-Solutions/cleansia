import { computed, Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import {
  CustomerActionAuditDto,
  CustomerAuditClient,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { buildOutcomeOptions } from '../audit-log/audit-log.models';
import { buildCustomerAuditActionOptions } from '../customer-audit-actions';
import { buildCustomerAuditFilterChips } from './customer-audit-list.models';

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
  private readonly translate = inject(TranslateService);

  readonly audits = signal<CustomerActionAuditDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);
  readonly hasError = signal<boolean>(false);

  readonly lang = currentLanguage(this.translate);
  readonly outcomeOptions = computed(() => {
    this.lang();
    return buildOutcomeOptions(this.translate);
  });
  readonly actionOptions = computed(() => {
    this.lang();
    return buildCustomerAuditActionOptions(this.translate);
  });
  readonly filterForm = inject(FormBuilder).group({
    userId: [''],
    action: [null as string | null],
    resourceType: [''],
    resourceId: [''],
    clientAudience: [''],
    occurredFrom: [null as Date | null],
    occurredTo: [null as Date | null],
    success: [null as boolean | null],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] => buildCustomerAuditFilterChips(value, this.translate),
    apply: (value) =>
      this.applyFilter({
        userId: emptyToUndefined(value.userId),
        action: emptyToUndefined(value.action),
        resourceType: emptyToUndefined(value.resourceType),
        resourceId: emptyToUndefined(value.resourceId),
        clientAudience: emptyToUndefined(value.clientAudience),
        occurredFrom: value.occurredFrom ?? undefined,
        occurredTo: value.occurredTo ?? undefined,
        success: value.success ?? undefined,
      }),
  });

  private readonly currentFilter = signal<CustomerAuditFilterParams | null>(null);
  private readonly currentOffset = signal<number>(0);
  private readonly currentLimit = signal<number>(20);
  private readonly currentSort = signal<SortDefinition[] | undefined>(undefined);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

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

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadAudits();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadAudits();
  }

  applyFilter(filter: CustomerAuditFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadAudits();
  }

}

function emptyToUndefined(value: string | null | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}
