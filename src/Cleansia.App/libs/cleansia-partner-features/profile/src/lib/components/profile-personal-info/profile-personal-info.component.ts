import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input, OnInit, inject, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaAddressAutocompleteComponent,
  CleansiaCalendarComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { EmployeeEntityType } from '@cleansia/partner-services';
import type { MapboxAddressSuggestion } from '@cleansia/services';
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
    CleansiaAddressAutocompleteComponent,
  ],
  templateUrl: './profile-personal-info.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
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

  // Mapbox pick patches the three text fields; country stays user-chosen since
  // Mapbox doesn't return our internal Country.Id.
  onAddressPicked(suggestion: MapboxAddressSuggestion): void {
    const form = this.facade.formGroup;
    form.patchValue({
      street: suggestion.street || form.get('street')?.value || '',
      city: suggestion.city || form.get('city')?.value || '',
      zipCode: suggestion.zipCode || form.get('zipCode')?.value || '',
    });
  }
}
