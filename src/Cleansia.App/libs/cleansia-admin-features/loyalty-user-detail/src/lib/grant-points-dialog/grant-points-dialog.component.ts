import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';

export type GrantPointsDialogMode = 'grant' | 'revoke';

export interface GrantPointsDialogSubmit {
  mode: GrantPointsDialogMode;
  points: number;
  reason: string;
}

const POINTS_MIN = 1;
const POINTS_MAX = 100000;
const REASON_MAX = 500;

@Component({
  selector: 'cleansia-admin-grant-points-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    DialogModule,
    CleansiaButtonComponent,
    CleansiaTextareaComponent,
    CleansiaTextInputComponent,
  ],
  templateUrl: './grant-points-dialog.component.html',
})
export class GrantPointsDialogComponent {
  private readonly fb = inject(FormBuilder);

  /** Two-way bound visibility flag — wire via [(visible)]. */
  readonly visible = input<boolean>(false);
  readonly visibleChange = output<boolean>();

  readonly mode = input<GrantPointsDialogMode>('grant');
  readonly submitting = input<boolean>(false);

  readonly submitForm = output<GrantPointsDialogSubmit>();

  readonly form = this.fb.group({
    points: this.fb.control<number | null>(null, {
      validators: [
        Validators.required,
        Validators.min(POINTS_MIN),
        Validators.max(POINTS_MAX),
      ],
    }),
    reason: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(REASON_MAX)],
    }),
  });

  readonly headerKey = computed(() =>
    this.mode() === 'grant'
      ? 'pages.loyalty_user_detail.grant_dialog.title_grant'
      : 'pages.loyalty_user_detail.grant_dialog.title_revoke'
  );

  readonly submitKey = computed(() =>
    this.mode() === 'grant'
      ? 'pages.loyalty_user_detail.grant_dialog.submit_grant'
      : 'pages.loyalty_user_detail.grant_dialog.submit_revoke'
  );

  reset(): void {
    this.form.reset({ points: null, reason: '' });
  }

  onVisibilityChanged(value: boolean): void {
    if (!value) {
      this.reset();
    }
    this.visibleChange.emit(value);
  }

  cancel(): void {
    this.onVisibilityChanged(false);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.submitForm.emit({
      mode: this.mode(),
      points: Number(v.points),
      reason: v.reason.trim(),
    });
  }
}
