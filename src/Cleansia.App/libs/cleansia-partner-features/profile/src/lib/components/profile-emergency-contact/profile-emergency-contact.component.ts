import { Component, Input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaSectionComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ProfileFacade } from '../../profile/profile.facade';

@Component({
  selector: 'cleansia-profile-emergency-contact',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaTelephoneComponent,
  ],
  template: `
    <cleansia-section [title]="'pages.profile.emergency_contact' | translate">
      <div class="cleansia-profile__fields" [formGroup]="facade.formGroup">
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.emergency_name' | translate"
            [floatVariant]="'on'"
            formControlName="emergencyName"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-telephone
            [label]="'pages.profile.emergency_phone' | translate"
            [floatVariant]="'on'"
            formControlName="emergencyPhone"
          />
        </div>
      </div>
    </cleansia-section>
  `,
})
export class ProfileEmergencyContactComponent {
  @Input({ required: true }) facade!: ProfileFacade;
}
