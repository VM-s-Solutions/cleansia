import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup } from '@angular/forms';
import {
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaSelectComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-payment-info',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
  ],
  templateUrl: './order-payment-info.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderPaymentInfoComponent {
  formGroup = input.required<FormGroup>();
  paymentTypeOptions = input.required<ICleansiaSelectOption[]>();
  currencyOptions = input.required<ICleansiaSelectOption[]>();
}
