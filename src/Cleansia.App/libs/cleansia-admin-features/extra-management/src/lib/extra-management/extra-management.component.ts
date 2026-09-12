import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CleansiaAdminRoute, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import {
  ExtraListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { ExtraManagementFacade } from './extra-management.facade';
import {
  CatalogStatusFilter,
  getExtraTableDefinition,
  mapStatusFilterToIsActive,
} from './extra-management.models';

@Component({
  selector: 'cleansia-admin-extra-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    ReactiveFormsModule,
    ConfirmDialogModule,
    CleansiaPermissionDirective,
  ],
  templateUrl: './extra-management.component.html',
  providers: [ExtraManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExtraManagementComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(ExtraManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  extraColumns!: TableColumn<ExtraListItem>[];
  extraActions!: TableAction<ExtraListItem>[];
  statusFilterOptions!: ICleansiaSelectOption[];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.nonNullable.group({
    searchTerm: [''],
    status: ['all' as CatalogStatusFilter],
  });

  isFilterDrawerOpen = signal(false);
  private filterFormVersion = signal(0);
  activeFilterChips = computed(() => {
    this.filterFormVersion();
    return this.getActiveFilterChips();
  });
  hasActiveFilters = computed(() => this.activeFilterChips().length > 0);
  activeFilterCount = computed(() => this.activeFilterChips().length);

  ngAfterViewInit(): void {
    this.rebuildTableDefinitions();

    this.filterForm.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterFormVersion.update(v => v + 1);
      });

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
      });

    this.facade.loadExtras();
  }

  private rebuildTableDefinitions(): void {
    const tableDefinition = getExtraTableDefinition(
      {
        onEdit: this.editExtra.bind(this),
        onDelete: this.confirmDeleteExtra.bind(this),
        onDeactivate: this.confirmDeactivateExtra.bind(this),
        onActivate: this.activateExtra.bind(this),
        getIsActiveFilter: () => this.facade.isActiveFilter(),
      },
      this.translate,
      this.facade.formatCurrency.bind(this.facade)
    );

    this.extraColumns = tableDefinition.columns;
    this.extraActions = tableDefinition.actions;

    this.statusFilterOptions = [
      {
        label: this.translate.instant('pages.extra_management.filters.status_all'),
        value: 'all',
      },
      {
        label: this.translate.instant('pages.extra_management.filters.status_active'),
        value: 'active',
      },
      {
        label: this.translate.instant('pages.extra_management.filters.status_inactive'),
        value: 'inactive',
      },
    ];
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewExtraDetails(extra: ExtraListItem): void {
    if (extra.id) {
      this.router.navigate([CleansiaAdminRoute.EXTRA_MANAGEMENT, extra.id, 'edit']);
    }
  }

  applyFilters(): void {
    const formValues = this.filterForm.getRawValue();

    this.facade.applyFilter({
      searchTerm: formValues.searchTerm.trim() || undefined,
      isActive: mapStatusFilterToIsActive(formValues.status ?? 'all'),
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      searchTerm: '',
      status: 'all',
    });
    this.facade.resetFilter();
  }

  onSortChange(event: { field: string; order: number }): void {
    if (
      event.field === this.lastSortField &&
      event.order === this.lastSortOrder
    ) {
      return;
    }

    this.lastSortField = event.field;
    this.lastSortOrder = event.order;

    const sortDirection =
      event.order === 1 ? SortDirection.Ascending : SortDirection.Descending;
    const sort = [
      new SortDefinition({
        field: event.field,
        direction: sortDirection,
      }),
    ];
    this.facade.onSortChange(sort);
  }

  onPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.onPageChange(offset, limit);
  }

  createExtra(): void {
    this.facade.navigateToCreateExtra();
  }

  editExtra(extra: ExtraListItem): void {
    this.facade.navigateToEditExtra(extra);
  }

  activateExtra(extra: ExtraListItem): void {
    this.facade.activateExtra(extra);
  }

  confirmDeactivateExtra(extra: ExtraListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.extra_management.deactivate_confirm',
        { name: extra.name }
      ),
      header: this.translate.instant('pages.extra_management.deactivate_extra'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deactivateExtra(extra);
      },
    });
  }

  confirmDeleteExtra(extra: ExtraListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.extra_management.delete_confirm'),
      header: this.translate.instant('pages.extra_management.delete_extra'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteExtra(extra);
      },
    });
  }

  openFilterDrawer(): void {
    this.isFilterDrawerOpen.set(true);
  }

  closeFilterDrawer(): void {
    this.isFilterDrawerOpen.set(false);
  }

  getActiveFilterChips(): { key: string; label: string; value: string }[] {
    const chips: { key: string; label: string; value: string }[] = [];
    const values = this.filterForm.getRawValue();

    if (values.searchTerm) {
      chips.push({
        key: 'searchTerm',
        label: this.translate.instant('pages.extra_management.filters.search'),
        value: values.searchTerm,
      });
    }

    if (values.status && values.status !== 'all') {
      chips.push({
        key: 'status',
        label: this.translate.instant('pages.extra_management.filters.status'),
        value: this.translate.instant(
          values.status === 'active'
            ? 'pages.extra_management.filters.status_active'
            : 'pages.extra_management.filters.status_inactive'
        ),
      });
    }

    return chips;
  }

  removeFilterChip(key: string): void {
    if (key === 'status') {
      this.filterForm.patchValue({ status: 'all' });
    } else {
      this.filterForm.patchValue({ [key]: '' });
    }
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }
}
