import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { CleansiaBankAccountComponent } from './cleansia-bank-account.component';

@Component({
  standalone: true,
  imports: [CleansiaBankAccountComponent],
  template: `<cleansia-bank-account [prefix]="prefix" [number]="number" [bankCode]="bankCode" />`,
})
class HostComponent {
  readonly prefix = new FormControl('', { nonNullable: true });
  readonly number = new FormControl('', { nonNullable: true });
  readonly bankCode = new FormControl('', { nonNullable: true });
}

type Segment = 'prefix' | 'number' | 'bank-code';

/**
 * Pasting into one box, by the rule the three twins share (→ /partner-app/onboarding). The run is the
 * pasted text, whitespace dropped and an en or em dash read as a hyphen, and a run that is a written-out
 * account or an IBAN fills the three boxes, however short it is. Otherwise the box's new text is what
 * the browser would make of the paste at the selection, and text that fits the box is kept as pasted.
 * Text that overflows it: one character is typing, and the box keeps the digits of its new text that fit;
 * digits that fit the box replace what it held, and longer digits up to ten are the account number.
 * Anything else leaves the box as it was.
 */
describe('CleansiaBankAccountComponent paste', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const input = (segment: Segment): HTMLInputElement =>
    fixture.nativeElement.querySelector(`.cleansia-bank-account__segment--${segment}`);

  function paste(segment: Segment, text: string, caret?: number, selectionEnd = caret): Event {
    if (caret !== undefined && selectionEnd !== undefined) input(segment).setSelectionRange(caret, selectionEnd);
    const event = new Event('paste', { cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { getData: () => text } });
    input(segment).dispatchEvent(event);
    fixture.detectChanges();
    return event;
  }

  function boxes(): [string, string, string] {
    return [input('prefix').value, input('number').value, input('bank-code').value];
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  it.each([
    [1, 'bank-code', '', '0800', 'at the end', ['', '', '0800']],
    [2, 'bank-code', '0800', '5500', 'after', ['', '', '5500']],
    [3, 'bank-code', '0800', '5500', 'before', ['', '', '5500']],
    [4, 'bank-code', '0800', '55', 'over the selection 08', ['', '', '5500']],
    [5, 'bank-code', '08', '00', 'after', ['', '', '0800']],
    [6, 'bank-code', '08', '0800', 'after', ['', '', '0800']],
    [7, 'bank-code', '0800', '2000145399', 'after', ['', '2000145399', '0800']],
    [8, 'bank-code', '', '2000145399', 'into', ['', '2000145399', '']],
    [9, 'prefix', '12345', '67', 'after', ['67', '', '']],
    [10, 'prefix', '', '19', 'into', ['19', '', '']],
    [11, 'number', '', '19-2000145399/0800', 'into', ['19', '2000145399', '0800']],
    [12, 'number', '', 'CZ65 0800 0000 1920 0014 5399', 'into', ['19', '2000145399', '0800']],
    [13, 'bank-code', '0800', '5', 'after', ['', '', '0800']],
    [14, 'number', '2000145399', '1234567890', 'after', ['', '1234567890', '']],
    [15, 'number', '', '12345/0800', 'into', ['', '12345', '0800']],
    [16, 'number', '', '19-1234567', 'into', ['19', '1234567', '']],
    [17, 'bank-code', '0800', '5', 'before', ['', '', '5080']],
    [18, 'bank-code', '0800', '19-2000145399/0800', 'over all of', ['19', '2000145399', '0800']],
    [19, 'number', '1234567890', '19-2000145399/0801', 'over all of', ['19', '2000145399', '0801']],
    [20, 'bank-code', '0800', '19-2000145399/0800', 'after', ['19', '2000145399', '0800']],
    [21, 'number', '2000145399', '2000145399/0800', 'over all of', ['', '2000145399', '0800']],
    [22, 'number', '19', '19-2000145399/0800', 'before', ['19', '2000145399', '0800']],
  ] as [number, Segment, string, string, string, [string, string, string]][])(
    'case %s: the %s segment holding "%s", "%s" pasted %s it',
    (_, segment, held, text, where, expected) => {
      const box = { prefix: host.prefix, number: host.number, 'bank-code': host.bankCode }[segment];
      box.setValue(held);
      const selections: Record<string, [number, number]> = {
        before: [0, 0],
        'over the selection 08': [0, 2],
        'over all of': [0, held.length],
      };
      const [caret, selectionEnd] = selections[where] ?? [held.length, held.length];

      const event = paste(segment, text, caret, selectionEnd);

      expect(event.defaultPrevented).toBe(true);
      expect(parts()).toEqual(expected);
      expect(boxes()).toEqual(expected);
    },
  );

  it('puts a paste the segment keeps in place of the selection and leaves the caret after it, as the browser would', () => {
    host.prefix.setValue('1234');

    paste('prefix', '9', 1, 3);

    expect(parts()).toEqual(['194', '', '']);
    expect(input('prefix').selectionStart).toBe(2);
    expect(input('prefix').selectionEnd).toBe(2);
  });

  it('keeps a pasted line as a single-line box takes it, its trailing line break dropped', () => {
    paste('prefix', '19\r\n');

    expect(parts()).toEqual(['19', '', '']);
  });

  it('splits a written-out account even where it fits the segment, and keeps other text that fits as pasted, as typing it would', () => {
    paste('number', '12345/0800');
    paste('prefix', '19/');

    expect(parts()).toEqual(['19/', '12345', '0800']);
    expect(boxes()).toEqual(['19/', '12345', '0800']);
  });

  it('takes one character pasted into a full segment as typing there, keeping the digits that fit', () => {
    host.bankCode.setValue('0800');

    paste('bank-code', '5', 2);
    expect(parts()).toEqual(['', '', '0850']);

    paste('bank-code', '-', 0);
    expect(parts()).toEqual(['', '', '0850']);
    expect(boxes()).toEqual(['', '', '0850']);
  });

  it('spreads a written-out account over the three segments', () => {
    const event = paste('prefix', '19-2000145399/0800');

    expect(event.defaultPrevented).toBe(true);
    expect(host.prefix.value).toBe('19');
    expect(host.number.value).toBe('2000145399');
    expect(host.bankCode.value).toBe('0800');
  });

  it('clears an old prefix when the written-out account has none', () => {
    host.prefix.setValue('35');

    paste('number', '2000145399/0800');

    expect(host.prefix.value).toBe('');
    expect(host.number.value).toBe('2000145399');
    expect(host.bankCode.value).toBe('0800');
  });

  it('keeps the bank code when the written-out account has none', () => {
    host.bankCode.setValue('0100');

    paste('number', '19-2000145399');

    expect(host.prefix.value).toBe('19');
    expect(host.bankCode.value).toBe('0100');
  });

  it.each(['prefix', 'number', 'bank-code'] as const)(
    'takes a bare number pasted into the %s segment as the number and leaves the prefix and bank code',
    (segment) => {
      host.prefix.setValue('19');
      host.bankCode.setValue('0800');

      const event = paste(segment, '2000145399');

      expect(event.defaultPrevented).toBe(true);
      expect(host.prefix.value).toBe('19');
      expect(host.number.value).toBe('2000145399');
      expect(host.bankCode.value).toBe('0800');
    },
  );

  it('keeps a bank code pasted into its own segment there, and the number already entered', () => {
    host.number.setValue('2000145399');

    const event = paste('bank-code', '0800');

    expect(event.defaultPrevented).toBe(true);
    expect(parts()).toEqual(['', '2000145399', '0800']);
  });

  it('keeps a prefix pasted into its own segment there, and the number already entered', () => {
    host.number.setValue('2000145399');

    paste('prefix', '19');

    expect(parts()).toEqual(['19', '2000145399', '']);
  });

  it('puts a pasted bank code or prefix too long to join the one in the segment in its place, spaces dropped', () => {
    host.prefix.setValue('1234');
    host.bankCode.setValue('0100');

    paste('bank-code', ' 08 00 ');
    paste('prefix', ' 19\u00A0');

    expect(parts()).toEqual(['19', '', '0800']);
  });

  it.each([
    ['5500', 'after', '0800', 'bank-code', ['', '2000145399', '5500']],
    ['5500', 'before', '0800', 'bank-code', ['', '2000145399', '5500']],
    ['67', 'after', '12345', 'prefix', ['67', '2000145399', '']],
  ] as const)(
    'keeps %s pasted %s the %s of the %s segment there, and the number already entered',
    (text, side, held, segment, expected) => {
      host.number.setValue('2000145399');
      (segment === 'prefix' ? host.prefix : host.bankCode).setValue(held);

      const event = paste(segment, text, side === 'before' ? 0 : held.length);

      expect(event.defaultPrevented).toBe(true);
      expect(parts()).toEqual(expected);
    },
  );

  it('marks only the segment a fitting bank code lands in as edited', () => {
    paste('bank-code', '0800');

    expect(host.bankCode.dirty).toBe(true);
    expect(host.number.dirty).toBe(false);
    expect(host.prefix.dirty).toBe(false);
  });

  it.each([
    ['prefix', '123456', ['123456', '', '']],
    ['prefix', '1234567', ['', '1234567', '']],
    ['bank-code', '1234', ['', '', '1234']],
    ['bank-code', '12345', ['', '12345', '']],
  ] as const)('reads %s paste %s as the segment it fits, or else the number', (segment, text, expected) => {
    paste(segment, text);

    expect(parts()).toEqual(expected);
  });

  it('keeps the segment as it was when the paste is no account and too long for it', () => {
    host.number.setValue('123');

    const event = paste('number', 'not an account');

    expect(event.defaultPrevented).toBe(true);
    expect(parts()).toEqual(['', '123', '']);
    expect(boxes()).toEqual(['', '123', '']);
  });

  function parts(): [string, string, string] {
    return [host.prefix.value, host.number.value, host.bankCode.value];
  }

  /** Banking apps space the parts, and some use a no-break space or a narrow one. */
  it('ignores whitespace of every kind, inside the digits too', () => {
    paste('number', ' 19 - 2000145399 / 0800 ');
    expect(parts()).toEqual(['19', '2000145399', '0800']);

    paste('number', '19\u00A0-\u00A02000\u202F145\u00A0399\u00A0/\u00A00800');
    expect(parts()).toEqual(['19', '2000145399', '0800']);

    paste('prefix', '12321414 /\n3545');
    expect(parts()).toEqual(['', '12321414', '3545']);

    host.bankCode.setValue('0100');
    paste('prefix', '2000 1453 99');
    expect(parts()).toEqual(['', '2000145399', '0100']);
  });

  it('reads an en or em dash as a hyphen', () => {
    paste('number', '19\u20132000145399/0800');
    expect(parts()).toEqual(['19', '2000145399', '0800']);

    paste('number', '35\u20142000145399/0800');
    expect(parts()).toEqual(['35', '2000145399', '0800']);
  });

  it('breaks a Czech IBAN into its domestic parts', () => {
    const event = paste('prefix', 'CZ65 0800 0000 1920 0014 5399');

    expect(event.defaultPrevented).toBe(true);
    expect(parts()).toEqual(['19', '2000145399', '0800']);

    paste('bank-code', 'cz6508000000192000145399');
    expect(parts()).toEqual(['19', '2000145399', '0800']);
  });

  it('breaks a Slovak IBAN into its domestic parts', () => {
    paste('number', 'SK31 1200 0000 1987 4263 7541');

    expect(parts()).toEqual(['19', '8742637541', '1200']);
  });

  /** Leading zeros are padding in the BBAN, not part of the written account; an all-zero prefix is none. */
  it('drops the IBAN padding and clears an old prefix when the IBAN has none', () => {
    host.prefix.setValue('35');

    paste('number', 'CZ55 0800 0000 0000 0012 3457');

    expect(parts()).toEqual(['', '123457', '0800']);
  });

  it.each([
    'DE89 3704 0044 0532 0130 00',
    'CZ65 0800 0000 1920 0014 539',
    'CZ65 0800 0000 1920 0014 53990',
    'CZ6X 0800 0000 1920 0014 5399',
    'CZ00 0800 0000 0000 0000 0000',
  ])('keeps the segment as it was for %s, which is no domestic account', (text) => {
    const event = paste('number', text);

    expect(event.defaultPrevented).toBe(true);
    expect(parts()).toEqual(['', '', '']);
  });

  it.each([
    '1234567-1/0800',
    '12345678901/0800',
    '2000145399/08000',
    '20001/45/399',
    '19-20-2000145399/0800',
    '-2000145399/0800',
    '19-/0800',
    '2000145399/',
    '/0800',
    '12a4/0800',
    '١٢٣/0800',
  ])('keeps the segment as it was for %s, which is no account', (text) => {
    const event = paste('bank-code', text);

    expect(event.defaultPrevented).toBe(true);
    expect(parts()).toEqual(['', '', '']);
  });
});
