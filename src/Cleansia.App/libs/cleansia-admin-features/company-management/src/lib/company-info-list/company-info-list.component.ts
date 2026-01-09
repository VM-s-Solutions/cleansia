import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  inject,
  OnDestroy,
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
  TableDefinition,
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
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(CompanyInfoListFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  companyInfoTableDefinition!: TableDefinition<CompanyInfoListItem>;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    searchTerm: [''],
  });

  ngAfterViewInit(): void {
    this.companyInfoTableDefinition = getCompanyInfoTableDefinition(
      {
        onEdit: this.editCompanyInfo.bind(this),
        onDelete: this.confirmDeleteCompanyInfo.bind(this),
      },
      this.translate
    );

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    this.facade.loadCompanyInfos();
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
}
