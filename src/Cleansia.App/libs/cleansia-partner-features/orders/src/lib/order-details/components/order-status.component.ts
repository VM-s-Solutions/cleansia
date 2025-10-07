import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup } from '@angular/forms';
import {
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
  CleansiaSelectComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ICleansiaSelectOption } from '@cleansia/components';

@Component({
  selector: 'order-status',
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
    <cleansia-section [title]="'pages.order_details.order_status' | translate">
      <div class="cleansia-order-details__grid" [formGroup]="formGroup()">
        <div class="cleansia-order-details__field">
          <cleansia-select
            [label]="'pages.order_details.order_status' | translate"
            [floatVariant]="'on'"
            formControlName="orderStatus"
            [options]="statusOptions()"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-select
            [label]="'pages.order_details.payment_status' | translate"
            [floatVariant]="'on'"
            formControlName="paymentStatus"
            [options]="paymentStatusOptions()"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.confirmation_code' | translate"
            [floatVariant]="'on'"
            formControlName="confirmationCode"
            dataType="text"
            [disabled]="true"
          />
        </div>
      </div>
    </cleansia-section>
  `,
})
export class OrderStatusComponent {
  formGroup = input.required<FormGroup>();
  statusOptions = input.required<ICleansiaSelectOption[]>();
  paymentStatusOptions = input.required<ICleansiaSelectOption[]>();
}
