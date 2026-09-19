import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
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

const NOTE_MAX = 500;

export interface ExpireCreditDialogSubmit {
  currencyId: string;
  note: string;
}

/**
 * Taking a customer's whole credit balance in one currency off the books, now.
 *
 * <p><b>This is what lets a customer who wants to leave actually leave.</b> Erasure is refused while
 * a balance is positive and Cleansia does not do Stripe payouts, so without this an admin faces a
 * customer the platform can neither pay nor erase. → ExpireCustomerCredit</p>
 *
 * <p>A dialog rather than a confirm because the server requires a note, and that note is the only
 * record of why money the company owed stopped being owed. A confirm box has nowhere to put it.</p>
 *
 * <p>No amount field: the command only ever takes the whole balance. A partial discharge would need
 * a rule for what the remainder is now for, and there is no case that wants one — either the
 * customer is leaving or they are not.</p>
 */
@Component({
  selector: 'cleansia-admin-expire-credit-dialog',
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
  templateUrl: './expire-credit-dialog.component.html',
})
export class ExpireCreditDialogComponent {
  private readonly fb = inject(FormBuilder);

  /** Two-way bound visibility flag — wire via [(visible)]. */
  readonly visible = input<boolean>(false);
  readonly visibleChange = output<boolean>();

  readonly submitting = input<boolean>(false);

  /**
   * What is about to be discharged, shown back to the admin. The dialog does not compute it — the
   * balance it names must be the one the ledger above it shows, or the two disagree on screen. One
   * account per currency, so the currency IS the choice of which balance goes.
   */
  readonly balance = input<number>(0);
  readonly currencyId = input<string>('');
  readonly currencyCode = input<string>('');

  /** The account's currency and the note: the amount is always that account's whole balance. */
  readonly submitForm = output<ExpireCreditDialogSubmit>();

  readonly form = this.fb.group({
    note: this.fb.control<string>('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(NOTE_MAX)],
    }),
  });

  reset(): void {
    this.form.reset({ note: '' });
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
      currencyId: this.currencyId(),
      note: this.form.getRawValue().note.trim(),
    });
  }
}
