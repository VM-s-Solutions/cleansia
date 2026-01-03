import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaTextareaComponent } from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';

export interface RejectDialogData {
  title: string;
  subtitle?: string;
  reasonLabel?: string;
  reasonPlaceholder?: string;
}

export interface RejectDialogResult {
  reason: string;
}

@Component({
  selector: 'cleansia-reject-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslateModule,
    CleansiaButtonComponent,
    CleansiaTextareaComponent,
  ],
  templateUrl: './reject-dialog.component.html',
  styleUrl: './reject-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RejectDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as RejectDialogData;
  readonly loading = signal(false);

  readonly form = this.fb.nonNullable.group({
    reason: ['', [Validators.required, Validators.maxLength(500)]],
  });

  onCancel(): void {
    this.dialogRef.close();
  }

  onReject(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const result: RejectDialogResult = {
      reason: this.form.value.reason!,
    };

    this.dialogRef.close(result);
  }
}
