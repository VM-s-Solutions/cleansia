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
 * box that has focus, beside the digit it holds or in place of it.
 */
describe('CleansiaCodeInputComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  const boxes = (): HTMLInputElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('.cleansia-code-input__digit'));

  const values = (): string[] => boxes().map((box) => box.value);

  /** The box's value after an edit, which a browser never lets past maxlength; jsdom does not apply it. */
  function enter(index: number, value: string, caret = value.length): void {
    const box = boxes()[index];
    expect(value.length).toBeLessThanOrEqual(box.maxLength);
    box.value = value;
    box.setSelectionRange(caret, caret);
    box.dispatchEvent(new Event('input'));
    jest.runOnlyPendingTimers();
    fixture.detectChanges();
  }

  /** Text inserted at the caret without a paste event, cut as a browser cuts it to the room maxlength leaves. */
  function insert(index: number, text: string, caret: 'before' | 'after' = 'after'): void {
    const held = boxes()[index].value;
    const fits = text.slice(0, boxes()[index].maxLength - held.length);
    if (caret === 'before') {
      enter(index, fits + held, fits.length);
    } else {
      enter(index, held + fits);
    }
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

  it('lets a whole code into any one box beside the digit it already holds, spaced or inserted twice', () => {
    expect(boxes().map((box) => box.maxLength)).toEqual([13, 13, 13, 13, 13, 13]);
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

  it('keeps the first six digits of a code inserted twice, as far as the box lets it in, and drops anything that is not a digit', () => {
    insert(0, '123 456123456');

    expect(host.code.value).toBe('123456');
    expect(host.completed).toEqual(['123456']);
  });

  /** A wrong code typed by hand leaves the caret after the last box's digit, and the screen keeps a refused code. */
  it('takes a whole code inserted after the digit a box holds as that code, and submits it once', () => {
    ['9', '8', '7', '6', '5', '6'].forEach((digit, index) => enter(index, digit));

    enter(5, '6123456');

    expect(values()).toEqual(['1', '2', '3', '4', '5', '6']);
    expect(host.code.value).toBe('123456');
    expect(host.completed).toEqual(['987656', '123456']);
  });

  it('takes a spaced code inserted after the digit a box holds as that code, and submits it once', () => {
    ['9', '8', '7', '6', '5', '6'].forEach((digit, index) => enter(index, digit));

    insert(5, '123 456');

    expect(values()).toEqual(['1', '2', '3', '4', '5', '6']);
    expect(host.code.value).toBe('123456');
    expect(host.completed).toEqual(['987656', '123456']);
  });

  it('takes a whole code inserted before the digit a box holds as that code', () => {
    enter(5, '6');

    enter(5, '1234566', 6);

    expect(values()).toEqual(['1', '2', '3', '4', '5', '6']);
    expect(host.completed).toEqual(['123456']);
  });

  it('tells by the caret which side of the digit a code went in, when the code starts with that digit', () => {
    enter(5, '6');

    enter(5, '6123456', 6);

    expect(host.code.value).toBe('612345');
    expect(host.completed).toEqual(['612345']);
  });

  it('takes a whole code that replaced the digit a box held as that code, even when it starts with that digit', () => {
    enter(5, '6');

    enter(5, '612345');

    expect(host.code.value).toBe('612345');
    expect(host.completed).toEqual(['612345']);
  });

  it.each(['after', 'before'] as const)(
    'takes a code inserted twice %s the digit a box holds as that code, once',
    (caret) => {
      enter(5, '9');

      insert(5, '123456123456', caret);

      expect(values()).toEqual(['1', '2', '3', '4', '5', '6']);
      expect(host.completed).toEqual(['123456']);
    },
  );

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

  it('writes a short paste from the box it lands in, keeping the boxes before it', () => {
    enter(0, '1');
    enter(1, '2');

    paste(2, '34');

    expect(values()).toEqual(['1', '2', '3', '4', '', '']);
    expect(host.code.value).toBe('1234');
    expect(host.completed).toEqual([]);
    expect(document.activeElement).toBe(boxes()[3]);
  });

  it('writes a fragment the keyboard inserts into a box from that box onward', () => {
    enter(0, '1');
    enter(1, '2');

    insert(2, '34');

    expect(values()).toEqual(['1', '2', '3', '4', '', '']);
    expect(host.code.value).toBe('1234');
    expect(host.completed).toEqual([]);
  });

  it('cuts a short paste at the last box, and completes the code it fills', () => {
    ['1', '2', '3', '4'].forEach((digit, index) => enter(index, digit));

    paste(4, '567');

    expect(values()).toEqual(['1', '2', '3', '4', '5', '6']);
    expect(host.completed).toEqual(['123456']);
  });

  it('leaves the boxes after a short paste as they were', () => {
    ['1', '2', '3', '4', '5'].forEach((digit, index) => enter(index, digit));

    paste(1, '9');

    expect(values()).toEqual(['1', '9', '3', '4', '5', '']);
    expect(host.code.value).toBe('19345');
  });
});
