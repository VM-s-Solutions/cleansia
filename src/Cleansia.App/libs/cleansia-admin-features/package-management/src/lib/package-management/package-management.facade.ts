import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  DeletePackageResponse,
  PackageListItem,
  SortDefinition,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface PackageFilterParams {
  searchTerm?: string;
}

@Injectable()
export class PackageManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly packages = signal<PackageListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<PackageFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadPackages(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminPackageClient
      .getPaged(
        filterParams?.searchTerm,
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
          this.packages.set(response.data || []);
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
    this.loadPackages();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadPackages();
  }

  applyFilter(filter: PackageFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadPackages();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadPackages();
  }

  formatCurrency(value: number | undefined): string {
    if (value === undefined || value === null) return '';
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: 'CZK',
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
        takeUntil(this.destroyed$),
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
}