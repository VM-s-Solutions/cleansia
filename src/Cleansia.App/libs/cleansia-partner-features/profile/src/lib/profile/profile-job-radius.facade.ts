import { Injectable, computed, inject, signal } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { EmployeeItem, PartnerClient } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';
import {
  JobRadiusFormValue,
  canSubmitJobRadius,
  createJobRadiusForm,
  createUpdateJobRadiusCommand,
  toJobRadiusFormValue,
} from './profile-job-radius.models';

@Injectable()
export class ProfileJobRadiusFacade extends UnsubscribeControlDirective {
  private readonly partnerClient = inject(PartnerClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly formGroup = createJobRadiusForm(inject(FormBuilder).nonNullable);

  readonly loaded = signal(false);
  readonly loadFailed = signal(false);
  readonly saving = signal(false);

  private readonly employeeId = signal('');
  private readonly formValue = signal<JobRadiusFormValue>(
    this.formGroup.getRawValue()
  );

  readonly limitEnabled = computed(() => this.formValue().limitEnabled);
  readonly canSubmit = computed(() => canSubmitJobRadius(this.formValue()));

  constructor() {
    super();
    this.formGroup.valueChanges
      .pipe(takeUntil(this.destroyed$))
      .subscribe(() => this.formValue.set(this.formGroup.getRawValue()));
  }

  seed(employee: EmployeeItem): void {
    this.employeeId.set(employee.id ?? '');
    this.render(employee.jobRadiusKm);
    this.loadFailed.set(false);
    this.loaded.set(true);
  }

  markUnavailable(): void {
    this.loaded.set(false);
    this.loadFailed.set(true);
  }

  onSubmit(): void {
    if (this.saving() || !this.loaded() || !this.canSubmit()) {
      return;
    }

    const employeeId = this.employeeId();
    if (!employeeId) {
      this.snackbarService.showError(
        this.translate.instant('global.messages.profile.not_loaded')
      );
      return;
    }

    this.saving.set(true);

    this.partnerClient.employeeClient
      .updateJobRadius(
        createUpdateJobRadiusCommand(employeeId, this.formGroup.getRawValue())
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false))
      )
      .subscribe((response) => {
        if (!response) {
          return;
        }

        this.render(response.radiusKm);
        this.snackbarService.showSuccess(
          this.translate.instant('global.messages.profile.job_radius_saved')
        );
      });
  }

  private render(radiusKm: number | undefined): void {
    this.formGroup.setValue(toJobRadiusFormValue(radiusKm));
  }
}
