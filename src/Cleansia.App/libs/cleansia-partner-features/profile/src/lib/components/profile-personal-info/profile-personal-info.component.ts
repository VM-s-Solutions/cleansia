import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import {
  CleansiaAddressAutocompleteComponent,
  CleansiaCalendarComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import type { MapboxAddressSuggestion } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { ProfileFacade } from '../../profile/profile.facade';

@Component({
  selector: 'cleansia-partner-profile-personal-info',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    TranslatePipe,
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
export class ProfilePersonalInfoComponent {
  @Input({ required: true }) facade!: ProfileFacade;

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
