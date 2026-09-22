import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { CleansiaButtonComponent } from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';

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

// Not the shared confirmation dialog: the act flips the order to Paid and cannot be undone, so
// the copy has to name the amount.
@Component({
  selector: 'cleansia-partner-mark-cash-collected-dialog',
  standalone: true,
  imports: [TranslateModule, CleansiaButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mark-cash-collected-dialog.component.html',
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
