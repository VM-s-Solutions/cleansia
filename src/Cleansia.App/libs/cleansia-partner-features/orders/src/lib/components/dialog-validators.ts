import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Refuses whitespace-only text, which `Validators.required` accepts. */
export function notBlank(control: AbstractControl<string | null>): ValidationErrors | null {
  return typeof control.value === 'string' && control.value.trim().length === 0 && control.value.length > 0
    ? { blank: true }
    : null;
}
