import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-packages',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './order-packages.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderPackagesComponent {
  packages = input<any[]>();

  // THE ORDER'S currency, bound by the parent. It used to default to 'CZK' and the parent never bound
  // it, so every package line was labelled CZK regardless of what the order was priced in -- while the
  // same screen showed the real currency twenty lines below. Empty rather than a code, so a missing
  // one prints a bare number instead of a confident wrong label.
  currencyCode = input<string>('');
}
