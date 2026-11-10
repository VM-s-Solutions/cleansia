import { TranslateService } from '@ngx-translate/core';
import { monthName, orderStatusLegendLabel } from './dashboard-labels';

describe('dashboard labels', () => {
  const translate = {
    instant: (k: string) => ({ 'enums.order_status.in_progress': 'Probíhá' })[k] ?? k,
  } as unknown as TranslateService;

  it('words an order status key and keeps an unknown one as sent', () => {
    expect(orderStatusLegendLabel(translate, 'InProgress')).toBe('Probíhá');
    expect(orderStatusLegendLabel(translate, 'Mystery')).toBe('Mystery');
  });

  it('writes a month in the UI language from year and month', () => {
    expect(monthName(2026, 11, 'en')).toBe('November');
    expect(monthName(2026, 11, 'cs')).toBe('listopad');
  });
});
