import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaSelectComponent,
  CleansiaTextareaComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';

export interface ApproveDialogData {
  subtitle?: string;
  countries: ICleansiaSelectOption[];
}

export interface ApproveDialogResult {
  workCountryId: string;
  notes?: string;
}

@Component({
  selector: 'cleansia-approve-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslateModule,
    CleansiaButtonComponent,
    CleansiaSelectComponent,
    CleansiaTextareaComponent,
  ],
  templateUrl: './approve-dialog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApproveDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as ApproveDialogData;
  readonly loading = signal(false);

  readonly form = this.fb.nonNullable.group({
    workCountryId: ['', [Validators.required]],
    notes: ['', [Validators.maxLength(1000)]],
  });

  onCancel(): void {
    this.dialogRef.close();
  }

  onApprove(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { workCountryId, notes } = this.form.getRawValue();
    const result: ApproveDialogResult = {
      workCountryId,
      notes: notes.trim() ? notes : undefined,
    };

    this.dialogRef.close(result);
  }
}
