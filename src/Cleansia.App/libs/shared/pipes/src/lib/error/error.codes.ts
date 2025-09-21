import { TranslateService } from '@ngx-translate/core';

type ErrorCodesFn = (translate: TranslateService, value?: any) => string;

export const ErrorCodesFns: { [key: string]: ErrorCodesFn } = {
  required: (translate: TranslateService) =>
    translate.instant('validation.common.required'),
  email: (translate: TranslateService) =>
    translate.instant('validation.common.email'),
  birthDate: (translate: TranslateService, yearsRange: number) =>
    translate.instant('validation.common.birth_date', { yearsRange }),
  minlength: (translate: TranslateService, value: { requiredLength: number }) =>
    translate.instant('validation.common.min_length', {
      minLength: value.requiredLength,
    }),
  maxlength: (translate: TranslateService, value: { requiredLength: number }) =>
    translate.instant('validation.common.max_length', {
      maxLength: value.requiredLength,
    }),
  min: (translate: TranslateService, value: { min: number }) =>
    translate.instant('validation.common.min', {
      min: value.min,
    }),
  max: (translate: TranslateService, value: { max: number }) =>
    translate.instant('validation.common.max', {
      max: value.max,
    }),
  upperThresholdTooHigh: (translate: TranslateService) =>
    translate.instant('validation.common.upper_threshold_incorrect_range'),
  lowerThresholdTooHigh: (translate: TranslateService) =>
    translate.instant('validation.common.lower_threshold_incorrect_range'),
  pattern: (translate: TranslateService) =>
    translate.instant('validation.common.pattern'),
  fileType: (
    translate: TranslateService,
    value: { fileName?: string; acceptedTypes?: string; actualType?: string }
  ) =>
    translate.instant('validation.file.invalid_type', {
      fileName: value?.fileName || 'File',
      acceptedTypes: value?.acceptedTypes || 'allowed types',
    }),
  fileSize: (
    translate: TranslateService,
    value: { maxSize: number; fileName?: string; actualSize?: number }
  ) =>
    translate.instant('validation.file.max_size', {
      maxSize: value.maxSize,
      fileName: value?.fileName || 'File',
    }),
  fileCount: (
    translate: TranslateService,
    value: { type: string; min?: number; max?: number }
  ) => {
    if (value.type === 'minimum') {
      return translate.instant('validation.file.too_few_files', {
        min: value.min,
      });
    }
    return translate.instant('validation.file.too_many_files', {
      max: value.max,
    });
  },
  fileRequired: (translate: TranslateService) =>
    translate.instant('validation.file.required'),
  minimumAge: (
    translate: TranslateService,
    value: { requiredAge: number; actualAge: number }
  ) =>
    translate.instant('validation.common.minimum_age', {
      requiredAge: value.requiredAge,
      actualAge: value.actualAge,
    }),
  maximumAge: (
    translate: TranslateService,
    value: { maxAge: number; actualAge: number }
  ) =>
    translate.instant('validation.common.maximum_age', {
      maxAge: value.maxAge,
      actualAge: value.actualAge,
    }),
  phoneNumber: (translate: TranslateService) =>
    translate.instant('validation.common.phone_number'),
  iban: (translate: TranslateService) =>
    translate.instant('validation.common.iban'),
  alphabeticOnly: (translate: TranslateService) =>
    translate.instant('validation.common.alphabetic_only'),
  alphanumericOnly: (translate: TranslateService) =>
    translate.instant('validation.common.alphanumeric_only'),
  zipCode: (translate: TranslateService) =>
    translate.instant('validation.common.zip_code'),
  passportId: (translate: TranslateService) =>
    translate.instant('validation.common.national_id'),
  taxId: (translate: TranslateService) =>
    translate.instant('validation.common.tax_id'),
  strongPassword: (
    translate: TranslateService,
    value: {
      hasUpperCase: boolean;
      hasLowerCase: boolean;
      hasNumber: boolean;
      hasSpecialChar: boolean;
      minLength: boolean;
    }
  ) => {
    const requirements = [];
    if (!value.hasUpperCase)
      requirements.push(translate.instant('validation.password.uppercase'));
    if (!value.hasLowerCase)
      requirements.push(translate.instant('validation.password.lowercase'));
    if (!value.hasNumber)
      requirements.push(translate.instant('validation.password.number'));
    if (!value.hasSpecialChar)
      requirements.push(translate.instant('validation.password.special_char'));
    if (!value.minLength)
      requirements.push(translate.instant('validation.password.min_length'));
    return translate.instant('validation.password.requirements', {
      requirements: requirements.join(', '),
    });
  },
  confirmPassword: (translate: TranslateService) =>
    translate.instant('validation.common.confirm_password'),
  noWhitespace: (translate: TranslateService) =>
    translate.instant('validation.common.no_whitespace'),
  url: (translate: TranslateService) =>
    translate.instant('validation.common.url'),
};
