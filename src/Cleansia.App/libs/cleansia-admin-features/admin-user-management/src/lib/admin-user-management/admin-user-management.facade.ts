import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminUserListItem,
  SortDefinition,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface AdminUserFilterParams {
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class AdminUserManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly users = signal<AdminUserListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<AdminUserFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadUsers(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    this.adminClient.adminUserClient
      .getPaged(
        filterParams?.searchTerm,
        filterParams?.isActive,
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
          this.users.set(response.data || []);
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
    this.loadUsers();
  }

  onSortChange(sort: SortDefinition[] | undefined): void {
    this.currentSort.set(sort);
    this.loadUsers();
  }

  applyFilter(filter: AdminUserFilterParams): void {
    this.currentFilter.set(filter);
    this.currentOffset.set(0);
    this.loadUsers();
  }

  resetFilter(): void {
    this.currentFilter.set(null);
    this.currentOffset.set(0);
    this.loadUsers();
  }

  navigateToCreateUser(): void {
    this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT, 'create']);
  }

  navigateToEditUser(user: AdminUserListItem): void {
    if (user.id) {
      this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT, user.id, 'edit']);
    }
  }

  deactivateUser(user: AdminUserListItem): void {
    if (!user.id) return;

    this.adminClient.adminUserClient
      .deactivate(user.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.admin_user_management.messages.deactivate_success'
            )
          );
          this.loadUsers();
        }
      });
  }

  activateUser(user: AdminUserListItem): void {
    if (!user.id) return;

    this.adminClient.adminUserClient
      .activate(user.id)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.admin_user_management.messages.activate_success'
            )
          );
          this.loadUsers();
        }
      });
  }

  toggleUserStatus(user: AdminUserListItem): void {
    if (user.isActive) {
      this.deactivateUser(user);
    } else {
      this.activateUser(user);
    }
  }
}