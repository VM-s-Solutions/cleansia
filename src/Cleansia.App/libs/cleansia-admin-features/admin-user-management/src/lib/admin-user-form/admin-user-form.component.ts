import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AdminUserFormData, AdminUserFormFacade } from './admin-user-form.facade';

@Component({
  selector: 'cleansia-admin-user-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaTelephoneComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './admin-user-form.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [AdminUserFormFacade],
})
export class AdminUserFormComponent implements OnInit, OnDestroy {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(AdminUserFormFacade);

  private readonly mode = signal<'create' | 'edit'>('create');

  readonly isEditMode = computed(() => this.mode() === 'edit');
  readonly pageTitle = computed(() =>
    this.isEditMode()
      ? this.translate.instant('pages.admin_user_form.edit_title')
      : this.translate.instant('pages.admin_user_form.create_title')
  );

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(150)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(100)]],
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
    phoneNumber: ['', [Validators.maxLength(50)]],
  });

  private userLoadEffect = effect(() => {
    const user = this.facade.user();
    if (user && this.isEditMode()) {
      this.populateForm(user);
    }
  });

  ngOnInit(): void {
    const routeMode = this.route.snapshot.data['mode'] as 'create' | 'edit';
    if (routeMode) {
      this.mode.set(routeMode);
    }

    if (this.isEditMode()) {
      // In edit mode, password is not required
      this.form.get('password')?.clearValidators();
      this.form.get('password')?.updateValueAndValidity();

      // Email cannot be changed in edit mode
      this.form.get('email')?.disable();

      const userId = this.route.snapshot.paramMap.get('userId');
      if (userId) {
        this.facade.loadUser(userId);
      } else {
        this.router.navigate([CleansiaAdminRoute.ADMIN_USER_MANAGEMENT]);
      }
    }
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  private populateForm(user: {
    email?: string;
    firstName?: string;
    lastName?: string;
    phoneNumber?: string;
  }): void {
    this.form.patchValue({
      email: user.email ?? '',
      firstName: user.firstName ?? '',
      lastName: user.lastName ?? '',
      phoneNumber: user.phoneNumber ?? '',
    });
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.getRawValue();

    const data: AdminUserFormData = {
      email: formValue.email,
      password: formValue.password || undefined,
      firstName: formValue.firstName,
      lastName: formValue.lastName,
      phoneNumber: formValue.phoneNumber || undefined,
    };

    if (this.isEditMode()) {
      const userId = this.route.snapshot.paramMap.get('userId');
      if (userId) {
        this.facade.updateUser(userId, data);
      }
    } else {
      this.facade.createUser(data);
    }
  }

  onCancel(): void {
    this.facade.navigateBack();
  }
}