import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaFormSkeletonComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ToastModule } from 'primeng/toast';
import {
  ProfilePersonalInfoComponent,
  ProfileBankDetailsComponent,
  ProfileEmergencyContactComponent,
  ProfileAvailabilityComponent,
  ProfileDocumentsComponent,
} from '../components';
import { ProfileFacade } from './profile.facade';
import { ProfileDocumentsFacade } from './profile-documents.facade';

@Component({
  selector: 'cleansia-partner-profile',
  standalone: true,
  imports: [
    ToastModule,
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaFormSkeletonComponent,
    CleansiaCheckboxComponent,
    ProfilePersonalInfoComponent,
    ProfileBankDetailsComponent,
    ProfileEmergencyContactComponent,
    ProfileAvailabilityComponent,
    ProfileDocumentsComponent,
  ],
  templateUrl: './profile.component.html',
  providers: [ProfileDocumentsFacade, ProfileFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent implements OnInit {
  protected readonly facade = inject(ProfileFacade);

  ngOnInit(): void {
    this.facade.loadProfile();
  }
}
