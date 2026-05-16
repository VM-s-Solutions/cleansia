import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminUserDetailDto,
  CreateAdminUserCommand,
  CreateAdminUserResponse,
  UpdateAdminUserCommand,
  UpdateAdminUserResponse,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface AdminUserFormData {
  email: string;
  password?: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
}

@Injectable()
export class AdminUserFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly user = signal<AdminUserDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);

  loadUser(userId: string): void {
    this.loading.set(true);

    this.adminClient.adminUserClient
      .details(userId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.user.set(response);
        } else {
          this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT]);
        }
      });
  }

  createUser(data: AdminUserFormData): void {
    this.saving.set(true);

    const command = new CreateAdminUserCommand({
      email: data.email,
      password: data.password,
      firstName: data.firstName,
      lastName: data.lastName,
      phoneNumber: data.phoneNumber || undefined,
    });

    this.adminClient.adminUserClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: CreateAdminUserResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.admin_user_form.messages.create_success'
            )
          );
          this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT]);
        }
      });
  }

  updateUser(userId: string, data: AdminUserFormData): void {
    this.saving.set(true);

    const command = new UpdateAdminUserCommand({
      userId,
      firstName: data.firstName,
      lastName: data.lastName,
      phoneNumber: data.phoneNumber || undefined,
    });

    this.adminClient.adminUserClient
      .update(userId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response: UpdateAdminUserResponse | null) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.admin_user_form.messages.update_success'
            )
          );
          this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT]);
        }
      });
  }

  navigateBack(): void {
    this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT]);
  }
}