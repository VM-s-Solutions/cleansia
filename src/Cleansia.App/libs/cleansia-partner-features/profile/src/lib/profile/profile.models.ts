import { FormControl, FormGroup, Validators } from '@angular/forms';
import {
  BlobFileDto,
  EmployeeItem,
  UpdateEmployeeCommand,
} from '@cleansia/partner-services';
import { CustomValidators } from '@cleansia/services';
import { FileTransformationUtils } from '@cleansia/utils';

export interface TimeRange {
  start: string; // HH:mm format
  end: string; // HH:mm format
}

export interface DayAvailability {
  day: string;
  timeRanges: TimeRange[];
}

export const DAYS_OF_WEEK = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
] as const;

export type DayOfWeek = (typeof DAYS_OF_WEEK)[number];

export class ProfileFormFactory {
  static createEmployeeProfileForm(): FormGroup {
    return new FormGroup({
      employeeId: new FormControl(undefined),
      firstName: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(100),
      ]),
      lastName: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(100),
      ]),
      dateOfBirth: new FormControl(null, [
        Validators.required,
        CustomValidators.minimumAge(18),
      ]),
      street: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(5),
        Validators.maxLength(255),
      ]),
      city: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(100),
      ]),
      zipCode: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(20),
        CustomValidators.zipCode(),
      ]),
      countryId: new FormControl(undefined, [Validators.required]),
      nationalityId: new FormControl(undefined, [Validators.required]),
      phone: new FormControl('+420', [
        Validators.required,
        CustomValidators.phoneNumber(),
      ]),
      email: new FormControl(undefined, [
        Validators.required,
        Validators.email,
        Validators.maxLength(254),
      ]),
      passportId: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(5),
        Validators.maxLength(20),
        CustomValidators.passportId(),
      ]),
      taxId: new FormControl(undefined, [
        Validators.required,
        Validators.maxLength(20),
        CustomValidators.taxId(),
      ]),
      iban: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(15),
        Validators.maxLength(34),
        CustomValidators.iban(),
      ]),
      emergencyName: new FormControl(undefined, [Validators.maxLength(100)]),
      emergencyPhone: new FormControl(undefined, [
        CustomValidators.phoneNumber(),
      ]),
      documents: new FormControl(null, [
        CustomValidators.documentFile(),
        CustomValidators.fileSize(10),
        CustomValidators.fileCount(1, 10),
      ]),
      availability: new FormControl<Record<string, TimeRange[]>>({}),
      consent: new FormControl(false, [Validators.requiredTrue]),
    });
  }

  static createTimeRangeFormGroup(timeRange?: TimeRange): FormGroup {
    return new FormGroup({
      start: new FormControl(timeRange?.start || '', [Validators.required]),
      end: new FormControl(timeRange?.end || '', [Validators.required]),
    });
  }

  static mapEmployeeToFormData(employee: EmployeeItem): any {
    return {
      employeeId: employee.id || undefined,
      firstName: employee.firstName || undefined,
      lastName: employee.lastName || undefined,
      email: employee.email || undefined,
      phone: employee.phoneNumber || '+420',
      dateOfBirth: employee.birthDate || employee.birthDate || null,
      street: employee.street || undefined,
      city: employee.city || undefined,
      zipCode: employee.zipCode || undefined,
      countryId: employee.countryId || undefined,
      nationalityId: employee.nationalityId || undefined,
      passportId: employee.passportId || undefined,
      taxId: employee.taxId || undefined,
      iban: employee.iban || undefined,
      emergencyName: employee.emergencyContactName || undefined,
      emergencyPhone: employee.emergencyContactPhone || undefined,
      availability: (employee as any).availability || {},
    };
  }

  static validateFormSection(
    formGroup: FormGroup,
    sectionName: string
  ): {
    isValid: boolean;
    errors: string[];
    invalidControls: string[];
  } {
    const errors: string[] = [];
    const invalidControls: string[] = [];

    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);
      if (control && control.invalid) {
        invalidControls.push(key);
        if (control.errors) {
          Object.keys(control.errors).forEach((errorKey) => {
            errors.push(`${sectionName}.${key}: ${errorKey}`);
          });
        }
      }
    });

    return {
      isValid: errors.length === 0,
      errors,
      invalidControls,
    };
  }

  static createUpdateCommand(
    formData: any,
    documents: BlobFileDto[]
  ): UpdateEmployeeCommand {
    return new UpdateEmployeeCommand({
      employeeId: formData.employeeId,
      firstName: formData.firstName,
      lastName: formData.lastName,
      email: formData.email,
      phone: formData.phone,
      birthDate: formData.dateOfBirth,
      street: formData.street,
      city: formData.city,
      zipCode: formData.zipCode,
      countryId: formData.countryId,
      state: formData.state,
      nationalityId: formData.nationalityId,
      passportId: formData.passportId,
      taxId: formData.taxId,
      iban: formData.iban,
      emergencyName: formData.emergencyName,
      emergencyPhone: formData.emergencyPhone,
      documents,
      availability: formData.availability,
      consent: formData.consent,
    });
  }

  static getUploadedFiles(formGroup: FormGroup): File[] {
    const files = formGroup.get('documents')?.value;
    return FileTransformationUtils.normalizeFiles(files);
  }

  static getFilesSizeInfo(formGroup: FormGroup): {
    totalSize: string;
    fileCount: number;
  } {
    const files = ProfileFormFactory.getUploadedFiles(formGroup);
    const totalBytes = FileTransformationUtils.getTotalFileSize(files);
    return {
      totalSize: FileTransformationUtils.formatFileSize(totalBytes),
      fileCount: files.length,
    };
  }
}
