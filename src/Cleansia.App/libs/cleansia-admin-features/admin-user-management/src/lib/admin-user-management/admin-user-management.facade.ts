import { Injectable, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminUserListItem,
  SortDefinition,
  SortDirection,
} from '@cleansia/admin-services';
import { FilterChip, FilterDrawerState, PaginationState, SortEvent } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, DialogService, SnackbarService } from '@cleansia/services';
import { currentLanguage } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';

export interface AdminUserFilterParams {
  searchTerm?: string;
  isActive?: boolean;
}

@Injectable()
export class AdminUserManagementFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly dialog = inject(DialogService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly users = signal<AdminUserListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly totalRecords = signal<number>(0);

  readonly lang = currentLanguage(this.translate);
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
              label: this.translate.instant('pages.admin_user_management.filters.search'),
              value: value.searchTerm,
            },
          ]
        : [],
    apply: (value) => this.applyFilter({ searchTerm: value.searchTerm?.trim() || undefined }),
  });

  private currentFilter = signal<AdminUserFilterParams | null>(null);
  private currentOffset = signal<number>(0);
  private currentLimit = signal<number>(20);
  private currentSort = signal<SortDefinition[] | undefined>(undefined);

  constructor() {
    super();
    this.filters.connect(this.destroyed$);
  }

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

  onPageChange(event: PaginationState): void {
    this.currentOffset.set(event.first);
    this.currentLimit.set(event.rows);
    this.loadUsers();
  }

  onSortChange(event: SortEvent): void {
    this.currentSort.set([
      new SortDefinition({
        field: event.field,
        direction: event.order === 1 ? SortDirection.Ascending : SortDirection.Descending,
      }),
    ]);
    this.loadUsers();
  }

  applyFilter(filter: AdminUserFilterParams): void {
    this.currentFilter.set(filter);
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
          this.snackbarService.showSuccessTranslated(
            'pages.admin_user_management.messages.deactivate_success'
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
          this.snackbarService.showSuccessTranslated(
            'pages.admin_user_management.messages.activate_success'
          );
          this.loadUsers();
        }
      });
  }

  toggleUserStatus(user: AdminUserListItem): void {
    const action = user.isActive ? 'deactivate' : 'activate';
    this.dialog
      .confirmTranslated(
        `pages.admin_user_management.${action}_confirm`,
        `pages.admin_user_management.${action}_user`
      )
      .pipe(takeUntil(this.destroyed$), filter(Boolean))
      .subscribe(() => (user.isActive ? this.deactivateUser(user) : this.activateUser(user)));
  }
}