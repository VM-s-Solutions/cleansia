import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslateModule } from '@ngx-translate/core';
import { CleansiaCalendarComponent } from './cleansia-calendar.component';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, CleansiaCalendarComponent],
  template: `
    <form [formGroup]="form">
      <cleansia-calendar data-spec="starts" label="Starts" formControlName="startsOn" />
      <cleansia-calendar data-spec="ends" label="Ends" formControlName="endsOn" [floatVariant]="null" />
    </form>
    <cleansia-calendar data-spec="paid" label="Paid" [disabled]="locked()" />
  `,
})
class CalendarHostComponent {
  readonly locked = signal(false);
  readonly form = new FormGroup({
    startsOn: new FormControl<Date | null>(null),
    endsOn: new FormControl<Date | null>({ value: null, disabled: true }),
  });
}

describe('CleansiaCalendarComponent — a disabled control', () => {
  let fixture: ComponentFixture<CalendarHostComponent>;
  let host: CalendarHostComponent;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [CalendarHostComponent, TranslateModule.forRoot()],
      providers: [provideNoopAnimations()],
    });
    fixture = TestBed.createComponent(CalendarHostComponent);
    host = fixture.componentInstance;
    await settle();
  });

  async function settle(): Promise<void> {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function field(spec: string): HTMLInputElement {
    return (fixture.nativeElement as HTMLElement).querySelector(
      `[data-spec="${spec}"] input`
    ) as HTMLInputElement;
  }

  it('draws the picker disabled once the control is disabled in code, and editable once enabled again', async () => {
    expect(field('starts').disabled).toBe(false);

    host.form.controls.startsOn.disable();
    await settle();
    expect(field('starts').disabled).toBe(true);

    host.form.controls.startsOn.enable();
    await settle();
    expect(field('starts').disabled).toBe(false);
  });

  it('draws a control created disabled as disabled from the first render, with the label stacked', () => {
    expect(field('ends').disabled).toBe(true);
  });

  it('disables the picker through the disabled input', async () => {
    host.locked.set(true);
    await settle();
    expect(field('paid').disabled).toBe(true);

    host.locked.set(false);
    await settle();
    expect(field('paid').disabled).toBe(false);
  });
});
