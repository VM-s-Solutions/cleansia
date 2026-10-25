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

  // In-field hints, separate from the aria labels above. One border around three boxes removes the
  // only other cue for which box is which, so an empty control without these is three anonymous gaps
  // around a dash and a slash. They are their own strings rather than the labels reused because the
  // labels are full ("Account prefix", "Номер рахунку") and the segments are sized in `ch`.
  readonly prefixPlaceholder = input<string>('');
  readonly numberPlaceholder = input<string>('');
  readonly bankCodePlaceholder = input<string>('');
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
   * Drives PrimeNG's `.p-floatlabel:has(input.p-filled) label` rule by hand.
   *
   * PrimeNG floats the label on `input:focus`, `input.p-filled` or an autofill — and `.p-filled` is
   * put there by its own `InputText` directive, which these segments deliberately do not use (the
   * border belongs to the wrapper). So without this the label floats on focus and drops back over the
   * typed digits on blur, and once there are placeholders it would sit on top of them permanently.
   *
   * Placeholders count as content for this purpose: they occupy the same space the resting label
   * would, so the label has to be out of the way whenever they are on screen.
   */
  protected filled(): boolean {
    return (
      [this.prefixPlaceholder(), this.numberPlaceholder(), this.bankCodePlaceholder()].some(
        (hint) => hint !== ''
      ) || [this.prefix(), this.number(), this.bankCode()].some((control) => control.value !== '')
    );
  }

  /**
   * A paste into one box, by the rule the apps' `splitPastedAccount` follows too
   * (→ /partner-app/onboarding). The run is the pasted text itself, which the paste event carries, with
   * whitespace of every kind dropped and an en or em dash read as a hyphen, because that is what banking
   * apps put on the clipboard. A run that is a written-out account (`19-2000145399/0800`,
   * `2000145399/0800`, `19-2000145399`) or a Czech or Slovak IBAN fills the three boxes before anything
   * else, however short it is, a missing prefix clearing the old one and a missing bank code keeping it.
   * Otherwise the box's new text is what the browser would make of the paste at the selection, and text
   * that fits the box is kept as pasted. Text that overflows it: a single character is typing, and the
   * box keeps the digits of its new text that fit; digits that fit the box replace what it held; longer
   * digits, up to the number's ten, are the account number and the box keeps what it held. Anything else
   * leaves the box as it was. Typing itself is the browser's, held to each box's maxlength.
   */
  protected onPaste(
    event: ClipboardEvent,
    box: HTMLInputElement,
    segment: FormControl<string>,
    segmentMaxLength: number
  ): void {
    const pasted = asSingleLine(event.clipboardData?.getData('text') ?? '');
    if (!pasted) return;
    event.preventDefault();

    const run = pasted.replace(/\s/g, '').replace(/[\u2013\u2014]/g, '-');
    const account = writtenOutAccount(domesticFormOfIban(run) ?? run);
    if (account) {
      this.prefix().setValue(account.prefix ?? '');
      this.number().setValue(account.number);
      if (account.bankCode) this.bankCode().setValue(account.bankCode);
      for (const control of [this.prefix(), this.number(), this.bankCode()]) {
        control.markAsDirty();
      }
      return;
    }

    const held = box.value;
    const start = box.selectionStart ?? held.length;
    const text = held.slice(0, start) + pasted + held.slice(box.selectionEnd ?? start);

    if (text.length <= segmentMaxLength) {
      this.write(segment, text);
      box.setSelectionRange(start + pasted.length, start + pasted.length);
      return;
    }

    if (run.length === 1) {
      this.write(segment, text.replace(/\D/g, '').slice(0, segmentMaxLength));
      return;
    }

    if (!/^\d+$/.test(run)) return;
    if (run.length <= segmentMaxLength) {
      this.write(segment, run);
    } else if (run.length <= this.NumberMaxLength) {
      this.write(this.number(), run);
    }
  }

  private write(control: FormControl<string>, value: string): void {
    control.setValue(value);
    control.markAsDirty();
  }
}

/** What a single-line field inserts of pasted text: trailing line breaks go, the others read as spaces. */
function asSingleLine(text: string): string {
  return text.replace(/[\r\n]+$/, '').replace(/\r\n|[\r\n]/g, ' ');
}

/** `prefix-number/code`, `number/code` or `prefix-number`; the separators are what mark an account. */
function writtenOutAccount(text: string): { prefix?: string; number: string; bankCode?: string } | null {
  if (!/[-/]/.test(text)) return null;
  const match = /^(?:(\d{1,6})-)?(\d{1,10})(?:\/(\d{1,4}))?$/.exec(text);
  return match ? { prefix: match[1], number: match[2], bankCode: match[3] } : null;
}

/**
 * `CZ65 0800 0000 1920 0014 5399` (spaces already dropped) reads as `19-2000145399/0800`: a Czech or
 * Slovak BBAN is bank code (4), prefix (6) and number (10), and the zero padding is not part of the
 * written account. Null for any other text. Shape only — the server checks the digits.
 */
function domesticFormOfIban(text: string): string | null {
  const bban = /^(?:CZ|SK)\d{2}(\d{20})$/.exec(text.toUpperCase())?.[1];
  if (!bban) return null;

  const prefix = bban.slice(4, 10).replace(/^0+/, '');
  const number = bban.slice(10).replace(/^0+/, '');
  return `${prefix ? `${prefix}-` : ''}${number}/${bban.slice(0, 4)}`;
}
