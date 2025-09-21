import { Component, inject, OnInit } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaCalendarComponent,
  CleansiaCheckboxComponent,
  CleansiaFileComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { CalendarModule } from 'primeng/calendar';
import { ToastModule } from 'primeng/toast';
import { ProfileFacade } from './profile.facade';

@Component({
  selector: 'cleansia-partner-profile',
  standalone: true,
  imports: [
    ToastModule,
    TranslatePipe,
    CalendarModule,
    ReactiveFormsModule,
    CleansiaFileComponent,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaCalendarComponent,
    CleansiaCheckboxComponent,
    CleansiaSelectComponent,
    CleansiaTelephoneComponent,
    CleansiaTextInputComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss'],
  providers: [ProfileFacade],
})
export class ProfileComponent implements OnInit {
  protected readonly facade = inject(ProfileFacade);

  ngOnInit(): void {
    this.facade.loadProfile();
  }
}
