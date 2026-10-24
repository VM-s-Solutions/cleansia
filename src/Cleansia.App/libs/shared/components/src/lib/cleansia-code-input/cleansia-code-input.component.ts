import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Output,
  QueryList,
  ViewChildren,
  forwardRef,
  input,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'cleansia-code-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaCodeInputComponent),
      multi: true,
    },
  ],
  templateUrl: './cleansia-code-input.component.html',
  styles: [
    `
      .cleansia-code-input {
        display: flex;
        gap: 0.5rem;
        justify-content: center;

        &__digit {
          width: 3rem;
          height: 3.5rem;
          text-align: center;
          font-size: 1.5rem;
          font-weight: 600;
          border: 2px solid var(--surface-border, #dee2e6);
          border-radius: 8px;
          background: var(--surface-card, #fff);
          color: var(--text-color, #333);
          outline: none;
          transition: border-color 0.2s, box-shadow 0.2s;
          caret-color: transparent;

          &:focus {
            border-color: var(--p-primary-color);
            box-shadow: 0 0 0 2px rgba(var(--cleansia-primary-rgb), 0.15);
          }

          &--filled {
            border-color: var(--p-primary-color);
            background: var(--surface-hover, #f0f0f0);
            color: var(--text-color, #333);
          }
        }
      }

      @media (max-width: 480px) {
        .cleansia-code-input__digit {
          width: 2.5rem;
          height: 3rem;
          font-size: 1.25rem;
        }
      }
    `,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaCodeInputComponent implements ControlValueAccessor {
  @ViewChildren('digitInput') digitInputs!: QueryList<
    ElementRef<HTMLInputElement>
  >;
  @Output() codeComplete = new EventEmitter<string>();

  length = input(6);
  digits: string[] = [];

  // Placeholders until Angular calls registerOnChange / registerOnTouched. A
  // ControlValueAccessor has to be callable before the form binds to it.
  private onChange: (value: string) => void = () => {
    /* replaced by registerOnChange */
  };
  private onTouched: () => void = () => {
    /* replaced by registerOnTouched */
  };

  constructor() {
    this.digits = Array(this.length()).fill('');
  }

  writeValue(value: string | null): void {
    const code = (value || '').replace(/\D/g, '').slice(0, this.length());
    this.digits = Array(this.length())
      .fill('')
      .map((_, i) => code[i] || '');
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  // Safari's one-time-code AutoFill and the keyboard's suggestion strip put a whole code into the
  // focused box without a paste event, beside the digit it holds or in place of it. The code is what
  // the edit put in, so that digit is not run into it; one that replaced a selected digit it starts
  // with reads as five digits put in, and the box's whole value is the code then. Fewer digits put in
  // at once are written from this box onward, as a paste of them is; a single one is typing: the new
  // digit sits before the caret.
  onDigitInput(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    const caret = input.selectionStart ?? input.value.length;
    const inserted = insertedRun(input.value, this.digits[index], caret).replace(/\D/g, '');
    const code = inserted.length >= this.length() ? inserted : input.value.replace(/\D/g, '');

    if (code.length >= this.length()) {
      this.fillFrom(code);
      input.value = this.digits[index];
      return;
    }

    if (inserted.length > 1) {
      this.fillFrom(inserted, index);
      input.value = this.digits[index];
      return;
    }

    const typed = input.value.charAt(caret - 1);
    const typedDigit = /\d/.test(typed);
    const digit = typedDigit ? typed : code.slice(-1);
    this.digits[index] = digit;
    input.value = digit;
    this.emitValue();

    if (typedDigit && index < this.length() - 1) {
      this.focusDigit(index + 1);
    }
  }

  onKeyDown(event: KeyboardEvent, index: number): void {
    if (event.key === 'Backspace') {
      if (this.digits[index] === '' && index > 0) {
        this.focusDigit(index - 1);
        this.digits[index - 1] = '';
        this.emitValue();
        event.preventDefault();
      } else {
        this.digits[index] = '';
        this.emitValue();
      }
    } else if (event.key === 'ArrowLeft' && index > 0) {
      this.focusDigit(index - 1);
      event.preventDefault();
    } else if (event.key === 'ArrowRight' && index < this.length() - 1) {
      this.focusDigit(index + 1);
      event.preventDefault();
    }
  }

  // A whole code fills the boxes from the first, wherever it is pasted; fewer digits are written from
  // the box they are pasted into, as the apps insert them at the caret.
  onPaste(event: ClipboardEvent, index: number): void {
    event.preventDefault();
    const pasted = (event.clipboardData?.getData('text') || '').replace(
      /\D/g,
      ''
    );
    if (!pasted) return;

    this.fillFrom(pasted, pasted.length >= this.length() ? 0 : index);
  }

  onFocus(index: number): void {
    this.onTouched();
    const inputs = this.digitInputs?.toArray();
    if (inputs?.[index]) {
      inputs[index].nativeElement.select();
    }
  }

  private fillFrom(code: string, start = 0): void {
    const written = code.slice(0, this.length() - start);
    for (let i = 0; i < written.length; i++) {
      this.digits[start + i] = written[i];
    }
    this.emitValue();
    this.focusDigit(start + written.length - 1);
  }

  private focusDigit(index: number): void {
    setTimeout(() => {
      const inputs = this.digitInputs?.toArray();
      if (inputs?.[index]) {
        inputs[index].nativeElement.focus();
        inputs[index].nativeElement.select();
      }
    });
  }

  private emitValue(): void {
    const code = this.digits.join('');
    this.onChange(code);
    if (
      code.length === this.length() &&
      code.replace(/\D/g, '').length === this.length()
    ) {
      this.codeComplete.emit(code);
    }
  }
}

/**
 * What an edit put into a box that held `held`: its new value less that digit, which sits after the
 * caret when the edit went in before it. The apps' `OneTimeCode.accept` and `acceptOneTimeCode` take
 * the same run, with their caret held after the code.
 */
function insertedRun(value: string, held: string, caret: number): string {
  if (held === '') return value;
  if (value.slice(caret) === held) return value.slice(0, caret);
  return value.startsWith(held) ? value.slice(held.length) : value;
}
