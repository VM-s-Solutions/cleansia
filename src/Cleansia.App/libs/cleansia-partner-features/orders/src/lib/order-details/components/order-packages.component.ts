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
  currencyCode = input<string>('CZK');
}
