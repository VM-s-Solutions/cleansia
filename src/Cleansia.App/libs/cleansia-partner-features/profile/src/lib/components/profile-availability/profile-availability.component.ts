import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
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
  templateUrl: './profile-availability.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileAvailabilityComponent {
  @Input({ required: true }) facade!: ProfileFacade;
}
