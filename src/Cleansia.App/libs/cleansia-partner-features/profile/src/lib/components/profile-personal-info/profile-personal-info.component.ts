import { Component, Input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaCalendarComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { CalendarModule } from 'primeng/calendar';
import { ProfileFacade } from '../../profile/profile.facade';

@Component({
  selector: 'cleansia-profile-personal-info',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    CalendarModule,
    CleansiaSectionComponent,
    CleansiaTextInputComponent,
    CleansiaCalendarComponent,
    CleansiaSelectComponent,
    CleansiaTelephoneComponent,
  ],
  template: `
    <cleansia-section [title]="'pages.profile.personal_info' | translate">
      <div class="cleansia-profile__fields" [formGroup]="facade.formGroup">
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.first_name' | translate"
            [floatVariant]="'on'"
            formControlName="firstName"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.last_name' | translate"
            [floatVariant]="'on'"
            formControlName="lastName"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-calendar
            [label]="'pages.profile.date_of_birth' | translate"
            [floatVariant]="'on'"
            formControlName="dateOfBirth"
            [required]="true"
            [showIcon]="true"
            [iconDisplay]="'input'"
            [dateFormat]="'dd.mm.yy'"
          />
        </div>
        <div class="cleansia-profile__field cleansia-profile__field--span-3">
          <cleansia-text-input
            [label]="'pages.profile.street' | translate"
            [floatVariant]="'on'"
            formControlName="street"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.city' | translate"
            [floatVariant]="'on'"
            formControlName="city"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.zip_code' | translate"
            [floatVariant]="'on'"
            formControlName="zipCode"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-select
            [options]="facade.countries()"
            [label]="'pages.profile.country' | translate"
            [floatVariant]="'on'"
            [filter]="true"
            formControlName="countryId"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-telephone
            [label]="'pages.profile.phone' | translate"
            [floatVariant]="'on'"
            formControlName="phone"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.email' | translate"
            [floatVariant]="'on'"
            formControlName="email"
            dataType="email"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-select
            [options]="facade.countries()"
            [label]="'pages.profile.nationality' | translate"
            [floatVariant]="'on'"
            [filter]="true"
            formControlName="nationalityId"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.national_id' | translate"
            [floatVariant]="'on'"
            formControlName="passportId"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.tax_id' | translate"
            [floatVariant]="'on'"
            formControlName="taxId"
            dataType="text"
          />
        </div>
      </div>
    </cleansia-section>
  `,
})
export class ProfilePersonalInfoComponent {
  @Input({ required: true }) facade!: ProfileFacade;
}
