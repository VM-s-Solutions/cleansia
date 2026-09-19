import {
  booleanAttribute,
  ChangeDetectionStrategy,
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
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export abstract class CleansiaBaseFormInputComponent
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
      const validator = this.ngControl.control?.validator?.({} as AbstractControl);
      return this.required() || (validator && validator['required']);
    }
    return this.required();
  });

  onChange: (value: unknown) => void = () => {
    // Implemented by ControlValueAccessor
  };
  onTouch: () => void = () => {
    // Implemented by ControlValueAccessor
  };

  hasErrors(): boolean {
    return !!this.formControl && this.formControl.invalid && this.formControl.touched;
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
        // FormControlName receives its control only after this hook, so it is resolved from the
        // form by its full path: the bare name lands on the root group, which is a different
        // control inside a nested formGroupName and no control at all when the name exists only
        // there.
        const path = this.ngControl.path;
        this.formControl =
          this.ngControl.control ??
          ((path &&
            (this.ngControl.formDirective as FormGroupDirective | null)?.form.get(
              path
            )) as FormControl);
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

  registerOnChange(fn: (value: unknown) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouch = fn;
  }

  setDisabledState(): void {
    // Handled via signal and ngOnChanges
  }

  abstract writeValue(value: unknown): void;
}
