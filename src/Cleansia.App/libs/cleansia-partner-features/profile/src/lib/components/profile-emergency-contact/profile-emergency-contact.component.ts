import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
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
  templateUrl: './profile-emergency-contact.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileEmergencyContactComponent {
  @Input({ required: true }) facade!: ProfileFacade;
}
