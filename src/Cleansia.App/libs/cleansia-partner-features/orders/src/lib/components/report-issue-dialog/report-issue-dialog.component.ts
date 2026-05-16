import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { InputTextarea } from 'primeng/inputtextarea';

export interface ReportIssueDialogData {
  orderId: string;
}

export interface ReportIssueDialogResult {
  description: string;
}

@Component({
  selector: 'cleansia-report-issue-dialog',
  standalone: true,
  imports: [FormsModule, TranslateModule, ButtonModule, InputTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './report-issue-dialog.component.html',
  styleUrl: './report-issue-dialog.component.scss',
})
export class ReportIssueDialogComponent {
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as ReportIssueDialogData;
  description = '';

  onCancel(): void {
    this.dialogRef.close();
  }

  onSubmit(): void {
    if (!this.description.trim()) return;
    const result: ReportIssueDialogResult = {
      description: this.description.trim(),
    };
    this.dialogRef.close(result);
  }
}
