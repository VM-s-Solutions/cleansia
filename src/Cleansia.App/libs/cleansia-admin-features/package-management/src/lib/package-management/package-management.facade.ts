import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  DeletePackageResponse,
  PackageListItem,
  SortDefinition,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface PackageFilterParams {
  searchTerm?: string;
}

@Injectable()
export class PackageManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly packages = signal<PackageListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<PackageFilterParams | null>(null);
  private currentPage = signal<number>(1);
  private currentPageSize = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadPackages(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminPackageClient
      .getPaged(
        this.currentPage(),
        this.currentPageSize(),
        filterParams?.searchTerm,
        this.currentSort()?.[0]?.field,
        this.currentSort()?.[0]?.direction === 0
      )
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.packages.set(response.data || []);
          this.totalRecords.set(response.total || 0);
        }
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  onPageChange(page: number, pageSize: number): void {
    this.currentPage.set(page);
    this.currentPageSize.set(pageSize);
    this.loadPackages();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadPackages();
  }

  applyFilter(filter: PackageFilterParams): void {
    this.currentFilter.set(filter);
    this.currentPage.set(1);
    this.loadPackages();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentPage.set(1);
    this.loadPackages();
  }

  formatCurrency(value: number | undefined): string {
    if (value === undefined || value === null) return '';
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: 'EUR',
    }).format(value);
  }

  navigateToCreatePackage(): void {
    this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT, 'create']);
  }

  navigateToEditPackage(pkg: PackageListItem): void {
    if (pkg.id) {
      this.router.navigate([CleansiaAdminRoute.PACKAGE_MANAGEMENT, pkg.id, 'edit']);
    }
  }

  deletePackage(pkg: PackageListItem): void {
    if (!pkg.id) return;

    this.adminClient.adminPackageClient
      .delete(pkg.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response: DeletePackageResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.package_management.messages.delete_success')
          );
          this.loadPackages();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}