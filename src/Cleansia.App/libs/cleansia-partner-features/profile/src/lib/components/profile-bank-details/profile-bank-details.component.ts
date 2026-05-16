import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
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
  templateUrl: './profile-bank-details.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileBankDetailsComponent {
  @Input({ required: true }) facade!: ProfileFacade;
}
