import { FormControl, FormGroup, Validators } from '@angular/forms';
import {
  BlobFileDto,
  EmployeeEntityType,
  EmployeeItem,
  UpdateEmployeeCommand,
  UpdateEmployeeTimeRangeDto,
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

export interface ProfileFormData {
  employeeId?: string;
  firstName?: string;
  lastName?: string;
  phone?: string;
  dateOfBirth?: Date | string | null;
  street?: string;
  city?: string;
  zipCode?: string;
  state?: string;
  countryId?: string;
  nationalityId?: string;
  passportId?: string;
  entityType?: EmployeeEntityType;
  registrationNumber?: string;
  vatNumber?: string;
  legalEntityName?: string;
  emergencyName?: string;
  emergencyPhone?: string;
  availability?: Record<string, TimeRange[]>;
  consent?: boolean;
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
      passportId: new FormControl(undefined, [
        Validators.required,
        Validators.minLength(5),
        Validators.maxLength(20),
        CustomValidators.passportId(),
      ]),
      entityType: new FormControl<EmployeeEntityType>(
        EmployeeEntityType.NaturalPerson,
        [Validators.required]
      ),
      registrationNumber: new FormControl(undefined, [
        Validators.required,
        Validators.maxLength(50),
      ]),
      vatNumber: new FormControl(undefined, [Validators.maxLength(50)]),
      legalEntityName: new FormControl(undefined, [Validators.maxLength(200)]),
      emergencyName: new FormControl(undefined, [Validators.maxLength(100)]),
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

  static createTimeRangeFormGroup(timeRange?: TimeRange): FormGroup {
    return new FormGroup({
      start: new FormControl(timeRange?.start || '', [Validators.required]),
      end: new FormControl(timeRange?.end || '', [Validators.required]),
    });
  }

  static mapEmployeeToFormData(employee: EmployeeItem): ProfileFormData {
    return {
      employeeId: employee.id || undefined,
      firstName: employee.firstName || undefined,
      lastName: employee.lastName || undefined,
      phone: employee.phoneNumber || '+420',
      dateOfBirth: employee.birthDate || employee.birthDate || null,
      street: employee.street || undefined,
      city: employee.city || undefined,
      zipCode: employee.zipCode || undefined,
      countryId: employee.countryId || undefined,
      nationalityId: employee.nationalityId || undefined,
      passportId: employee.passportId || undefined,
      entityType: employee.entityType ?? EmployeeEntityType.NaturalPerson,
      registrationNumber: employee.registrationNumber || undefined,
      vatNumber: employee.vatNumber || undefined,
      legalEntityName: employee.legalEntityName || undefined,
      emergencyName: employee.emergencyContactName || undefined,
      emergencyPhone: employee.emergencyContactPhone || undefined,
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
    formData: ProfileFormData,
    documents: BlobFileDto[]
  ): UpdateEmployeeCommand {
    const birthDate =
      formData.dateOfBirth instanceof Date
        ? formData.dateOfBirth
        : formData.dateOfBirth
          ? new Date(formData.dateOfBirth)
          : new Date();

    const command = new UpdateEmployeeCommand();
    command.employeeId = formData.employeeId;
    command.firstName = formData.firstName;
    command.lastName = formData.lastName;
    command.phone = formData.phone;
    command.birthDate = birthDate;
    command.street = formData.street;
    command.city = formData.city;
    command.zipCode = formData.zipCode;
    command.countryId = formData.countryId;
    command.state = formData.state;
    command.nationalityId = formData.nationalityId;
    command.passportId = formData.passportId;
    command.entityType = formData.entityType ?? EmployeeEntityType.NaturalPerson;
    command.registrationNumber = formData.registrationNumber;
    command.vatNumber = formData.vatNumber || undefined;
    command.legalEntityName =
      formData.entityType === EmployeeEntityType.LegalEntity
        ? formData.legalEntityName
        : undefined;
    command.emergencyName = formData.emergencyName;
    command.emergencyPhone = formData.emergencyPhone;
    command.documents = documents;
    // availability is deliberately NOT set. This form never edited it — the module that pretended to
    // was dead UI — and the real editor is the dedicated UpdateAvailability endpoint. Leaving the
    // member absent is what tells the server "unchanged"; sending an empty map would clear the
    // cleaner's schedule on every profile save. UpdateEmployee.cs guards the null side of that.
    command.consent = formData.consent ?? false;

    return command;
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
