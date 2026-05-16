import { ChangeDetectionStrategy, Component, input } from '@angular/core';
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
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaTelephoneComponent,
  ],
  templateUrl: './order-customer-info.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OrderCustomerInfoComponent {
  formGroup = input.required<FormGroup>();
}
