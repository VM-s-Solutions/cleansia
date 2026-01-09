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
  AdminUserListItem,
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
    CleansiaLanguageSwitcherComponent,
    ReactiveFormsModule,
    ConfirmDialogModule,
  ],
  templateUrl: './admin-user-management.component.html',
  providers: [AdminUserManagementFacade, ConfirmationService],
})
export class AdminUserManagementComponent implements AfterViewInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  protected readonly facade = inject(AdminUserManagementFacade);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);

  userTableDefinition!: TableDefinition<AdminUserListItem>;

  private lastSortField: string | null = null;
  private lastSortOrder: number | null = null;
  private destroy$ = new Subject<void>();

  filterForm = this.fb.group({
    searchTerm: [''],
  });

  ngAfterViewInit(): void {
    this.userTableDefinition = getAdminUserTableDefinition(
      {
        onEdit: this.editUser.bind(this),
        onToggleStatus: this.confirmToggleStatus.bind(this),
      },
      this.translate
    );

    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged(), takeUntil(this.destroy$))
      .subscribe(() => {
        this.applyFilters();
      });

    this.facade.loadUsers();
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

  createUser(): void {
    this.facade.navigateToCreateUser();
  }

  editUser(user: AdminUserListItem): void {
    this.facade.navigateToEditUser(user);
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
}