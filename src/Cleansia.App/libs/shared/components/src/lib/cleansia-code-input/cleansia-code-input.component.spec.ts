import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CleansiaCodeInputComponent } from './cleansia-code-input.component';

@Component({
  standalone: true,
  imports: [CleansiaCodeInputComponent, ReactiveFormsModule],
  template: `<cleansia-code-input [formControl]="code" (codeComplete)="completed.push($event)" />`,
})
class HostComponent {
  readonly code = new FormControl('', { nonNullable: true });
  readonly completed: string[] = [];
}

/**
 * A code reaches the boxes three ways: typed one digit at a time, pasted, or inserted whole without a
 * paste event — Safari's one-time-code AutoFill and the keyboard's suggestion strip both write into the
 * box that has focus.
 */
describe('CleansiaCodeInputComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const boxes = (): HTMLInputElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('.cleansia-code-input__digit'));

  const values = (): string[] => boxes().map((box) => box.value);

  function enter(index: number, value: string, caret = value.length): void {
    const box = boxes()[index];
    box.value = value;
    box.setSelectionRange(caret, caret);
    box.dispatchEvent(new Event('input'));
    jest.runOnlyPendingTimers();
    fixture.detectChanges();
  }

  function paste(index: number, text: string): Event {
    const event = new Event('paste', { cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { getData: () => text } });
    boxes()[index].dispatchEvent(event);
    jest.runOnlyPendingTimers();
    fixture.detectChanges();
    return event;
  }

  beforeEach(() => {
    jest.useFakeTimers();
    TestBed.configureTestingModule({ imports: [HostComponent] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => jest.useRealTimers());

  it('lets a whole code into any one box', () => {
    expect(boxes().map((box) => box.maxLength)).toEqual([6, 6, 6, 6, 6, 6]);
  });

  it('spreads a whole code inserted into the first box over all six and completes it', () => {
    enter(0, '123456');

    expect(values()).toEqual(['1', '2', '3', '4', '5', '6']);
    expect(host.code.value).toBe('123456');
    expect(host.completed).toEqual(['123456']);
    expect(document.activeElement).toBe(boxes()[5]);
  });

  it('spreads a whole code from the first box whichever box it was inserted into', () => {
    enter(3, '987654');

    expect(values()).toEqual(['9', '8', '7', '6', '5', '4']);
    expect(host.completed).toEqual(['987654']);
  });

  it('keeps the first six digits of a code inserted twice, and drops anything that is not a digit', () => {
    enter(0, '123 456123456');

    expect(host.code.value).toBe('123456');
    expect(host.completed).toEqual(['123456']);
  });

  it('takes a typed digit and moves on to the next box', () => {
    enter(0, '1');

    expect(values()).toEqual(['1', '', '', '', '', '']);
    expect(host.code.value).toBe('1');
    expect(host.completed).toEqual([]);
    expect(document.activeElement).toBe(boxes()[1]);
  });

  it('takes the digit just typed into a box that already holds one, wherever the caret was', () => {
    enter(2, '3');

    enter(2, '35');
    expect(values()[2]).toBe('5');

    enter(2, '75', 1);
    expect(values()[2]).toBe('7');
    expect(host.code.value).toBe('7');
  });

  it('keeps the digit in a box when what was typed next to it is not a digit', () => {
    enter(0, '3');

    enter(0, '3a');

    expect(values()[0]).toBe('3');
    expect(host.code.value).toBe('3');
  });

  it('empties a box whose digit was replaced by something that is not a digit', () => {
    enter(0, '3');

    enter(0, 'a');

    expect(values()[0]).toBe('');
    expect(host.code.value).toBe('');
  });

  it('completes the code once the sixth digit is typed', () => {
    ['1', '2', '3', '4', '5', '6'].forEach((digit, index) => enter(index, digit));

    expect(host.code.value).toBe('123456');
    expect(host.completed).toEqual(['123456']);
  });

  it('spreads a pasted code over the boxes as before, separators dropped', () => {
    const event = paste(2, '12 34 56');

    expect(event.defaultPrevented).toBe(true);
    expect(values()).toEqual(['1', '2', '3', '4', '5', '6']);
    expect(host.completed).toEqual(['123456']);
  });

  it('fills only as many boxes as a short paste has digits', () => {
    paste(0, '12');

    expect(values()).toEqual(['1', '2', '', '', '', '']);
    expect(host.code.value).toBe('12');
    expect(document.activeElement).toBe(boxes()[1]);
  });
});
