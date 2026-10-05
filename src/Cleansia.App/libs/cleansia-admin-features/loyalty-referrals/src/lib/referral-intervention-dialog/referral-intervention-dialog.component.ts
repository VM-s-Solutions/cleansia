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
import {
  REFERRAL_INTERVENTION_COPY,
  ReferralInterventionMode,
  ReferralInterventionSubmit,
} from './referral-intervention-dialog.models';

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
  /** The credit the referral paid, formatted "referrer / referred"; null when nothing was paid. */
  readonly credit = input<string | null>(null);
  readonly submitting = input<boolean>(false);

  readonly submitForm = output<ReferralInterventionSubmit>();

  readonly form = this.fb.group({
    reason: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(REASON_MAX)],
    }),
  });

  private readonly copy = computed(() => REFERRAL_INTERVENTION_COPY[this.mode()]);
  readonly headerKey = computed(() => this.copy().titleKey);
  readonly hintKey = computed(() => this.copy().hintKey);
  readonly submitKey = computed(() => this.copy().submitKey);
  readonly destructive = computed(() => this.copy().destructive);

  readonly reverseSummary = computed(() => (this.mode() === 'reverse' ? this.credit() : null));

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
