import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminUserFilter,
  AdminUserListItem,
  GetPagedAdminUsersRequest,
  SortDefinition,
} from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { Subject, catchError, finalize, of, takeUntil } from 'rxjs';

export interface AdminUserFilterParams {
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class AdminUserManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  private destroy$ = new Subject<void>();

  readonly users = signal<AdminUserListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly totalRecords = signal<number>(0);

  private currentFilter = signal<AdminUserFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  loadUsers(): void {
    this.loading.set(true);
    const filterParams = this.currentFilter();

    const adminUserFilter = new AdminUserFilter();
    if (filterParams?.searchTerm) {
      adminUserFilter.searchTerm = filterParams.searchTerm;
    }
    if (filterParams?.isActive !== undefined) {
      adminUserFilter.isActive = filterParams.isActive;
    }

    const request = new GetPagedAdminUsersRequest({
      offset: this.currentOffset(),
      limit: this.currentLimit(),
      filter: adminUserFilter,
      sort: this.currentSort(),
    });

    this.adminClient.adminUserClient
      .getPaged(request)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.admin_user_management.messages.load_error'
            )
          );
          console.error('Error loading admin users:', error);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.users.set(response.data || []);
          this.totalRecords.set(response.total || 0);
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
    this.router.navigate(['/admin-user-management', 'create']);
  }

  navigateToEditUser(user: AdminUserListItem): void {
    if (user.id) {
      this.router.navigate(['/admin-user-management', user.id, 'edit']);
    }
  }

  deactivateUser(user: AdminUserListItem): void {
    if (!user.id) return;

    this.adminClient.adminUserClient
      .deactivate(user.id)
      .pipe(
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.admin_user_management.messages.deactivate_error'
            )
          );
          console.error('Error deactivating user:', error);
          return of(null);
        })
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
        takeUntil(this.destroy$),
        catchError((error) => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.admin_user_management.messages.activate_error'
            )
          );
          console.error('Error activating user:', error);
          return of(null);
        })
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

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}