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
  template: `
    <div class="report-issue-dialog">
      <div class="report-issue-dialog__header">
        <i class="pi pi-exclamation-triangle report-issue-dialog__icon"></i>
        <h3>{{ 'pages.order_details.report_issue' | translate }}</h3>
      </div>
      <div class="report-issue-dialog__body">
        <label class="report-issue-dialog__label">
          {{ 'pages.order_details.issue_description' | translate }}
        </label>
        <textarea
          pInputTextarea
          [(ngModel)]="description"
          [rows]="5"
          [placeholder]="'pages.order_details.issue_description_placeholder' | translate"
          class="report-issue-dialog__textarea"
        ></textarea>
      </div>
      <div class="report-issue-dialog__footer">
        <p-button
          [label]="'global.actions.cancel' | translate"
          severity="secondary"
          [outlined]="true"
          (onClick)="onCancel()"
        />
        <p-button
          [label]="'global.actions.submit' | translate"
          severity="danger"
          [disabled]="!description.trim()"
          (onClick)="onSubmit()"
        />
      </div>
    </div>
  `,
  styles: [`
    .report-issue-dialog {
      padding: 1rem;
    }
    .report-issue-dialog__header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1.5rem;
    }
    .report-issue-dialog__header h3 {
      margin: 0;
      font-size: 1.25rem;
    }
    .report-issue-dialog__icon {
      color: var(--red-500);
      font-size: 1.5rem;
    }
    .report-issue-dialog__label {
      display: block;
      margin-bottom: 0.5rem;
      font-weight: 500;
    }
    .report-issue-dialog__textarea {
      width: 100%;
    }
    .report-issue-dialog__footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 1.5rem;
    }
  `],
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
