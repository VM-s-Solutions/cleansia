import { Component, input } from '@angular/core';
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
  styleUrls: ['../order-details.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
  ],
  template: `
    <cleansia-section [title]="'pages.order_details.payment_information' | translate">
      <div class="cleansia-order-details__grid" [formGroup]="formGroup()">
        <div class="cleansia-order-details__field">
          <cleansia-select
            [options]="paymentTypeOptions()"
            [label]="'pages.order_details.payment_type' | translate"
            [floatVariant]="'on'"
            formControlName="paymentType"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.total_price' | translate"
            [floatVariant]="'on'"
            formControlName="totalPrice"
            dataType="text"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-select
            [options]="currencyOptions()"
            [label]="'pages.order_details.currency' | translate"
            [floatVariant]="'on'"
            formControlName="currency"
            [disabled]="true"
          />
        </div>
      </div>
    </cleansia-section>
  `,
})
export class OrderPaymentInfoComponent {
  formGroup = input.required<FormGroup>();
  paymentTypeOptions = input.required<ICleansiaSelectOption[]>();
  currencyOptions = input.required<ICleansiaSelectOption[]>();
}
