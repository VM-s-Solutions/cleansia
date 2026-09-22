import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaTextareaComponent } from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { notBlank } from '../dialog-validators';

export interface ReportIssueDialogData {
  orderId: string;
}

export interface ReportIssueDialogResult {
  description: string;
}

@Component({
  selector: 'cleansia-partner-report-issue-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, TranslateModule, CleansiaButtonComponent, CleansiaTextareaComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-issue-dialog.component.html',
})
export class ReportIssueDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as ReportIssueDialogData;

  readonly form = this.fb.nonNullable.group({
    description: ['', [Validators.required, notBlank]],
  });

  onCancel(): void {
    this.dialogRef.close();
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const result: ReportIssueDialogResult = {
      description: this.form.getRawValue().description.trim(),
    };
    this.dialogRef.close(result);
  }
}
