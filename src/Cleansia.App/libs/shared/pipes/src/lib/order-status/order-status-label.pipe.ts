import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { toSnakeCase } from '@cleansia/utils';

/**
 * Translates an order status code to its localized label.
 * Reads `enums.order_status.<snake_name>` from i18n; falls back to the
 * raw `name` if the key is missing.
 */
@Pipe({
  name: 'orderStatusLabel',
  standalone: true,
})
export class OrderStatusLabelPipe implements PipeTransform {
  private readonly translate = inject(TranslateService);

  transform(status: { name?: string } | null | undefined): string {
    const name = status?.name;
    if (!name) return '';
    const key = `enums.order_status.${toSnakeCase(name)}`;
    const translated = this.translate.instant(key);
    return translated === key ? name : translated;
  }
}
