import { Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminClient,
  CompanyInfoListItem,
  DeleteCompanyInfoResponse,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface CompanyInfoFilterParams {
  searchTerm?: string;
  countryId?: string;
}

@Injectable()
export class CompanyInfoListFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly lang = currentLanguage(this.translate);
  readonly companyInfos = signal<CompanyInfoListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  readonly filterForm = inject(FormBuilder).group({
    searchTerm: [''],
  });
  readonly filters = new FilterDrawerState({
    form: this.filterForm,
    lang: this.lang,
    chips: (value): FilterChip[] =>
      value.searchTerm
        ? [
            {
              key: 'searchTerm',
              label: this.translate.instant('pages.company_management.filters.search'),
              value: value.searchTerm,
            },
          ]
        : [],
    apply: (value) => this.applyFilter({ searchTerm: value.searchTerm?.trim() || undefined }),
  });

  private currentFilter = signal<CompanyInfoFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

  loadCompanyInfos(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminCompanyClient
      .getPaged(
        filterParams?.searchTerm,
        filterParams?.countryId,
        this.currentSort(),
        this.currentOffset(),
        this.currentLimit()
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.companyInfos.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadCompanyInfos();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadCompanyInfos();
  }

  applyFilter(filter: CompanyInfoFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadCompanyInfos();
  }

  navigateToCreate(): void {
    this.router.navigate([CleansiaAdminRoute.COMPANY_INFO, 'create']);
  }

  navigateToEdit(companyInfo: CompanyInfoListItem): void {
    if (companyInfo.id) {
      this.router.navigate([CleansiaAdminRoute.COMPANY_INFO, companyInfo.id, 'edit']);
    }
  }

  deleteCompanyInfo(companyInfo: CompanyInfoListItem): void {
    if (!companyInfo.id) return;

    this.adminClient.adminCompanyClient
      .delete(companyInfo.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response: DeleteCompanyInfoResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.company_management.messages.delete_success')
          );
          this.loadCompanyInfos();
        }
      });
  }
}
