import { Component, Input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaAvailabilityComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ProfileFacade } from '../../profile/profile.facade';

@Component({
  selector: 'cleansia-profile-availability',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaAvailabilityComponent,
  ],
  template: `
    <cleansia-section [title]="'pages.profile.availability' | translate">
      <p class="cleansia-profile__description">
        {{ 'pages.profile.availability_description' | translate }}
      </p>
      <div [formGroup]="facade.formGroup">
        <cleansia-availability formControlName="availability" />
      </div>
    </cleansia-section>
  `,
})
export class ProfileAvailabilityComponent {
  @Input({ required: true }) facade!: ProfileFacade;
}
