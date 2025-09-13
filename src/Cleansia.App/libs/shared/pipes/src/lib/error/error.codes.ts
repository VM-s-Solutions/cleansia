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
};
