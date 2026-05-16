import { ChangeDetectionStrategy, Component, input } from '@angular/core';
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
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaSelectComponent,
  ],
  templateUrl: './order-status.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderStatusComponent {
  formGroup = input.required<FormGroup>();
  statusOptions = input.required<ICleansiaSelectOption[]>();
  paymentStatusOptions = input.required<ICleansiaSelectOption[]>();
}
