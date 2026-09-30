import { TestBed } from '@angular/core/testing';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { readFileSync } from 'fs';
import { join } from 'path';
import { CompleteOrderDialogComponent } from './complete-order-dialog.component';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const I18N_DIR = join(__dirname, '../../../../../../../apps/cleansia-partner.app/src/assets/i18n');

/**
 * Pay has no time term and the server takes the notes as optional, so the dialog neither grades the
 * clean against its estimate nor tells the cleaner the notes are required or bear on their pay.
 */
const HINT_CLAIMS: Record<(typeof LOCALES)[number], { required: RegExp; pay: RegExp }> = {
  en: { required: /\brequired\b/i, pay: /\bpay/i },
  cs: { required: /(?:^|\s)povinn/iu, pay: /plat/iu },
  sk: { required: /(?:^|\s)povinn/iu, pay: /plat/iu },
  uk: { required: /(?:^|\s)обов/iu, pay: /оплат/iu },
  ru: { required: /(?:^|\s)обязательн/iu, pay: /оплат/iu },
};

function completeOrderCopy(locale: string): Record<string, string> {
  const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as {
    pages: { orders: { complete_order: Record<string, string> } };
  };
  return bundle.pages.orders.complete_order;
}

describe('CompleteOrderDialogComponent', () => {
  let close: jest.Mock;

  function render() {
    const fixture = TestBed.createComponent(CompleteOrderDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(async () => {
    close = jest.fn();
    await TestBed.configureTestingModule({
      imports: [CompleteOrderDialogComponent, TranslateModule.forRoot()],
      providers: [
        { provide: DynamicDialogRef, useValue: { close } },
        {
          provide: DynamicDialogConfig,
          useValue: { data: { orderId: 'order-1', orderNumber: 'ORD-1', estimatedTime: 120 } },
        },
      ],
    }).compileComponents();
  });

  it('shows no delay or on-time verdict, however long the clean took', () => {
    const fixture = render();
    fixture.componentInstance.form.controls.actualCompletionTimeMinutes.setValue('180');
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('complete_order.delay');
    expect(text).not.toContain('complete_order.on_time');
    expect(text).not.toContain('%');
  });

  it('completes without notes', () => {
    const fixture = render();

    expect(fixture.componentInstance.form.valid).toBe(true);
    fixture.componentInstance.onComplete();

    expect(close).toHaveBeenCalledWith({ actualCompletionTimeMinutes: 120, completionNotes: '' });
  });

  it.each(LOCALES)('keeps the retired verdict keys out of the %s bundle', (locale) => {
    const copy = completeOrderCopy(locale);

    for (const retired of ['delay', 'on_time', 'status']) {
      expect(copy).not.toHaveProperty(retired);
    }
  });

  it.each(LOCALES)('does not call the notes required or tie them to pay, in %s', (locale) => {
    const hint = completeOrderCopy(locale)['notes_hint'];
    const { required, pay } = HINT_CLAIMS[locale];

    expect(hint.trim().length).toBeGreaterThan(0);
    expect({ required: required.test(hint), pay: pay.test(hint) }).toEqual({
      required: false,
      pay: false,
    });
  });
});
