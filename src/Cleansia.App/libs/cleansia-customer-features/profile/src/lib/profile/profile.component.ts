import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { CustomerClient } from '@cleansia/customer-services';
import {
  ChangePasswordCommand,
  GetCurrentUserQuery,
  UpdateCurrentUserCommand,
  UserListItem,
} from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { TabsModule } from 'primeng/tabs';
import { SkeletonModule } from 'primeng/skeleton';

@Component({
  selector: 'cleansia-customer-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslateModule,
    InputTextModule,
    DatePickerModule,
    TabsModule,
    SkeletonModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit {
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);

  user = signal<UserListItem | null>(null);
  loading = signal(true);
  saving = signal(false);

  profileForm = new FormGroup({
    firstName: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    lastName: new FormControl('', [Validators.required, Validators.maxLength(50)]),
    phoneNumber: new FormControl('', [Validators.maxLength(20)]),
    birthDate: new FormControl<Date | null>(null),
  });

  passwordForm = new FormGroup({
    newPassword: new FormControl('', [
      Validators.required,
      Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/),
    ]),
    confirmPassword: new FormControl('', [Validators.required]),
  });

  ngOnInit(): void {
    this.loadProfile();
  }

  loadProfile(): void {
    this.loading.set(true);
    this.customerClient.userClient
      .getCurrent(new GetCurrentUserQuery())
      .subscribe({
        next: (user) => {
          this.user.set(user);
          this.profileForm.patchValue({
            firstName: user.firstName || '',
            lastName: user.lastName || '',
            phoneNumber: user.phoneNumber || '',
            birthDate: user.birthDate ? new Date(user.birthDate) : null,
          });
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
        },
      });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) return;
    this.saving.set(true);

    const user = this.user();
    const cmd = new UpdateCurrentUserCommand({
      id: user?.id,
      firstName: this.profileForm.value.firstName || undefined,
      lastName: this.profileForm.value.lastName || undefined,
      phoneNumber: this.profileForm.value.phoneNumber || undefined,
      birthDate: this.profileForm.value.birthDate || undefined,
      languageCode: this.translate.currentLang,
      photo: undefined as any,
    });

    this.customerClient.userClient.updateCurrentUser(cmd).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackbar.showSuccess(
          this.translate.instant('pages.profile.save_success')
        );
        this.loadProfile();
      },
      error: () => {
        this.saving.set(false);
        this.snackbar.showError(
          this.translate.instant('pages.profile.save_error')
        );
      },
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid || this.passwordMismatch) return;
    this.saving.set(true);

    const cmd = new ChangePasswordCommand({
      email: this.user()?.email,
      code: '',
      newPassword: this.passwordForm.value.newPassword || undefined,
    });

    this.customerClient.userClient.changePassword(cmd).subscribe({
      next: () => {
        this.saving.set(false);
        this.snackbar.showSuccess(
          this.translate.instant('pages.profile.save_success')
        );
        this.passwordForm.reset();
      },
      error: () => {
        this.saving.set(false);
        this.snackbar.showError(
          this.translate.instant('pages.profile.save_error')
        );
      },
    });
  }

  get passwordMismatch(): boolean {
    return (
      this.passwordForm.value.newPassword !== this.passwordForm.value.confirmPassword
    );
  }
}
