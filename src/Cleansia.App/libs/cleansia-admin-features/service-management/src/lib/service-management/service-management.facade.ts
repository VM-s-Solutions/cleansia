import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  DeleteServiceResponse,
  ServiceListItem,
  SortDefinition,
} from '@cleansia/admin-services';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface ServiceFilterParams {
  searchTerm?: string;
}

@Injectable()
export class ServiceManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly services = signal<ServiceListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<ServiceFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadServices(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminServiceClient
      .getPaged(
        filterParams?.searchTerm,
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
          this.services.set(response.data || []);
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
    this.loadServices();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadServices();
  }

  applyFilter(filter: ServiceFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadServices();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadServices();
  }

  formatCurrency(value: number | undefined): string {
    if (value === undefined || value === null) return '';
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: 'EUR',
    }).format(value);
  }

  navigateToCreateService(): void {
    this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT, 'create']);
  }

  navigateToEditService(service: ServiceListItem): void {
    if (service.id) {
      this.router.navigate([CleansiaAdminRoute.SERVICE_MANAGEMENT, service.id, 'edit']);
    }
  }

  deleteService(service: ServiceListItem): void {
    if (!service.id) return;

    this.adminClient.adminServiceClient
      .delete(service.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError(() => of(null))
      )
      .subscribe((response: DeleteServiceResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.service_management.messages.delete_success')
          );
          this.loadServices();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}