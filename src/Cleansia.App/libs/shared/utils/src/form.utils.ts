import { AbstractControl, FormGroup } from '@angular/forms';

/**
 * Form utility functions for common form operations
 */
export class FormUtils {
  /**
   * Marks all fields in a form group as touched
   */
  static markAllFieldsAsTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);
      if (control) {
        control.markAsTouched();
        // Recursively mark nested form groups
        if (control instanceof FormGroup) {
          this.markAllFieldsAsTouched(control);
        }
      }
    });
  }

  /**
   * Marks all fields in a form group as dirty
   */
  static markAllFieldsAsDirty(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);
      if (control) {
        control.markAsDirty();
        if (control instanceof FormGroup) {
          this.markAllFieldsAsDirty(control);
        }
      }
    });
  }

  /**
   * Resets all fields in a form group
   */
  static resetAllFields(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);
      if (control) {
        control.reset();
        if (control instanceof FormGroup) {
          this.resetAllFields(control);
        }
      }
    });
  }

  /**
   * Gets all validation errors from a form group
   */
  static getAllFormErrors(formGroup: FormGroup): { [key: string]: any } {
    const formErrors: { [key: string]: any } = {};

    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);
      if (control) {
        if (control.errors) {
          formErrors[key] = control.errors;
        }
        // Handle nested form groups
        if (control instanceof FormGroup) {
          const nestedErrors = this.getAllFormErrors(control);
          if (Object.keys(nestedErrors).length > 0) {
            formErrors[key] = nestedErrors;
          }
        }
      }
    });

    return formErrors;
  }

  /**
   * Checks if any field in the form group has errors
   */
  static hasAnyErrors(formGroup: FormGroup): boolean {
    return Object.keys(this.getAllFormErrors(formGroup)).length > 0;
  }

  /**
   * Gets the first error message from a control
   */
  static getFirstErrorMessage(control: AbstractControl | null): string | null {
    if (!control || !control.errors) {
      return null;
    }

    const errorKey = Object.keys(control.errors)[0];
    return errorKey;
  }

  /**
   * Patches form values safely (only patches existing controls)
   */
  static safePatchValue(formGroup: FormGroup, values: any): void {
    const patchObject: any = {};

    Object.keys(values).forEach((key) => {
      if (formGroup.get(key) && values[key] !== undefined) {
        patchObject[key] = values[key];
      }
    });

    formGroup.patchValue(patchObject);
  }

  /**
   * Gets form value with null/undefined values converted to empty strings
   */
  static getFormValueWithDefaults(formGroup: FormGroup): any {
    const formValue = formGroup.value;
    const cleanedValue: any = {};

    Object.keys(formValue).forEach((key) => {
      const value = formValue[key];
      cleanedValue[key] = value ?? '';
    });

    return cleanedValue;
  }

  /**
   * Enables or disables all controls in a form group
   */
  static setFormGroupEnabled(formGroup: FormGroup, enabled: boolean): void {
    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);
      if (control) {
        if (enabled) {
          control.enable();
        } else {
          control.disable();
        }

        if (control instanceof FormGroup) {
          this.setFormGroupEnabled(control, enabled);
        }
      }
    });
  }

  /**
   * Checks if form group is valid and all required fields are filled
   */
  static isFormReadyForSubmission(formGroup: FormGroup): boolean {
    return formGroup.valid && formGroup.dirty;
  }

  /**
   * Validates specific control and returns validation status
   */
  static validateControl(control: AbstractControl | null): {
    isValid: boolean;
    errors: any;
    errorCount: number;
  } {
    if (!control) {
      return { isValid: true, errors: null, errorCount: 0 };
    }

    const errors = control.errors;
    return {
      isValid: !errors,
      errors,
      errorCount: errors ? Object.keys(errors).length : 0
    };
  }

  /**
   * Gets form submission data with cleaned values
   */
  static getSubmissionData(formGroup: FormGroup): any {
    const formValue = this.getFormValueWithDefaults(formGroup);

    // Remove empty strings and null values for optional fields
    const submissionData: any = {};
    Object.keys(formValue).forEach((key) => {
      const value = formValue[key];
      if (value !== '' && value !== null && value !== undefined) {
        submissionData[key] = value;
      }
    });

    return submissionData;
  }
}