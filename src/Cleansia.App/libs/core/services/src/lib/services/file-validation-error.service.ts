import { Injectable, inject } from '@angular/core';
import { ValidationErrors } from '@angular/forms';
import { ErrorCodesFns } from '@cleansia/pipes';
import { TranslateService } from '@ngx-translate/core';
import { SnackbarService } from './snackbar.service';

@Injectable({
  providedIn: 'root',
})
export class FileValidationErrorService {
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  /**
   * Handles file validation errors and displays appropriate snackbar messages
   * @param errors ValidationErrors from a form control
   * @returns boolean indicating if any errors were handled
   */
  handleFileValidationErrors(errors: ValidationErrors | null): boolean {
    if (!errors) {
      return false;
    }

    // Process errors in priority order
    const errorKeys = ['fileRequired', 'fileType', 'fileSize', 'fileCount'];

    for (const errorKey of errorKeys) {
      if (errors[errorKey]) {
        const errorFn = ErrorCodesFns[errorKey];
        if (errorFn) {
          const errorMessage = errorFn(this.translate, errors[errorKey]);
          this.snackbarService.showError(errorMessage);
          return true;
        }
      }
    }

    // Fallback for any other file-related errors
    const firstErrorKey = Object.keys(errors)[0];
    const errorFn = ErrorCodesFns[firstErrorKey];
    if (errorFn) {
      const errorMessage = errorFn(this.translate, errors[firstErrorKey]);
      this.snackbarService.showError(errorMessage);
      return true;
    }

    // Generic fallback
    this.snackbarService.showErrorTranslated('validation.file.generic_error');
    return true;
  }

  /**
   * Gets all file validation error messages as an array
   * @param errors ValidationErrors from a form control
   * @returns string array of error messages
   */
  getFileValidationErrors(errors: ValidationErrors | null): string[] {
    if (!errors) {
      return [];
    }

    const errorMessages: string[] = [];
    const fileErrorKeys = ['fileRequired', 'fileType', 'fileSize', 'fileCount'];

    for (const errorKey of fileErrorKeys) {
      if (errors[errorKey]) {
        const errorFn = ErrorCodesFns[errorKey];
        if (errorFn) {
          errorMessages.push(errorFn(this.translate, errors[errorKey]));
        }
      }
    }

    return errorMessages;
  }
}
