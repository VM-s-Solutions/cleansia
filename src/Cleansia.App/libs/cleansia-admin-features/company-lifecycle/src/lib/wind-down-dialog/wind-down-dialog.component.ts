import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaCalendarComponent } from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogModule } from 'primeng/dialog';

export const WIND_DOWN_CONSEQUENCE_KEYS: readonly string[] = [
  'pages.company_lifecycle.wind_down_dialog.consequences.notices',
  'pages.company_lifecycle.wind_down_dialog.consequences.orders',
  'pages.company_lifecycle.wind_down_dialog.consequences.templates',
  'pages.company_lifecycle.wind_down_dialog.consequences.memberships',
  'pages.company_lifecycle.wind_down_dialog.consequences.credit',
  'pages.company_lifecycle.wind_down_dialog.consequences.pay_period',
];

@Component({
  selector: 'cleansia-admin-wind-down-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe, DialogModule, CleansiaButtonComponent, CleansiaCalendarComponent],
  templateUrl: './wind-down-dialog.component.html',
})
export class WindDownDialogComponent {
  private readonly fb = inject(FormBuilder);

  readonly visible = input<boolean>(false);
  readonly visibleChange = output<boolean>();
  readonly submitting = input<boolean>(false);
  readonly companyName = input<string>('');
  readonly minDate = input<Date | null>(null);

  readonly submitForm = output<Date>();

  readonly consequenceKeys = WIND_DOWN_CONSEQUENCE_KEYS;

  readonly form = this.fb.group({
    fromDate: this.fb.control<Date | null>(null, { validators: [Validators.required] }),
  });

  onVisibilityChanged(value: boolean): void {
    if (!value) {
      this.form.reset({ fromDate: null });
    }
    this.visibleChange.emit(value);
  }

  cancel(): void {
    this.onVisibilityChanged(false);
  }

  submit(): void {
    const fromDate = this.form.controls.fromDate.value;
    if (this.form.invalid || !fromDate) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitForm.emit(fromDate);
  }
}
