import { FormControl, FormGroup, Validators } from '@angular/forms';
import {
  BlobFileDto,
  CustomValidators,
  UpdateEmployeeCommand,
} from '@cleansia/services';
import { FileTransformationUtils } from '@cleansia/utils';

export class ProfileFormFactory {
  static createEmployeeProfileForm(): FormGroup {
    return new FormGroup({
      employeeId: new FormControl(undefined),
      firstName: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(100),
        CustomValidators.alphabeticOnly(),
      ]),
      lastName: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(2),
        Validators.maxLength(100),
        CustomValidators.alphabeticOnly(),
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
        CustomValidators.alphabeticOnly(),
      ]),
      zipCode: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(20),
        CustomValidators.zipCode(),
      ]),
      countryId: new FormControl(undefined, [Validators.required]),
      nationalityId: new FormControl(undefined, [Validators.required]),
      phone: new FormControl(undefined, [
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
      emergencyName: new FormControl(undefined, [
        Validators.maxLength(100),
        CustomValidators.alphabeticOnly(),
      ]),
      emergencyPhone: new FormControl(undefined, [
        CustomValidators.phoneNumber(),
      ]),
      documents: new FormControl(null, [
        CustomValidators.documentFile(),
        CustomValidators.fileSize(10),
        CustomValidators.fileCount(1, 10),
      ]),
      consent: new FormControl(false, [Validators.requiredTrue]),
    });
  }

  static mapEmployeeToFormData(employee: any): any {
    return {
      employeeId: employee.employeeId || undefined,
      firstName: employee.firstName || undefined,
      lastName: employee.lastName || undefined,
      email: employee.email || undefined,
      phone: employee.phoneNumber || employee.phone || undefined,
      dateOfBirth: employee.birthDate || employee.dateOfBirth || null,
      street: employee.street || undefined,
      city: employee.city || undefined,
      zipCode: employee.zipCode || undefined,
      countryId: employee.countryId || undefined,
      nationalityId: employee.nationalityId || undefined,
      passportId: employee.passportId || undefined,
      taxId: employee.taxId || undefined,
      iban: employee.iban || undefined,
      emergencyName: employee.emergencyName || undefined,
      emergencyPhone: employee.emergencyPhone || undefined,
      consent: employee.consent || false,
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

  /**
   * Creates an UpdateEmployeeCommand from form data and documents
   */
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
      nationalityId: formData.nationalityId,
      passportId: formData.passportId,
      taxId: formData.taxId,
      iban: formData.iban,
      emergencyName: formData.emergencyName,
      emergencyPhone: formData.emergencyPhone,
      documents,
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
