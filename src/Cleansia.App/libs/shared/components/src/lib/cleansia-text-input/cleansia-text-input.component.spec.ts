import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CleansiaTextInputComponent } from './cleansia-text-input.component';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, CleansiaTextInputComponent],
  template: `<cleansia-text-input [formControl]="control" dataType="text" />`,
})
class NormalizingHostComponent {
  readonly control = new FormControl('', { nonNullable: true });

  constructor() {
    this.control.valueChanges.subscribe((value) => {
      const digits = value.replace(/\D/g, '');
      if (digits !== value) {
        this.control.setValue(digits, { emitEvent: false });
      }
    });
  }
}

describe('CleansiaTextInputComponent', () => {
  let fixture: ComponentFixture<NormalizingHostComponent>;
  let input: HTMLInputElement;

  const type = (text: string): void => {
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [NormalizingHostComponent] });
    fixture = TestBed.createComponent(NormalizingHostComponent);
    fixture.detectChanges();
    input = fixture.nativeElement.querySelector('input');
  });

  it('renders the model value', () => {
    fixture.componentInstance.control.setValue('5885638003');
    fixture.detectChanges();

    expect(input.value).toBe('5885638003');
  });

  // A `[value]` binding only writes the DOM when the expression differs from what
  // Angular last wrote, so a normalizer that strips the character just typed
  // leaves it visible while the model no longer holds it — the user then sees an
  // account number we are not going to send.
  it('re-renders when the model rejects the character just typed', () => {
    type('19');
    expect(input.value).toBe('19');

    type('19-');

    expect(fixture.componentInstance.control.value).toBe('19');
    expect(input.value).toBe('19');
  });

  it('keeps accepting input after a rejected character', () => {
    type('19');
    type('19-');
    type('192');

    expect(fixture.componentInstance.control.value).toBe('192');
    expect(input.value).toBe('192');
  });
});
