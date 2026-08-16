import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  input,
  signal,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { FloatLabel } from 'primeng/floatlabel';

/**
 * A Czech bank account entered as ONE control: `prefix – number / bank code`.
 *
 * Three fields, one border. The separators are rendered rather than typed, because the format is the
 * bank's and not the customer's to remember — someone copying `19-2000145399/0800` off a statement
 * should not have to decide where the dash goes.
 *
 * **It takes the three FormControls rather than being a ControlValueAccessor over a composite.** The
 * account already exists as three columns (`AccountPrefix`, `AccountNumber`, `BankCode`), each with
 * its own validators, and a composite CVA would have to split and rejoin a value the server never
 * asked to be joined. Passing the controls through keeps every validator, every error message and the
 * DTO exactly where they were — this component changes how the field LOOKS and nothing else.
 *
 * Focus and invalid states are drawn on the wrapper, never on a segment: a focus ring around one third
 * of the control would undo the grouping it exists to create.
 */
@Component({
  selector: 'cleansia-bank-account',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FloatLabel],
  templateUrl: './cleansia-bank-account.component.html',
  styleUrl: './cleansia-bank-account.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaBankAccountComponent {
  /** Czech maxima: prefix 6 digits, number 10, bank code 4. */
  protected readonly PrefixMaxLength = 6;
  protected readonly NumberMaxLength = 10;
  protected readonly BankCodeMaxLength = 4;

  // FormControl<string>, not <string | null>: every form in this app is built with
  // NonNullableFormBuilder, and widening here would force a null check the callers cannot produce.
  readonly prefix = input.required<FormControl<string>>();
  readonly number = input.required<FormControl<string>>();
  readonly bankCode = input.required<FormControl<string>>();

  readonly label = input<string>('');
  readonly prefixLabel = input<string>('');
  readonly numberLabel = input<string>('');
  readonly bankCodeLabel = input<string>('');
  readonly floatVariant = input<'on' | 'in' | 'over'>('on');
  readonly id = input<string>(`cleansia-bank-account-${Math.random().toString(36).slice(2, 9)}`);

  protected readonly focused = signal(false);

  /**
   * Any segment being invalid reddens the whole control, because the user sees one control. Only
   * after it has been touched — a form that opens red teaches nothing.
   */
  protected invalid(): boolean {
    return [this.prefix(), this.number(), this.bankCode()].some(
      (control) => control.invalid && (control.touched || control.dirty)
    );
  }

  protected onBlur(): void {
    this.focused.set(false);
  }

  /**
   * Paste a whole account and it lands in the right segments. `19-2000145399/0800`,
   * `2000145399/0800` and a bare number all work, because those are the three shapes a Czech account
   * is actually written in — and the alternative is the customer deleting the punctuation by hand,
   * which is exactly the friction the single control is meant to remove.
   */
  protected onPaste(event: ClipboardEvent): void {
    const text = event.clipboardData?.getData('text')?.trim();
    if (!text) return;

    const match = /^(?:(\d{1,6})\s*-\s*)?(\d{1,10})(?:\s*\/\s*(\d{1,4}))?$/.exec(text);
    if (!match) return;

    event.preventDefault();
    const [, prefix, number, bankCode] = match;

    this.prefix().setValue(prefix ?? '');
    this.number().setValue(number);
    if (bankCode) this.bankCode().setValue(bankCode);

    for (const control of [this.prefix(), this.number(), this.bankCode()]) {
      control.markAsDirty();
    }
  }
}
