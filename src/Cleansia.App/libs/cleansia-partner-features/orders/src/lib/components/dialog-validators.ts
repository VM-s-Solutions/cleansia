import { AbstractControl, ValidationErrors } from '@angular/forms';

/**
 * Refuses whitespace-only text, which `Validators.required` accepts. It reports the required
 * error so the control prints its own "this field is required" message rather than an unknown one.
 */
export function notBlank(control: AbstractControl<string | null>): ValidationErrors | null {
  return typeof control.value === 'string' && control.value.trim().length === 0 && control.value.length > 0
    ? { required: true }
    : null;
}
