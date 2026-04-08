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
  LanguageListItem,
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
import { LanguageManagementFacade } from './language-management.facade';
import {
  getLanguageTableDefinition,
  getLanguageToCountryCode,
} from './language-management.models';

@Component({
  selector: 'cleansia-admin-language-management',
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
  ],
  templateUrl: './language-management.component.html',
  providers: [LanguageManagementFacade, ConfirmationService],
})
export class LanguageManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(LanguageManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  flagTemplate = viewChild<TemplateRef<any>>('flagTemplate');

  languageColumns!: TableColumn<LanguageListItem>[];
  languageActions!: TableAction<LanguageListItem>[];

  // Expose helper function to template
  getLanguageToCountryCode = getLanguageToCountryCode;

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
    this.cd.detectChanges();

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
        this.cd.detectChanges();
      });

    this.facade.loadLanguages();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getLanguageTableDefinition(
      {
        onEdit: this.editLanguage.bind(this),
        onDelete: this.confirmDeleteLanguage.bind(this),
      },
      this.translate,
      this.flagTemplate()
    );
    this.languageColumns = tableDef.columns;
    this.languageActions = tableDef.actions;
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  applyFilters(): void {
    // Filtering is handled client-side in the table component
  }

  resetFilters(): void {
    this.filterForm.reset({
      searchTerm: '',
    });
  }

  onSortChange(event: { field: string; order: number }): void {
    // Sorting is handled client-side in the table component
  }

  createLanguage(): void {
    this.facade.navigateToCreateLanguage();
  }

  editLanguage(language: LanguageListItem): void {
    this.facade.navigateToEditLanguage(language);
  }

  confirmDeleteLanguage(language: LanguageListItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('pages.language_management.delete_confirm'),
      header: this.translate.instant('pages.language_management.delete_language'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.deleteLanguage(language);
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
        label: this.translate.instant('pages.language_management.filters.search'),
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