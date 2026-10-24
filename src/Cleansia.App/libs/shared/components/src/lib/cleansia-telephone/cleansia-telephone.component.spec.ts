import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { CleansiaTelephoneComponent } from './cleansia-telephone.component';

@Component({
  standalone: true,
  imports: [CleansiaTelephoneComponent, ReactiveFormsModule],
  template: `<cleansia-telephone [formControl]="phone" [defaultCountryFlag]="flag()" />`,
})
class HostComponent {
  readonly phone = new FormControl('', { nonNullable: true });
  readonly flag = signal('cz');
}

/** A number pasted or autofilled with its own country code is read as that country, not as digits. */
describe('CleansiaTelephoneComponent input', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const field = (): HTMLInputElement =>
    fixture.nativeElement.querySelector('.cleansia-telephone__input');

  const selectedCode = (): string =>
    fixture.nativeElement.querySelector('.cleansia-telephone__code').textContent.trim();

  const selectedFlag = (): string =>
    (fixture.nativeElement.querySelector('.cleansia-telephone__trigger .cleansia-telephone__flag') as HTMLElement)
      .className;

  function enter(value: string): void {
    field().value = value;
    field().dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function render(flag = 'cz'): void {
    TestBed.configureTestingModule({ imports: [HostComponent, TranslateModule.forRoot()] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    host.flag.set(flag);
    fixture.detectChanges();
  }

  it('keeps the country code of a pasted number once, with Czechia selected', () => {
    render();

    enter('+420 777 111 222');

    expect(host.phone.value).toBe('+420 777111222');
    expect(field().value).toBe('777111222');
    expect(selectedCode()).toBe('+420');
  });

  it('switches to the country a pasted number names', () => {
    render();

    enter('+421 905 123 456');

    expect(host.phone.value).toBe('+421 905123456');
    expect(selectedCode()).toBe('+421');
    expect(selectedFlag()).toContain('fi-sk');
  });

  it('reads the country code past leading spaces and the invisible marks a contacts app adds', () => {
    render();

    enter('‪  +421 905 123 456‬');

    expect(host.phone.value).toBe('+421 905123456');
  });

  it('keeps the selected country when the pasted code is one it shares with another', () => {
    render('us');

    enter('+1 202 555 0100');

    expect(host.phone.value).toBe('+1 2025550100');
    expect(selectedFlag()).toContain('fi-us');
  });

  it('takes typed digits as the national number of the selected country, as before', () => {
    render();

    enter('777 111 222');

    expect(host.phone.value).toBe('+420 777111222');
    expect(field().value).toBe('777111222');
  });

  it('reads a plus that names no known country code as digits, as before', () => {
    render();

    enter('+999 123');

    expect(host.phone.value).toBe('+420 999123');
    expect(selectedCode()).toBe('+420');
  });
});
