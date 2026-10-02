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

/**
 * Pasting a whole account into the one control. The apps' `splitPastedAccount` (owner decision D14)
 * is the rule: a written-out account fills the segments it names, and a bare number is the account
 * number and nothing else.
 */
describe('CleansiaBankAccountComponent paste', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  function paste(segment: 'prefix' | 'number' | 'bank-code', text: string): Event {
    const input: HTMLInputElement = fixture.nativeElement.querySelector(
      `.cleansia-bank-account__segment--${segment}`,
    );
    const event = new Event('paste', { cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { getData: () => text } });
    input.dispatchEvent(event);
    fixture.detectChanges();
    return event;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
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

  it('leaves text that is not an account to the segment it was pasted into', () => {
    const event = paste('number', 'not an account');

    expect(event.defaultPrevented).toBe(false);
    expect(host.number.value).toBe('');
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
  ])('leaves %s, which is no domestic account, to the segment', (text) => {
    const event = paste('number', text);

    expect(event.defaultPrevented).toBe(false);
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
  ])('leaves %s, which is no account, to the segment', (text) => {
    const event = paste('number', text);

    expect(event.defaultPrevented).toBe(false);
    expect(parts()).toEqual(['', '', '']);
  });
});
