import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaFormSkeletonComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { PartnerAuthService } from '@cleansia/partner-services';
import { DialogService } from '@cleansia/services';
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
  private readonly dialogService = inject(DialogService);
  private readonly authService = inject(PartnerAuthService);

  ngOnInit(): void {
    this.facade.loadProfile();
  }

  onLogout(): void {
    this.dialogService
      .confirmTranslated(
        'global.dialog.confirm_logout',
        'global.dialog.confirm'
      )
      .subscribe((confirmed) => {
        if (confirmed) {
          // logout() returns a cold Observable — must subscribe or nothing
          // happens (no server-side refresh-token revoke, no local cookie
          // cleanup, no redirect). The pipe(tap(...)) inside the service does the work.
          this.authService.logout().subscribe();
        }
      });
  }
}
