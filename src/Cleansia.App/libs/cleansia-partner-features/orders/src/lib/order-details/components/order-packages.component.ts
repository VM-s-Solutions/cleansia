import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { CleansiaSectionComponent } from '@cleansia/components';
import { PackageDetails } from '@cleansia/partner-services';
import { currentLanguage, formatMoney, localeFor } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

interface PackageRow {
  readonly id: string | undefined;
  readonly name: string | undefined;
  readonly description: string | undefined;
  readonly includedServices: readonly string[];
  readonly price: string;
}

@Component({
  selector: 'cleansia-partner-order-packages',
  standalone: true,
  imports: [CleansiaSectionComponent, TranslatePipe],
  templateUrl: './order-packages.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderPackagesComponent {
  private readonly translate = inject(TranslateService);
  private readonly lang = currentLanguage(this.translate);

  packages = input<PackageDetails[]>();

  // THE ORDER'S currency, bound by the parent. It used to default to 'CZK' and the parent never bound
  // it, so every package line was labelled CZK regardless of what the order was priced in -- while the
  // same screen showed the real currency twenty lines below. Empty rather than a code, so a missing
  // one prints a bare number instead of a confident wrong label.
  currencyCode = input<string>('');

  readonly rows = computed((): PackageRow[] => {
    const lang = this.lang();
    const code = this.currencyCode() || undefined;
    return (this.packages() ?? []).map((pkg) => ({
      id: pkg.id,
      name: pkg.name,
      description: pkg.description,
      includedServices: pkg.includedServices ?? [],
      price: formatMoney(pkg.price, code, localeFor(lang), { fractionDigits: 2 }),
    }));
  });
}
