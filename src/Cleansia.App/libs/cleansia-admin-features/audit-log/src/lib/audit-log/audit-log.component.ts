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
import { Router } from '@angular/router';
import {
  AdminActionAuditDto,
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
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { AuditLogFacade } from './audit-log.facade';
import {
  buildOutcomeOptions,
  getAuditLogTableActions,
  getAuditLogTableColumns,
  getOutcomeClass,
  getOutcomeLabelKey,
} from './audit-log.models';

@Component({
  selector: 'cleansia-admin-audit-log',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaCalendarComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    FormsModule,
    ReactiveFormsModule,
  ],
  templateUrl: './audit-log.component.html',
  providers: [AuditLogFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(AuditLogFacade);

  readonly outcomeTemplate =
    viewChild<TemplateRef<AdminActionAuditDto>>('outcomeTemplate');

  auditColumns!: TableColumn<AdminActionAuditDto>[];
  auditActions!: TableAction<AdminActionAuditDto>[];
  outcomeOptions: ICleansiaSelectOption[] = [];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private readonly destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    actorId: [''],
    actorEmail: [''],
    action: [''],
    resourceType: [''],
    resourceId: [''],
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

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
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
    this.auditColumns = getAuditLogTableColumns(
      this.translate,
      this.outcomeTemplate()
    );
    this.auditActions = getAuditLogTableActions(this.translate, (audit) =>
      this.viewEntry(audit)
    );
  }

  private rebuildFilterOptions(): void {
    this.outcomeOptions = buildOutcomeOptions(this.translate);
  }

  getOutcomeClass(audit: AdminActionAuditDto): string {
    return getOutcomeClass(audit.success);
  }

  getOutcomeLabelKey(audit: AdminActionAuditDto): string {
    return getOutcomeLabelKey(audit.success);
  }

  viewResourceHistory(audit: AdminActionAuditDto): void {
    if (!audit.resourceType || !audit.resourceId) return;
    this.router.navigate([
      CleansiaAdminRoute.AUDIT_LOG,
      'resource',
      audit.resourceType,
      audit.resourceId,
    ]);
  }

  viewEntry(audit: AdminActionAuditDto): void {
    if (!audit.id) return;
    this.router.navigate([CleansiaAdminRoute.AUDIT_LOG, 'entry', audit.id]);
  }

  applyFilters(): void {
    const values = this.filterForm.value;
    this.facade.applyFilter({
      actorId: emptyToUndefined(values.actorId),
      actorEmail: emptyToUndefined(values.actorEmail),
      action: emptyToUndefined(values.action),
      resourceType: emptyToUndefined(values.resourceType),
      resourceId: emptyToUndefined(values.resourceId),
      occurredFrom: values.occurredFrom ?? undefined,
      occurredTo: values.occurredTo ?? undefined,
      success: values.success ?? undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      actorId: '',
      actorEmail: '',
      action: '',
      resourceType: '',
      resourceId: '',
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
    this.facade.onSortChange([
      new SortDefinition({ field: event.field, direction }),
    ]);
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

  removeFilterChip(key: string): void {
    switch (key) {
      case 'actorId':
        this.filterForm.patchValue({ actorId: '' });
        break;
      case 'actorEmail':
        this.filterForm.patchValue({ actorEmail: '' });
        break;
      case 'action':
        this.filterForm.patchValue({ action: '' });
        break;
      case 'resourceType':
        this.filterForm.patchValue({ resourceType: '' });
        break;
      case 'resourceId':
        this.filterForm.patchValue({ resourceId: '' });
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

  private buildFilterChips(): { key: string; label: string; value: string }[] {
    const chips: { key: string; label: string; value: string }[] = [];
    const v = this.filterForm.value;

    if (v.actorId) {
      chips.push({
        key: 'actorId',
        label: this.translate.instant('pages.audit_log.filters.actor_id'),
        value: v.actorId,
      });
    }
    if (v.actorEmail) {
      chips.push({
        key: 'actorEmail',
        label: this.translate.instant('pages.audit_log.filters.actor_email'),
        value: v.actorEmail,
      });
    }
    if (v.action) {
      chips.push({
        key: 'action',
        label: this.translate.instant('pages.audit_log.filters.action'),
        value: v.action,
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
    if (v.occurredFrom || v.occurredTo) {
      chips.push({
        key: 'dateRange',
        label: this.translate.instant('pages.audit_log.filters.date_range'),
        value: [v.occurredFrom, v.occurredTo]
          .filter(Boolean)
          .map((d) => (d as Date).toLocaleDateString('en-GB'))
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
