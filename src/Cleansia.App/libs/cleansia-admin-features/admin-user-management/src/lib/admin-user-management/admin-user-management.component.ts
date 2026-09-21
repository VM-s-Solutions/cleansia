import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AdminUserListItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AdminUserManagementFacade } from './admin-user-management.facade';
import { getAdminUserTableDefinition } from './admin-user-management.models';

@Component({
  selector: 'cleansia-admin-user-management',
  standalone: true,
  imports: [
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaStatusBadgeComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    ReactiveFormsModule,
    CleansiaPermissionDirective,
  ],
  templateUrl: './admin-user-management.component.html',
  providers: [AdminUserManagementFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminUserManagementComponent implements OnInit {
  private readonly router = inject(Router);
  protected readonly facade = inject(AdminUserManagementFacade);
  protected readonly Policy = Policy;
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);

  private readonly statusTemplate = viewChild<TemplateRef<AdminUserListItem>>('statusTemplate');

  protected readonly table = computed(() => {
    this.facade.lang();
    return getAdminUserTableDefinition(
      {
        onEdit: (row) => this.facade.navigateToEditUser(row),
        onToggleStatus: (row) => this.confirmToggleStatus(row),
        onViewCustomer: (row) => this.viewCustomer(row),
      },
      this.translate,
      this.permissions,
      this.statusTemplate()
    );
  });

  ngOnInit(): void {
    this.facade.loadUsers();
  }

  viewCustomer(user: AdminUserListItem): void {
    if (!user.id) return;
    this.router.navigate(['/customers', user.id], {
      queryParams: user.email ? { email: user.email } : undefined,
    });
  }

  confirmToggleStatus(user: AdminUserListItem): void {
    this.facade.toggleUserStatus(user);
  }
}
