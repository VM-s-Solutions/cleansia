import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  OnDestroy,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CustomValidators } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { AdminProfileFacade } from './admin-profile.facade';
import { PASSWORD_PATTERN } from './admin-profile.models';

@Component({
  selector: 'cleansia-admin-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './admin-profile.component.html',
  providers: [AdminProfileFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminProfileComponent implements OnDestroy {
  private readonly fb = inject(FormBuilder);
  protected readonly facade = inject(AdminProfileFacade);

  private readonly destroy$ = new Subject<void>();

  readonly form = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: [
      '',
      [Validators.required, Validators.pattern(PASSWORD_PATTERN)],
    ],
    confirmPassword: [
      '',
      [Validators.required, CustomValidators.confirmPassword('newPassword')],
    ],
  });

  private readonly resetOnSuccess = effect(() => {
    if (this.facade.passwordChanged() > 0) {
      this.form.reset();
    }
  });

  constructor() {
    this.form.controls.newPassword.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() =>
        this.form.controls.confirmPassword.updateValueAndValidity()
      );
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.facade.changePassword({
      currentPassword: value.currentPassword,
      newPassword: value.newPassword,
    });
  }
}
