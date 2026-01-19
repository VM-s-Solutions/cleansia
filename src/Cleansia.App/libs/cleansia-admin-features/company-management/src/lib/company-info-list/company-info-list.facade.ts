import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  CompanyInfoListItem,
  DeleteCompanyInfoResponse,
  SortDefinition,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface CompanyInfoFilterParams {
  searchTerm?: string;
  countryId?: string;
}

@Injectable()
export class CompanyInfoListFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly companyInfos = signal<CompanyInfoListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<CompanyInfoFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

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
        takeUntil(this.destroy$),
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

  onPageChange(offset: number, limit: number): void {
    this.currentOffset.set(offset);
    this.currentLimit.set(limit);
    this.loadCompanyInfos();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadCompanyInfos();
  }

  applyFilter(filter: CompanyInfoFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadCompanyInfos();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
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
        takeUntil(this.destroy$),
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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
