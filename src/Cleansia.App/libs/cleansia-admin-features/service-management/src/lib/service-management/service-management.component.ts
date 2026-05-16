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
  ServiceListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  TableColumn,
  TableAction,
  PaginationState,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { ServiceManagementFacade } from './service-management.facade';
import { getServiceTableDefinition } from './service-management.models';

@Component({
  selector: 'cleansia-admin-service-management',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    ReactiveFormsModule,
    ConfirmDialogModule,
    CleansiaPermissionDirective,
  ],
  templateUrl: './service-management.component.html',
  providers: [ServiceManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ServiceManagementComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(ServiceManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  serviceColumns!: TableColumn<ServiceListItem>[];
  serviceActions!: TableAction<ServiceListItem>[];

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    searchTerm: [''],
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

    this.facade.loadServices();
  }

  private rebuildTableDefinitions(): void {
    const tableDefinition = getServiceTableDefinition(
      {
        onEdit: this.editService.bind(this),
        onDelete: this.confirmDeleteService.bind(this),
      },
      this.translate,
      this.facade.formatCurrency.bind(this.facade)
    );

    this.serviceColumns = tableDefinition.columns;
    this.serviceActions = tableDefinition.actions;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  viewServiceDetails(service: ServiceListItem): void {
    if (service.id) {
      this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT, service.id, 'edit']);
    }
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;

    this.facade.applyFilter({
      searchTerm: formValues.searchTerm?.trim() || undefined,
    });
  }

  resetFilters(): void {
    this.filterForm.reset({
      searchTerm: '',
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

  createService(): void {
    this.facade.navigateToCreateService();
  }

  editService(service: ServiceListItem): void {
    this.facade.navigateToEditService(service);
  }

  confirmDeleteService(service: ServiceListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.service_management.delete_confirm'),
      header: this.translate.instant('pages.service_management.delete_service'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteService(service);
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
    const values = this.filterForm.value;

    if (values.searchTerm) {
      chips.push({
        key: 'searchTerm',
        label: this.translate.instant('pages.service_management.filters.search'),
        value: values.searchTerm,
      });
    }

    return chips;
  }

  removeFilterChip(key: string): void {
    this.filterForm.patchValue({ [key]: '' });
    this.applyFilters();
  }

  clearAllFilters(): void {
    this.resetFilters();
  }
}