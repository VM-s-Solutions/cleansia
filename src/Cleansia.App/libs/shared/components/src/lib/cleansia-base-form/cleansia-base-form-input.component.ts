import {
  booleanAttribute,
  Component,
  computed,
  inject,
  Injector,
  input,
  OnChanges,
  OnDestroy,
  OnInit,
  Signal,
  SimpleChanges,
} from '@angular/core';
import {
  AbstractControl,
  ControlValueAccessor,
  FormControl,
  FormControlDirective,
  FormControlName,
  FormGroupDirective,
  NgControl,
  NgModel,
} from '@angular/forms';
import { Subject } from 'rxjs';
import { InputSize } from './cleansia-base-form.models';

@Component({
  template: '',
  standalone: true,
})
export class CleansiaBaseFormInputComponent
  implements ControlValueAccessor, OnInit, OnChanges, OnDestroy
{
  private injector = inject(Injector);
  protected ngControl: NgControl | null = null;

  formControl = new FormControl(); // Initialized here, updated in ngOnInit

  protected destroyed$ = new Subject<void>();

  inputSize = input<InputSize>('full-width');
  disabled = input(false, { transform: booleanAttribute });
  label = input<string>();
  required = input(false, { transform: booleanAttribute });
  readonlyInput = input(false, { transform: booleanAttribute }); // Renamed to avoid conflict with JS keyword
  showErrors = input(true, { transform: booleanAttribute });
  placeholder = input('');
  className = input<string>();
  showRequired = input(true, { transform: booleanAttribute });
  autocomplete = input('');

  isRequired: Signal<boolean> = computed(() => {
    if (this.ngControl) {
      const validator = this.ngControl.control?.validator?.(
        {} as AbstractControl
      );
      return this.required() || (validator && validator['required']);
    }
    return this.required();
  });

  onChange: (value: any) => void = () => {
    // Implemented by ControlValueAccessor
  };
  onTouch: () => void = () => {
    // Implemented by ControlValueAccessor
  };

  hasErrors(): boolean {
    return (
      !!this.formControl && this.formControl.invalid && this.formControl.touched
    );
  }

  ngOnInit(): void {
    this.ngControl = this.injector.get(NgControl, null, {
      optional: true,
      self: true,
    });
    if (this.ngControl !== null) {
      this.ngControl.valueAccessor = this;
    }

    if (this.ngControl) {
      if (this.ngControl instanceof FormControlName) {
        this.formControl =
          this.ngControl.control ||
          ((this.ngControl.formDirective as FormGroupDirective)?.form.controls[
            this.ngControl.name as string
          ] as FormControl);
      } else if (
        this.ngControl instanceof FormControlDirective ||
        this.ngControl instanceof NgModel
      ) {
        this.formControl = this.ngControl.control;
        if (this.ngControl instanceof NgModel) {
          this.formControl.valueChanges?.subscribe(() =>
            this.ngControl?.viewToModelUpdate(this.formControl.value)
          );
        }
      }
    }

    if (this.disabled()) {
      this.formControl.disable();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['disabled'] && this.formControl) {
      if (changes['disabled'].currentValue) {
        this.formControl.disable();
      } else {
        this.formControl.enable();
      }
    }
  }

  ngOnDestroy(): void {
    this.destroyed$.next();
    this.destroyed$.complete();
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouch = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    // Handled via signal and ngOnChanges
  }

  writeValue(value: any): void {
    // To be overridden by subclasses if needed
  }
}
