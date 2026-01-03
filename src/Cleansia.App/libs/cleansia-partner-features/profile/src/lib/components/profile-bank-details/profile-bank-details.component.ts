import { Component, Input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ProfileFacade } from '../../profile/profile.facade';

@Component({
  selector: 'cleansia-profile-bank-details',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
  ],
  template: `
    <cleansia-section [title]="'pages.profile.bank_details' | translate">
      <div class="cleansia-profile__field cleansia-profile__field--full" [formGroup]="facade.formGroup">
        <cleansia-text-input
          [label]="'pages.profile.iban' | translate"
          [floatVariant]="'on'"
          formControlName="iban"
          dataType="text"
        />
      </div>
    </cleansia-section>
  `,
})
export class ProfileBankDetailsComponent {
  @Input({ required: true }) facade!: ProfileFacade;
}
