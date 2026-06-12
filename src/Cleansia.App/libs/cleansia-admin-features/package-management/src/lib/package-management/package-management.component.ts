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
import {
  PackageListItem,
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
import { Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { PackageManagementFacade } from './package-management.facade';
import {
  CatalogStatusFilter,
  getPackageTableDefinition,
  mapStatusFilterToIsActive,
} from './package-management.models';

@Component({
  selector: 'cleansia-admin-package-management',
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
  templateUrl: './package-management.component.html',
  providers: [PackageManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PackageManagementComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  protected readonly facade = inject(PackageManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  packageColumns!: TableColumn<PackageListItem>[];
  packageActions!: TableAction<PackageListItem>[];
  statusFilterOptions!: ICleansiaSelectOption[];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.nonNullable.group({
    searchTerm: [''],
    status: ['all' as CatalogStatusFilter],
  });

  // Filter drawer state
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

    // Rebuild tables when language changes
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildTableDefinitions();
      });

    this.facade.loadPackages();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getPackageTableDefinition(
      {
        onEdit: this.editPackage.bind(this),
        onDelete: this.confirmDeletePackage.bind(this),
        onDeactivate: this.confirmDeactivatePackage.bind(this),
        onActivate: this.activatePackage.bind(this),
        getIsActiveFilter: () => this.facade.isActiveFilter(),
      },
      this.translate,
      this.facade.formatCurrency.bind(this.facade)
    );
    this.packageColumns = tableDef.columns;
    this.packageActions = tableDef.actions;

    this.statusFilterOptions = [
      {
        label: this.translate.instant('pages.package_management.filters.status_all'),
        value: 'all',
      },
      {
        label: this.translate.instant('pages.package_management.filters.status_active'),
        value: 'active',
      },
      {
        label: this.translate.instant('pages.package_management.filters.status_inactive'),
        value: 'inactive',
      },
    ];
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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

  createPackage(): void {
    this.facade.navigateToCreatePackage();
  }

  editPackage(pkg: PackageListItem): void {
    this.facade.navigateToEditPackage(pkg);
  }

  activatePackage(pkg: PackageListItem): void {
    this.facade.activatePackage(pkg);
  }

  confirmDeactivatePackage(pkg: PackageListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant(
        'pages.package_management.deactivate_confirm',
        { name: pkg.name }
      ),
      header: this.translate.instant('pages.package_management.deactivate_package'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deactivatePackage(pkg);
      },
    });
  }

  confirmDeletePackage(pkg: PackageListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.package_management.delete_confirm'),
      header: this.translate.instant('pages.package_management.delete_package'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deletePackage(pkg);
      },
    });
  }

  // Filter drawer methods
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
        label: this.translate.instant('pages.package_management.filters.search'),
        value: values.searchTerm,
      });
    }

    if (values.status && values.status !== 'all') {
      chips.push({
        key: 'status',
        label: this.translate.instant('pages.package_management.filters.status'),
        value: this.translate.instant(
          values.status === 'active'
            ? 'pages.package_management.filters.status_active'
            : 'pages.package_management.filters.status_inactive'
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
