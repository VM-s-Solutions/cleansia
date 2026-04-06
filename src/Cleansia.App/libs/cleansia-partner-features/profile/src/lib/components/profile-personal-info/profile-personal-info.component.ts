import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaCalendarComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { EmployeeEntityType } from '@cleansia/partner-services';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { CalendarModule } from 'primeng/calendar';
import { ProfileFacade } from '../../profile/profile.facade';

@Component({
  selector: 'cleansia-profile-personal-info',
  standalone: true,
  imports: [
    CommonModule,
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
          <cleansia-select
            [options]="entityTypeOptions"
            [label]="'pages.profile.entity_type' | translate"
            [floatVariant]="'on'"
            formControlName="entityType"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.registration_number' | translate"
            [floatVariant]="'on'"
            formControlName="registrationNumber"
            dataType="text"
          />
        </div>
        <div class="cleansia-profile__field">
          <cleansia-text-input
            [label]="'pages.profile.vat_number' | translate"
            [floatVariant]="'on'"
            formControlName="vatNumber"
            dataType="text"
          />
        </div>
        @if (isLegalEntity()) {
          <div class="cleansia-profile__field">
            <cleansia-text-input
              [label]="'pages.profile.legal_entity_name' | translate"
              [floatVariant]="'on'"
              formControlName="legalEntityName"
              dataType="text"
            />
          </div>
        }
      </div>
    </cleansia-section>
  `,
})
export class ProfilePersonalInfoComponent implements OnInit {
  @Input({ required: true }) facade!: ProfileFacade;

  private readonly translate = inject(TranslateService);

  readonly entityTypeOptions: ICleansiaSelectOption[] = [
    {
      value: EmployeeEntityType.NaturalPerson,
      label: this.translate.instant('pages.profile.entity_type_natural_person'),
    },
    {
      value: EmployeeEntityType.LegalEntity,
      label: this.translate.instant('pages.profile.entity_type_legal_entity'),
    },
  ];

  readonly isLegalEntity = signal(false);

  ngOnInit(): void {
    const control = this.facade.formGroup.get('entityType');
    if (!control) {
      return;
    }
    this.isLegalEntity.set(control.value === EmployeeEntityType.LegalEntity);
    control.valueChanges.subscribe((value) =>
      this.isLegalEntity.set(value === EmployeeEntityType.LegalEntity)
    );
  }
}
