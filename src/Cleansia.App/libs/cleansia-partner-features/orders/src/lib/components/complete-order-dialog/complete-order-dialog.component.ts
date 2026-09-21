import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaTextareaComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';

export interface CompleteOrderDialogData {
  orderId: string;
  orderNumber: string;
  estimatedTime: number;
}

export interface CompleteOrderDialogResult {
  actualCompletionTimeMinutes: number;
  completionNotes: string;
}

@Component({
  selector: 'cleansia-partner-complete-order-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslateModule,
    CleansiaButtonComponent,
    CleansiaTextInputComponent,
    CleansiaTextareaComponent,
  ],
  templateUrl: './complete-order-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompleteOrderDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as CompleteOrderDialogData;
  readonly loading = signal(false);

  // The minutes control carries a string: the text input hands over what was typed.
  readonly form = this.fb.nonNullable.group({
    actualCompletionTimeMinutes: [
      String(this.data.estimatedTime),
      [Validators.required, Validators.min(1)],
    ],
    completionNotes: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  private readonly formValue = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  // Every control event, so a touch from markAllAsTouched re-reads the errors as a value change does.
  private readonly formEvents = toSignal(this.form.events);

  readonly estimatedTime = this.data.estimatedTime;
  readonly actualTime = computed(() => Number(this.formValue().actualCompletionTimeMinutes) || 0);
  readonly delay = computed(() => this.actualTime() - this.estimatedTime);
  readonly delayPercentage = computed(() =>
    this.estimatedTime === 0 ? 0 : Math.round((this.delay() / this.estimatedTime) * 100)
  );
  readonly isDelayed = computed(() => this.delay() > 0);

  readonly showActualTimeError = computed(() => {
    this.formEvents();
    const control = this.form.controls.actualCompletionTimeMinutes;
    return control.touched && control.invalid;
  });
  readonly showNotesRequiredError = computed(() => {
    this.formEvents();
    const control = this.form.controls.completionNotes;
    return control.touched && control.hasError('required');
  });
  readonly showNotesTooLongError = computed(() => {
    this.formEvents();
    const control = this.form.controls.completionNotes;
    return control.touched && control.hasError('maxlength');
  });

  formatMinutes(minutes: number): string {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours === 0) return `${mins}m`;
    if (mins === 0) return `${hours}h`;
    return `${hours}h ${mins}m`;
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onComplete(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { actualCompletionTimeMinutes, completionNotes } = this.form.getRawValue();
    const result: CompleteOrderDialogResult = {
      actualCompletionTimeMinutes: Number(actualCompletionTimeMinutes),
      completionNotes,
    };

    this.dialogRef.close(result);
  }
}
