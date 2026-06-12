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
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';

export type ReferralInterventionMode = 'reverse' | 'forceQualify';

export interface ReferralInterventionSubmit {
  mode: ReferralInterventionMode;
  reason: string;
}

const REASON_MAX = 500;

@Component({
  selector: 'cleansia-admin-referral-intervention-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    DialogModule,
    CleansiaButtonComponent,
    CleansiaTextareaComponent,
  ],
  templateUrl: './referral-intervention-dialog.component.html',
})
export class ReferralInterventionDialogComponent {
  private readonly fb = inject(FormBuilder);

  readonly visible = input<boolean>(false);
  readonly visibleChange = output<boolean>();

  readonly mode = input<ReferralInterventionMode>('reverse');
  readonly submitting = input<boolean>(false);

  readonly submitForm = output<ReferralInterventionSubmit>();

  readonly form = this.fb.group({
    reason: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(REASON_MAX)],
    }),
  });

  readonly headerKey = computed(() =>
    this.mode() === 'reverse'
      ? 'pages.loyalty_referrals.intervention.title_reverse'
      : 'pages.loyalty_referrals.intervention.title_force_qualify'
  );

  readonly hintKey = computed(() =>
    this.mode() === 'reverse'
      ? 'pages.loyalty_referrals.intervention.hint_reverse'
      : 'pages.loyalty_referrals.intervention.hint_force_qualify'
  );

  readonly submitKey = computed(() =>
    this.mode() === 'reverse'
      ? 'pages.loyalty_referrals.intervention.submit_reverse'
      : 'pages.loyalty_referrals.intervention.submit_force_qualify'
  );

  reset(): void {
    this.form.reset({ reason: '' });
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
    this.submitForm.emit({
      mode: this.mode(),
      reason: this.form.getRawValue().reason.trim(),
    });
  }
}
