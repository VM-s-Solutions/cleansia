import { EmailType } from '@cleansia/admin-services';
import { toSnakeCase } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';

export function emailTypeLabel(translate: TranslateService, type: EmailType, fallback?: string): string {
  const key = `enums.email_type.${toSnakeCase(EmailType[type] ?? '')}`;
  const translated = translate.instant(key);
  return translated === key ? (fallback ?? '') : translated;
}
