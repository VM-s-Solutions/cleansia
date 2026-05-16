import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
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
  AdminUserListItem,
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
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { debounceTime, distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { AdminUserManagementFacade } from './admin-user-management.facade';
import { getAdminUserTableDefinition } from './admin-user-management.models';

@Component({
  selector: 'cleansia-admin-user-management',
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
    CleansiaPermissionDirective,
  ],
  templateUrl: './admin-user-management.component.html',
  providers: [AdminUserManagementFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminUserManagementComponent implements AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(AdminUserManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly permissions = inject(PermissionService);

  statusTemplate = viewChild<TemplateRef<any>>('statusTemplate');

  userColumns!: TableColumn<AdminUserListItem>[];
  userActions!: TableAction<AdminUserListItem>[];

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
      });

    this.facade.loadUsers();
  }

  private rebuildTableDefinitions(): void {
    const tableDef = getAdminUserTableDefinition(
      {
        onEdit: this.editUser.bind(this),
        onToggleStatus: this.confirmToggleStatus.bind(this),
        onViewLoyalty: this.viewLoyalty.bind(this),
      },
      this.translate,
      this.permissions,
      this.statusTemplate()
    );
    this.userColumns = tableDef.columns;
    this.userActions = tableDef.actions;
    this.cd.detectChanges();
  }

  getActiveStatusLabel(user: AdminUserListItem): string {
    return user.isActive
      ? this.translate.instant('global.status.active')
      : this.translate.instant('global.status.inactive');
  }

  getActiveStatusClass(user: AdminUserListItem): string {
    return user.isActive
      ? 'active-status-badge status-active'
      : 'active-status-badge status-inactive';
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

  onPageChange(event: PaginationState): void {
    const offset = event.first;
    const limit = event.rows;
    this.facade.onPageChange(offset, limit);
  }

  createUser(): void {
    this.facade.navigateToCreateUser();
  }

  editUser(user: AdminUserListItem): void {
    this.facade.navigateToEditUser(user);
  }

  viewLoyalty(user: AdminUserListItem): void {
    if (!user.id) return;
    this.router.navigate(['/loyalty/users', user.id], {
      queryParams: user.email ? { email: user.email } : undefined,
    });
  }

  confirmToggleStatus(user: AdminUserListItem): void {
    const messageKey = user.isActive
      ? 'pages.admin_user_management.deactivate_confirm'
      : 'pages.admin_user_management.activate_confirm';
    const headerKey = user.isActive
      ? 'pages.admin_user_management.deactivate_user'
      : 'pages.admin_user_management.activate_user';

    this.confirmationService.confirm({
      message: this.translate.instant(messageKey),
      header: this.translate.instant(headerKey),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.facade.toggleUserStatus(user);
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
        label: this.translate.instant('pages.admin_user_management.filters.search'),
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