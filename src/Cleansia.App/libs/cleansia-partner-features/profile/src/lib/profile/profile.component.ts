import { Component, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaSectionComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  FileComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { MessageService } from 'primeng/api';
import { CalendarModule } from 'primeng/calendar';
import { ToastModule } from 'primeng/toast';
import { ProfileFacade } from './profile.facade';

@Component({
  selector: 'cleansia-partner-profile',
  standalone: true,
  imports: [
    ToastModule,
    FileComponent,
    TranslatePipe,
    CalendarModule,
    ReactiveFormsModule,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaSectionComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaTelephoneComponent,
    CleansiaTextInputComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './profile.component.html',
  providers: [ProfileFacade, MessageService],
})
export class ProfileComponent {
  protected readonly facade = inject(ProfileFacade);
}
