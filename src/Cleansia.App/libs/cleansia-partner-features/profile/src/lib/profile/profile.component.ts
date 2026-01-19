import { Component, inject, OnInit } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
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

@Component({
  selector: 'cleansia-partner-profile',
  standalone: true,
  imports: [
    ToastModule,
    TranslatePipe,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaCheckboxComponent,
    CleansiaLanguageSwitcherComponent,
    ProfilePersonalInfoComponent,
    ProfileBankDetailsComponent,
    ProfileEmergencyContactComponent,
    ProfileAvailabilityComponent,
    ProfileDocumentsComponent,
  ],
  templateUrl: './profile.component.html',
  providers: [ProfileFacade],
})
export class ProfileComponent implements OnInit {
  protected readonly facade = inject(ProfileFacade);

  ngOnInit(): void {
    this.facade.loadProfile();
  }
}
