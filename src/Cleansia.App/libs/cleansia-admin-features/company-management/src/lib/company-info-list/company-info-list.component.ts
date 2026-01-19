import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  computed,
  inject,
  OnDestroy,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  CompanyInfoListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
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
import { CompanyInfoListFacade } from './company-info-list.facade';
import { getCompanyInfoTableDefinition } from './company-info-list.models';

@Component({
  selector: 'cleansia-admin-company-info-list',
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
    CleansiaLanguageSwitcherComponent,
    ReactiveFormsModule,
    ConfirmDialogModule,
  ],
  templateUrl: './company-info-list.component.html',
  providers: [CompanyInfoListFacade, ConfirmationService],
})
export class CompanyInfoListComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(CompanyInfoListFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');

  companyColumns!: TableColumn<CompanyInfoListItem>[];
  companyActions!: TableAction<CompanyInfoListItem>[];

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

    this.facade.loadCompanyInfos();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getCompanyInfoTableDefinition(
      {
        onEdit: this.editCompanyInfo.bind(this),
        onDelete: this.confirmDeleteCompanyInfo.bind(this),
      },
      this.translate,
      this.statusTemplate()
    );
    this.companyColumns = tableDef.columns;
    this.companyActions = tableDef.actions;
    this.cd.detectChanges();
  }

  getActiveStatusLabel(company: CompanyInfoListItem): string {
    return company.isActive
      ? this.translate.instant('global.status.active')
      : this.translate.instant('global.status.inactive');
  }

  getActiveStatusClass(company: CompanyInfoListItem): string {
    return company.isActive
      ? 'active-status-badge status-active'
      : 'active-status-badge status-inactive';
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
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

  createCompanyInfo(): void {
    this.facade.navigateToCreate();
  }

  editCompanyInfo(companyInfo: CompanyInfoListItem): void {
    this.facade.navigateToEdit(companyInfo);
  }

  confirmDeleteCompanyInfo(companyInfo: CompanyInfoListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.company_management.delete_confirm'),
      header: this.translate.instant('pages.company_management.delete_company'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteCompanyInfo(companyInfo);
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
        label: this.translate.instant('pages.company_management.filters.search'),
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
