import { Injectable, inject, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { Router } from '@angular/router';
import {
  AdminAuthService,
  AdminClient,
  AdminRole,
  AdminUserDetailDto,
  CreateAdminUserCommand,
  CreateAdminUserResponse,
  SetAdminRoleCommand,
  UpdateAdminUserCommand,
  UpdateAdminUserResponse,
} from '@cleansia/admin-services';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, filter, finalize, of, takeUntil } from 'rxjs';
import { DEFAULT_ADMIN_ROLE, resolveAdminUserFormErrorKey } from './admin-user-form.models';

export interface AdminUserFormData {
  email: string;
  password?: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  birthDate?: Date;
  preferredLanguageCode?: string;
  role?: AdminRole;
}

@Injectable()
export class AdminUserFormFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly authService = inject(AdminAuthService);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);

  readonly user = signal<AdminUserDetailDto | null>(null);
  readonly loading = signal<boolean>(false);
  readonly saving = signal<boolean>(false);
  readonly languageOptions = signal<ICleansiaSelectOption[]>([]);

  /** The last role the server confirmed for the loaded administrator. */
  readonly role = signal<AdminRole | null>(null);
  readonly roleSaving = signal<boolean>(false);
  /** The server refuses a role change on the caller's own account; the picker says so first. */
  readonly isSelf = signal<boolean>(false);

  private roleControl: FormControl<AdminRole | null> | null = null;

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
          this.isSelf.set(!!response.id && response.id === this.authService.getUserId());
          this.syncRole(response.adminRole ?? null);
        } else {
          this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT]);
        }
      });
  }

  connectRoleControl(control: FormControl<AdminRole | null>): void {
    this.roleControl = control;
    this.syncRole(this.role());
    control.valueChanges
      .pipe(
        takeUntil(this.destroyed$),
        filter((next): next is AdminRole => next !== null && next !== this.role())
      )
      .subscribe((next) => this.setRole(next));
  }

  loadLanguages(): void {
    this.adminClient.adminLanguageClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([]))
      )
      .subscribe((languages) => {
        this.languageOptions.set(
          (languages ?? [])
            .filter((lang) => Boolean(lang.code) && Boolean(lang.name))
            .map((lang) => ({ label: lang.name as string, value: lang.code }))
        );
      });
  }

  createUser(data: AdminUserFormData): void {
    this.saving.set(true);

    const command = new CreateAdminUserCommand();
    command.email = data.email;
    command.password = data.password;
    command.firstName = data.firstName;
    command.lastName = data.lastName;
    command.phoneNumber = data.phoneNumber || undefined;
    command.birthDate = data.birthDate;
    command.preferredLanguageCode = data.preferredLanguageCode || undefined;
    command.role = data.role ?? DEFAULT_ADMIN_ROLE;

    this.adminClient.adminUserClient
      .create(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveAdminUserFormErrorKey(error))
          );
          return of(null);
        }),
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

    const command = new UpdateAdminUserCommand();
    command.userId = userId;
    command.firstName = data.firstName;
    command.lastName = data.lastName;
    command.phoneNumber = data.phoneNumber || undefined;
    command.birthDate = data.birthDate;
    command.preferredLanguageCode = data.preferredLanguageCode || undefined;

    this.adminClient.adminUserClient
      .update(userId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showError(
            this.translate.instant(resolveAdminUserFormErrorKey(error))
          );
          return of(null);
        }),
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

  private setRole(role: AdminRole): void {
    const userId = this.user()?.id;
    if (!userId || this.isSelf()) {
      this.syncRole(this.role());
      return;
    }

    this.roleSaving.set(true);
    this.roleControl?.disable({ emitEvent: false });
    const command = new SetAdminRoleCommand();
    command.userId = userId;
    command.role = role;

    this.adminClient.adminUserClient
      .role(userId, command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.roleSaving.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.admin_user_form.messages.role_success')
          );
        }
        this.syncRole(response ? response.role : this.role());
      });
  }

  private syncRole(role: AdminRole | null): void {
    this.role.set(role);
    if (!this.roleControl) return;
    this.roleControl.setValue(role, { emitEvent: false });
    if (this.isSelf()) {
      this.roleControl.disable({ emitEvent: false });
    } else {
      this.roleControl.enable({ emitEvent: false });
    }
  }
}
