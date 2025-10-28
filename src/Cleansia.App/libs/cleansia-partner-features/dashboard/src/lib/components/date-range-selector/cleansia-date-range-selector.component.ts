import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CleansiaLabelComponent } from '@cleansia/components';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'cleansia-date-range-selector',
  standalone: true,
  imports: [CommonModule, ButtonModule, TranslateModule, CleansiaLabelComponent],
  templateUrl: './cleansia-date-range-selector.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaDateRangeSelectorComponent {
  startDate = input<Date>(new Date());
  endDate = input<Date>(new Date());
  rangeChanged = output<{ startDate: Date; endDate: Date }>();

  selectedPreset: string = 'last6Months';

  constructor(private translate: TranslateService) {}

  selectPreset(preset: string): void {
    this.selectedPreset = preset;
    const today = new Date();
    let startDate: Date;
    let endDate: Date = new Date(today);

    switch (preset) {
      case 'thisMonth':
        startDate = new Date(today.getFullYear(), today.getMonth(), 1);
        break;
      case 'last3Months':
        startDate = new Date(today.getFullYear(), today.getMonth() - 2, 1);
        break;
      case 'last6Months':
        startDate = new Date(today.getFullYear(), today.getMonth() - 5, 1);
        break;
      case 'thisYear':
        startDate = new Date(today.getFullYear(), 0, 1);
        break;
      default:
        startDate = new Date(today.getFullYear(), today.getMonth() - 5, 1);
    }

    this.rangeChanged.emit({ startDate, endDate });
  }

  formatDate(date: Date): string {
    const locale = this.translate.currentLang || 'cs-CZ';
    return date.toLocaleDateString(locale, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  }
}
