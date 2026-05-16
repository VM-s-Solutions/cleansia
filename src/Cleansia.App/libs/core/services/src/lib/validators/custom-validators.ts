import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

export class CustomValidators {
  static minimumAge(minAge: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const birthDate = new Date(control.value);
      const today = new Date();
      const age = today.getFullYear() - birthDate.getFullYear();
      const monthDiff = today.getMonth() - birthDate.getMonth();

      if (
        monthDiff < 0 ||
        (monthDiff === 0 && today.getDate() < birthDate.getDate())
      ) {
        return age - 1 < minAge
          ? { minimumAge: { requiredAge: minAge, actualAge: age - 1 } }
          : null;
      }

      return age < minAge
        ? { minimumAge: { requiredAge: minAge, actualAge: age } }
        : null;
    };
  }

  static maximumAge(maxAge: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const birthDate = new Date(control.value);
      const today = new Date();
      const age = today.getFullYear() - birthDate.getFullYear();
      const monthDiff = today.getMonth() - birthDate.getMonth();

      let actualAge = age;
      if (
        monthDiff < 0 ||
        (monthDiff === 0 && today.getDate() < birthDate.getDate())
      ) {
        actualAge = age - 1;
      }

      return actualAge > maxAge ? { maximumAge: { maxAge, actualAge } } : null;
    };
  }

  static phoneNumber(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const phoneRegex = /^[\+]?[0-9\s\-\(\)]{10,15}$/;
      return phoneRegex.test(control.value) ? null : { phoneNumber: true };
    };
  }

  static iban(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const ibanRegex =
        /^[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}$/;
      return ibanRegex.test(control.value.replace(/\s/g, '').toUpperCase())
        ? null
        : { iban: true };
    };
  }

  static alphabeticOnly(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const alphabeticRegex = /^[a-zA-Z\s]+$/;
      return alphabeticRegex.test(control.value)
        ? null
        : { alphabeticOnly: true };
    };
  }

  static alphanumericOnly(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const alphanumericRegex = /^[0-9A-Za-z]+$/;
      return alphanumericRegex.test(control.value)
        ? null
        : { alphanumericOnly: true };
    };
  }

  static zipCode(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const zipCodeRegex = /^[0-9A-Za-z\s-]+$/;
      return zipCodeRegex.test(control.value) ? null : { zipCode: true };
    };
  }

  static passportId(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const nationalIdRegex = /^[0-9A-Za-z]+$/;
      return nationalIdRegex.test(control.value) ? null : { passportId: true };
    };
  }

  static taxId(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const taxIdRegex = /^[0-9A-Za-z]*$/;
      return taxIdRegex.test(control.value) ? null : { taxId: true };
    };
  }

  static strongPassword(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const hasUpperCase = /[A-Z]/.test(control.value);
      const hasLowerCase = /[a-z]/.test(control.value);
      const hasNumber = /\d/.test(control.value);
      const hasSpecialChar = /[!@#$%^&*()_+\-=\[\]{};':"\\|,.<>\/?]/.test(
        control.value
      );
      const minLength = control.value.length >= 8;

      const valid =
        hasUpperCase &&
        hasLowerCase &&
        hasNumber &&
        hasSpecialChar &&
        minLength;

      return valid
        ? null
        : {
            strongPassword: {
              hasUpperCase,
              hasLowerCase,
              hasNumber,
              hasSpecialChar,
              minLength,
            },
          };
    };
  }

  static confirmPassword(passwordControlName: string): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.parent) {
        return null;
      }

      const passwordControl = control.parent.get(passwordControlName);
      if (!passwordControl || !control.value) {
        return null;
      }

      return passwordControl.value === control.value
        ? null
        : { confirmPassword: true };
    };
  }

  static noWhitespace(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      const hasWhitespace = /\s/.test(control.value);
      return hasWhitespace ? { noWhitespace: true } : null;
    };
  }

  static url(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      try {
        new URL(control.value);
        return null;
      } catch {
        return { url: true };
      }
    };
  }

  static fileRequired(): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const files = control.value as FileList | File[] | null;

      if (
        !files ||
        (files instanceof FileList && files.length === 0) ||
        (Array.isArray(files) && files.length === 0)
      ) {
        return { fileRequired: true };
      }

      return null;
    };
  }

  static fileType(allowedTypes: string[]): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const files = control.value as FileList | File[] | null;

      if (!files) {
        return null;
      }

      const fileArray = files instanceof FileList ? Array.from(files) : files;
      const invalidFiles = fileArray.filter((file) => {
        const fileExtension = file.name.split('.').pop()?.toLowerCase();
        const mimeType = file.type.toLowerCase();

        return !allowedTypes.some((type) => {
          const normalizedType = type.toLowerCase().replace('.', '');
          return (
            fileExtension === normalizedType ||
            mimeType.includes(normalizedType)
          );
        });
      });

      return invalidFiles.length > 0
        ? {
            fileType: {
              allowedTypes,
              invalidFiles: invalidFiles.map((f) => f.name),
            },
          }
        : null;
    };
  }

  static fileSize(maxSizeInMB: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const files = control.value as FileList | File[] | null;

      if (!files) {
        return null;
      }

      const maxSizeInBytes = maxSizeInMB * 1024 * 1024;
      const fileArray = files instanceof FileList ? Array.from(files) : files;
      const oversizedFiles = fileArray.filter(
        (file) => file.size > maxSizeInBytes
      );

      return oversizedFiles.length > 0
        ? {
            fileSize: {
              maxSize: maxSizeInMB,
              oversizedFiles: oversizedFiles.map((f) => ({
                name: f.name,
                size: f.size,
              })),
            },
          }
        : null;
    };
  }

  static fileCount(min?: number, max?: number): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const files = control.value as FileList | File[] | null;

      if (!files) {
        return null;
      }

      const fileCount = files instanceof FileList ? files.length : files.length;

      if (min !== undefined && fileCount < min) {
        return { fileCount: { min, actual: fileCount, type: 'minimum' } };
      }

      if (max !== undefined && fileCount > max) {
        return { fileCount: { max, actual: fileCount, type: 'maximum' } };
      }

      return null;
    };
  }

  static imageFile(): ValidatorFn {
    return CustomValidators.fileType([
      'jpg',
      'jpeg',
      'png',
      'gif',
      'bmp',
      'webp',
    ]);
  }

  static documentFile(): ValidatorFn {
    return CustomValidators.fileType([
      'pdf',
      'doc',
      'docx',
      'txt',
      'jpg',
      'jpeg',
      'png',
    ]);
  }
}
