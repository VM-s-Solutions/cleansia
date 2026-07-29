import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';

export interface MarkCashCollectedDialogData {
  orderId: string;
  /**
   * Pre-formatted order total (amount + currency symbol). Empty when the total
   * could not be resolved — the body copy then falls back to the amount-less
   * wording rather than showing a blank figure next to an irreversible action.
   */
  amount: string;
}

export interface MarkCashCollectedDialogResult {
  confirmed: boolean;
}

/**
 * Custom confirmation for recording a cash collection. Deliberately a dedicated
 * dialog component (same pattern as ReportIssueDialogComponent / AddNoteDialogComponent)
 * rather than window.confirm or the PrimeNG default confirm dialog: the action is
 * irreversible — it flips the order to Paid — so the copy has to name the amount.
 */
@Component({
  // The sibling dialogs predate the lint rule and still use the bare 'cleansia-' prefix;
  // this one is named to satisfy @angular-eslint/component-selector rather than inherit
  // their error. It is opened programmatically, so the selector is never written in a template.
  selector: 'cleansia-partner-mark-cash-collected-dialog',
  standalone: true,
  imports: [TranslateModule, ButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mark-cash-collected-dialog.component.html',
  styleUrl: './mark-cash-collected-dialog.component.scss',
})
export class MarkCashCollectedDialogComponent {
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as MarkCashCollectedDialogData;

  get hasAmount(): boolean {
    return !!this.data?.amount?.trim();
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  onConfirm(): void {
    const result: MarkCashCollectedDialogResult = { confirmed: true };
    this.dialogRef.close(result);
  }
}
