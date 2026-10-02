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
});
