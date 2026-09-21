import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { CleansiaButtonComponent, CleansiaLabelComponent } from '@cleansia/components';
import { currentLanguage, formatDate } from '@cleansia/utils';

export type DateRangePreset = 'thisMonth' | 'last3Months' | 'last6Months' | 'thisYear';

interface PresetOption {
  readonly preset: DateRangePreset;
  readonly labelKey: string;
}

const PRESETS: readonly PresetOption[] = [
  { preset: 'thisMonth', labelKey: 'pages.dashboard.date_range.this_month' },
  { preset: 'last3Months', labelKey: 'pages.dashboard.date_range.last_3_months' },
  { preset: 'last6Months', labelKey: 'pages.dashboard.date_range.last_6_months' },
  { preset: 'thisYear', labelKey: 'pages.dashboard.date_range.this_year' },
];

@Component({
  selector: 'cleansia-date-range-selector',
  standalone: true,
  imports: [TranslateModule, CleansiaButtonComponent, CleansiaLabelComponent],
  templateUrl: './cleansia-date-range-selector.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaDateRangeSelectorComponent {
  private readonly translate = inject(TranslateService);
  private readonly lang = currentLanguage(this.translate);

  startDate = input<Date>(new Date());
  endDate = input<Date>(new Date());
  rangeChanged = output<{ startDate: Date; endDate: Date }>();

  readonly presets = PRESETS;
  readonly selectedPreset = signal<DateRangePreset>('last6Months');

  readonly rangeLabel = computed(
    () => `${formatDate(this.startDate(), this.lang())} – ${formatDate(this.endDate(), this.lang())}`
  );

  selectPreset(preset: DateRangePreset): void {
    this.selectedPreset.set(preset);
    const today = new Date();
    let startDate: Date;
    const endDate = new Date(today);

    switch (preset) {
      case 'thisMonth':
        startDate = new Date(today.getFullYear(), today.getMonth(), 1);
        break;
      case 'last3Months':
        startDate = new Date(today.getFullYear(), today.getMonth() - 2, 1);
        break;
      case 'thisYear':
        startDate = new Date(today.getFullYear(), 0, 1);
        break;
      default:
        startDate = new Date(today.getFullYear(), today.getMonth() - 5, 1);
    }

    this.rangeChanged.emit({ startDate, endDate });
  }
}
