import { CommonModule } from '@angular/common';
import {
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
  template: `
    <div class="cleansia-code-input">
      @for (digit of digits; track $index) {
        <input
          #digitInput
          type="text"
          inputmode="numeric"
          maxlength="1"
          class="cleansia-code-input__digit"
          [class.cleansia-code-input__digit--filled]="digit !== ''"
          [value]="digit"
          (input)="onDigitInput($event, $index)"
          (keydown)="onKeyDown($event, $index)"
          (paste)="onPaste($event)"
          (focus)="onFocus($index)"
          autocomplete="one-time-code"
        />
      }
    </div>
  `,
  styles: [`
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
          border-color: var(--primary-color, #3b82f6);
          box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.15);
        }

        &--filled {
          border-color: var(--primary-color, #3b82f6);
          background: var(--primary-50, #eff6ff);
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
  `],
})
export class CleansiaCodeInputComponent implements ControlValueAccessor {
  @ViewChildren('digitInput') digitInputs!: QueryList<ElementRef<HTMLInputElement>>;
  @Output() codeComplete = new EventEmitter<string>();

  length = input(6);
  digits: string[] = [];

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

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

  onDigitInput(event: Event, index: number): void {
    const input = event.target as HTMLInputElement;
    const value = input.value.replace(/\D/g, '');

    if (value.length > 0) {
      this.digits[index] = value[0];
      input.value = value[0];
      this.emitValue();

      if (index < this.length() - 1) {
        this.focusDigit(index + 1);
      }
    } else {
      this.digits[index] = '';
      input.value = '';
      this.emitValue();
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

  onPaste(event: ClipboardEvent): void {
    event.preventDefault();
    const pasted = (event.clipboardData?.getData('text') || '').replace(/\D/g, '');
    if (!pasted) return;

    for (let i = 0; i < this.length(); i++) {
      this.digits[i] = pasted[i] || '';
    }
    this.emitValue();

    const lastFilledIndex = Math.min(pasted.length, this.length()) - 1;
    this.focusDigit(lastFilledIndex);
  }

  onFocus(index: number): void {
    this.onTouched();
    const inputs = this.digitInputs?.toArray();
    if (inputs?.[index]) {
      inputs[index].nativeElement.select();
    }
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
    if (code.length === this.length() && code.replace(/\D/g, '').length === this.length()) {
      this.codeComplete.emit(code);
    }
  }
}
