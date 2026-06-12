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
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { CleansiaAdminRoute, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import { resolveAdminUserFormErrorKey } from './admin-user-form.models';

export interface AdminUserFormData {
  email: string;
  password?: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  birthDate?: Date;
  preferredLanguageCode?: string;
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
  readonly languageOptions = signal<ICleansiaSelectOption[]>([]);

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

    const command = new CreateAdminUserCommand({
      email: data.email,
      password: data.password,
      firstName: data.firstName,
      lastName: data.lastName,
      phoneNumber: data.phoneNumber || undefined,
      birthDate: data.birthDate,
      preferredLanguageCode: data.preferredLanguageCode || undefined,
    });

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

    const command = new UpdateAdminUserCommand({
      userId,
      firstName: data.firstName,
      lastName: data.lastName,
      phoneNumber: data.phoneNumber || undefined,
      birthDate: data.birthDate,
      preferredLanguageCode: data.preferredLanguageCode || undefined,
    });

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
}
