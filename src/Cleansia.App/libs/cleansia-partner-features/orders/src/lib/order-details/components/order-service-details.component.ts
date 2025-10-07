import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormGroup } from '@angular/forms';
import {
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'order-service-details',
  styleUrls: ['../order-details.component.scss'],
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
  ],
  template: `
    <cleansia-section [title]="'pages.order_details.service_details' | translate">
      <div class="cleansia-order-details__grid" [formGroup]="formGroup()">
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.cleaning_date' | translate"
            [floatVariant]="'on'"
            formControlName="cleaningDateTime"
            dataType="text"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.rooms' | translate"
            [floatVariant]="'on'"
            formControlName="rooms"
            dataType="text"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.bathrooms' | translate"
            [floatVariant]="'on'"
            formControlName="bathrooms"
            dataType="text"
            [disabled]="true"
          />
        </div>
        <div class="cleansia-order-details__field">
          <cleansia-text-input
            [label]="'pages.order_details.estimated_time' | translate"
            [floatVariant]="'on'"
            formControlName="estimatedTime"
            dataType="text"
            [disabled]="true"
          />
        </div>
      </div>
    </cleansia-section>
  `,
})
export class OrderServiceDetailsComponent {
  formGroup = input.required<FormGroup>();
}
