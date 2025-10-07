import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup } from '@angular/forms';
import {
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaTelephoneComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-customer-info',
  styleUrls: ['../order-details.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaTelephoneComponent,
  ],
  template: `
    <cleansia-section [title]="'pages.order_details.customer_information' | translate">
      <div class="cleansia-order-details__grid" [formGroup]="formGroup()">
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.customer_name' | translate"
            [floatVariant]="'on'"
            formControlName="customerName"
            dataType="text"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.customer_email' | translate"
            [floatVariant]="'on'"
            formControlName="customerEmail"
            dataType="email"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-telephone
            [label]="'pages.order_details.customer_phone' | translate"
            [floatVariant]="'on'"
            formControlName="customerPhone"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field cleansia-order-details__field--full">
          <cleansia-text-input
            [label]="'pages.order_details.address' | translate"
            [floatVariant]="'on'"
            formControlName="address"
            dataType="text"
            [disabled]="true"
          />
        </div>
      </div>
    </cleansia-section>
  `,
})
export class OrderCustomerInfoComponent {
  formGroup = input.required<FormGroup>();
}
