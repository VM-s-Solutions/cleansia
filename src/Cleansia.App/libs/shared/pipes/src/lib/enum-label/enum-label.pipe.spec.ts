import { TestBed } from '@angular/core/testing';
import { TranslateService } from '@ngx-translate/core';
import { EnumLabelPipe, enumLabelKey } from './enum-label.pipe';

describe('EnumLabelPipe', () => {
  const dict: Record<string, string> = {
    'enums.payment_type.cash': 'Hotovost',
    'enums.payment_status.partially_refunded': 'Částečně vráceno',
  };
  let pipe: EnumLabelPipe;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        EnumLabelPipe,
        { provide: TranslateService, useValue: { instant: (k: string) => dict[k] ?? k } },
      ],
    });
    pipe = TestBed.inject(EnumLabelPipe);
  });

  it('translates a payment type and a multi-word payment status', () => {
    expect(pipe.transform({ name: 'Cash' }, 'payment_type')).toBe('Hotovost');
    expect(pipe.transform({ name: 'PartiallyRefunded' }, 'payment_status')).toBe('Částečně vráceno');
  });

  it('keeps the raw name when no key exists and is empty without a code', () => {
    expect(pipe.transform({ name: 'Card' }, 'payment_type')).toBe('Card');
    expect(pipe.transform(undefined, 'payment_type')).toBe('');
    expect(enumLabelKey('payment_type', undefined)).toBeUndefined();
  });
});
