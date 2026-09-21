import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  CustomerActionAuditDto,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
  PaginationState,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import {
  buildOutcomeOptions,
  formatResource,
  getOutcomeClass,
  getOutcomeLabelKey,
} from '../audit-log/audit-log.models';
import { AuditLogSegmentComponent } from '../audit-log-segment/audit-log-segment.component';
import {
  buildCustomerAuditActionOptions,
  getAuditActionLabelKey,
} from '../customer-audit-actions';
import { CustomerAuditListFacade } from './customer-audit-list.facade';
import { getCustomerAuditTableDefinition } from './customer-audit-list.models';

interface FilterChip {
  key: string;
  label: string;
  value: string;
}

@Component({
  selector: 'cleansia-admin-customer-audit-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    AuditLogSegmentComponent,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './customer-audit-list.component.html',
  providers: [CustomerAuditListFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerAuditListComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(CustomerAuditListFacade);

  readonly userTemplate = viewChild<TemplateRef<CustomerActionAuditDto>>('userTemplate');
  readonly resourceTemplate = viewChild<TemplateRef<CustomerActionAuditDto>>('resourceTemplate');
  readonly outcomeTemplate = viewChild<TemplateRef<CustomerActionAuditDto>>('outcomeTemplate');

  auditColumns!: TableColumn<CustomerActionAuditDto>[];
  auditActions!: TableAction<CustomerActionAuditDto>[];
  outcomeOptions: ICleansiaSelectOption[] = [];
  actionOptions: ICleansiaSelectOption[] = [];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private readonly destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    userId: [''],
    action: [null as string | null],
    resourceType: [''],
    resourceId: [''],
    clientAudience: [''],
    occurredFrom: [null as Date | null],
    occurredTo: [null as Date | null],
    success: [null as boolean | null],
  });

  isFilterDrawerOpen = signal(false);
  private readonly filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    this.filterFormVersion();
    return this.buildFilterChips();
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();
    this.rebuildFilterOptions();
    this.cd.detectChanges();

    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.filterFormVersion.update((v) => v + 1));

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.rebuildTableDefinitions();
      this.rebuildFilterOptions();
      this.cd.detectChanges();
    });

    this.facade.loadAudits();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private rebuildTableDefinitions(): void {
    const definition = getCustomerAuditTableDefinition(
      { onView: (audit) => this.viewEntry(audit) },
      this.translate,
      {
        user: this.userTemplate(),
        resource: this.resourceTemplate(),
        outcome: this.outcomeTemplate(),
      }
    );
    this.auditColumns = definition.columns;
    this.auditActions = definition.actions;
  }

  private rebuildFilterOptions(): void {
    this.outcomeOptions = buildOutcomeOptions(this.translate);
    this.actionOptions = buildCustomerAuditActionOptions(this.translate);
  }

  getOutcomeClass(audit: CustomerActionAuditDto): string {
    return getOutcomeClass(audit.success);
  }

  getOutcomeLabelKey(audit: CustomerActionAuditDto): string {
    return getOutcomeLabelKey(audit.success);
  }

  formatResource(audit: CustomerActionAuditDto): string {
    return formatResource(audit);
  }

  customerRoute(audit: CustomerActionAuditDto): string[] | null {
    return audit.userId ? ['/customers', audit.userId] : null;
  }

  resourceHistoryRoute(audit: CustomerActionAuditDto): (string | CleansiaAdminRoute)[] | null {
    if (!audit.resourceType || !audit.resourceId) return null;
    return [CleansiaAdminRoute.AUDIT_LOG, 'resource', audit.resourceType, audit.resourceId];
  }

  viewEntry(audit: CustomerActionAuditDto): void {
    if (!audit.id) return;
    this.router.navigate([CleansiaAdminRoute.AUDIT_LOG, 'customers', 'entry', audit.id]);
  }

  applyFilters(): void {
    const values = this.filterForm.value;
    this.facade.applyFilter({
      userId: emptyToUndefined(values.userId),
      action: emptyToUndefined(values.action),
      resourceType: emptyToUndefined(values.resourceType),
      resourceId: emptyToUndefined(values.resourceId),
      clientAudience: emptyToUndefined(values.clientAudience),
      occurredFrom: values.occurredFrom ?? undefined,
      occurredTo: values.occurredTo ?? undefined,
      success: values.success ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      userId: '',
      action: null,
      resourceType: '',
      resourceId: '',
      clientAudience: '',
      occurredFrom: null,
      occurredTo: null,
      success: null,
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: number }): void {
    if (event.field === this.lastSortField && event.order === this.lastSortOrder) {
      return;
    }
    this.lastSortField = event.field;
    this.lastSortOrder = event.order;
    const direction =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    this.facade.onSortChange([new SortDefinition({ field: event.field, direction })]);
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  onOutcomeChange(value: boolean | null): void {
    this.filterForm.patchValue({ success: value });
  }

  onActionChange(value: string | null): void {
    this.filterForm.patchValue({ action: value });
  }

  removeFilterChip(key: string): void {
    switch (key) {
      case 'userId':
        this.filterForm.patchValue({ userId: '' });
        break;
      case 'action':
        this.filterForm.patchValue({ action: null });
        break;
      case 'resourceType':
        this.filterForm.patchValue({ resourceType: '' });
        break;
      case 'resourceId':
        this.filterForm.patchValue({ resourceId: '' });
        break;
      case 'clientAudience':
        this.filterForm.patchValue({ clientAudience: '' });
        break;
      case 'dateRange':
        this.filterForm.patchValue({ occurredFrom: null, occurredTo: null });
        break;
      case 'success':
        this.filterForm.patchValue({ success: null });
        break;
    }
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }

  private buildFilterChips(): FilterChip[] {
    const chips: FilterChip[] = [];
    const v = this.filterForm.value;

    if (v.userId) {
      chips.push({
        key: 'userId',
        label: this.translate.instant('pages.audit_log.customers.filters.user_id'),
        value: v.userId,
      });
    }
    if (v.action) {
      const labelKey = getAuditActionLabelKey(v.action);
      chips.push({
        key: 'action',
        label: this.translate.instant('pages.audit_log.customers.filters.action'),
        value: labelKey ? this.translate.instant(labelKey) : v.action,
      });
    }
    if (v.resourceType) {
      chips.push({
        key: 'resourceType',
        label: this.translate.instant('pages.audit_log.filters.resource_type'),
        value: v.resourceType,
      });
    }
    if (v.resourceId) {
      chips.push({
        key: 'resourceId',
        label: this.translate.instant('pages.audit_log.filters.resource_id'),
        value: v.resourceId,
      });
    }
    if (v.clientAudience) {
      chips.push({
        key: 'clientAudience',
        label: this.translate.instant('pages.audit_log.customers.filters.audience'),
        value: v.clientAudience,
      });
    }
    if (v.occurredFrom || v.occurredTo) {
      chips.push({
        key: 'dateRange',
        label: this.translate.instant('pages.audit_log.filters.date_range'),
        value: [v.occurredFrom, v.occurredTo]
          .filter(Boolean)
          .map((d) => formatDate(d as Date, this.translate.currentLang))
          .join(' – '),
      });
    }
    if (v.success != null) {
      chips.push({
        key: 'success',
        label: this.translate.instant('pages.audit_log.filters.outcome'),
        value: this.translate.instant(getOutcomeLabelKey(v.success)),
      });
    }

    return chips;
  }
}

function emptyToUndefined(value: string | null | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}
