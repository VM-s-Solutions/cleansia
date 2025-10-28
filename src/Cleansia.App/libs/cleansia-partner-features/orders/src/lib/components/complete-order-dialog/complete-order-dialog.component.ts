import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputTextarea } from 'primeng/inputtextarea';
import { InputNumberModule } from 'primeng/inputnumber';

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
  selector: 'cleansia-complete-order-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslateModule,
    ButtonModule,
    InputTextModule,
    InputTextarea,
    InputNumberModule,
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

  readonly form = this.fb.nonNullable.group({
    actualCompletionTimeMinutes: [
      this.data.estimatedTime,
      [Validators.required, Validators.min(1)],
    ],
    completionNotes: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  get estimatedTime(): number {
    return this.data.estimatedTime;
  }

  get actualTime(): number {
    return this.form.value.actualCompletionTimeMinutes || 0;
  }

  get delay(): number {
    return this.actualTime - this.estimatedTime;
  }

  get delayPercentage(): number {
    if (this.estimatedTime === 0) return 0;
    return Math.round((this.delay / this.estimatedTime) * 100);
  }

  get isDelayed(): boolean {
    return this.delay > 0;
  }

  get isOnTime(): boolean {
    return this.delay <= 0;
  }

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

    const result: CompleteOrderDialogResult = {
      actualCompletionTimeMinutes: this.form.value.actualCompletionTimeMinutes!,
      completionNotes: this.form.value.completionNotes!,
    };

    this.dialogRef.close(result);
  }
}
