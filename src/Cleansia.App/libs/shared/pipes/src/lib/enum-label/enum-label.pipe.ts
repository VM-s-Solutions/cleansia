import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { toSnakeCase } from '@cleansia/utils';

export type EnumLabelGroup = 'payment_type' | 'payment_status';

export function enumLabelKey(group: EnumLabelGroup, name: string | undefined): string | undefined {
  return name ? `enums.${group}.${toSnakeCase(name)}` : undefined;
}

/**
 * Translates a coded enum (`{ name }`) through `enums.<group>.<snake_name>`; falls back to
 * the raw `name` when the key is missing.
 */
// Impure so a runtime language switch repaints the label.
@Pipe({ name: 'enumLabel', standalone: true, pure: false })
export class EnumLabelPipe implements PipeTransform {
  private readonly translate = inject(TranslateService);

  transform(code: { name?: string } | null | undefined, group: EnumLabelGroup): string {
    const key = enumLabelKey(group, code?.name);
    if (!key) return '';
    const translated = this.translate.instant(key);
    return translated === key ? (code?.name ?? '') : translated;
  }
}
