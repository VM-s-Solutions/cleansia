import { provideHttpClient } from '@angular/common/http';
import { Component, PLATFORM_ID, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { CleansiaSelectComponent } from '@cleansia/components';
import { LoyaltyTier, MyProfileDto, SavedAddressDto } from '@cleansia/customer-services';
import { TranslateModule } from '@ngx-translate/core';
import { Subject } from 'rxjs';
import { NotificationPreferencesComponent } from '../notification-preferences/notification-preferences.component';
import { ProfileComponent } from './profile.component';
import { ProfileFacade } from './profile.facade';

@Component({ selector: 'cleansia-customer-notification-preferences', standalone: true, template: '' })
class NotificationPreferencesStub {}

/** The signals and methods the template and the address dialog read; nothing else is spun up. */
class FakeProfileFacade {
  // The real facade's destroy hook runs against the double on teardown.
  destroyed$ = new Subject<void>();
  user = signal<MyProfileDto | null>(null);
  loading = signal(true);
  saving = signal(false);
  addresses = signal<SavedAddressDto[]>([]);
  addressesLoading = signal(false);
  avatarUrl = signal<string | null>(null);
  avatarSaving = signal(false);
  loyaltyTier = signal<LoyaltyTier | null>(null);
  credit = signal(null);
  countryOptions = signal<{ label: string; value: string }[]>([]);
  defaultCountryId = signal('');
  loadProfile = jest.fn();
  refreshSavedAddresses = jest.fn();
}

describe('ProfileComponent — the saved-address country picker', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let facade: FakeProfileFacade;

  beforeEach(async () => {
    facade = new FakeProfileFacade();
    facade.countryOptions.set([
      { label: 'Czechia', value: 'cze-id' },
      { label: 'Slovakia', value: 'svk-id' },
    ]);
    facade.defaultCountryId.set('svk-id');

    await TestBed.configureTestingModule({
      imports: [ProfileComponent, TranslateModule.forRoot()],
      providers: [
        provideHttpClient(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: PLATFORM_ID, useValue: 'server' },
      ],
    })
      .overrideComponent(ProfileComponent, {
        remove: { imports: [NotificationPreferencesComponent] },
        add: { imports: [NotificationPreferencesStub], providers: [{ provide: ProfileFacade, useValue: facade }] },
      })
      .compileComponents();

    fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
  });

  it('starts a new address on the country the facade resolved, by id', () => {
    fixture.componentInstance.openAddAddress();

    expect(fixture.componentInstance.addressForm.controls.country.value).toBe('svk-id');
  });

  it('keeps the saved country when editing an existing address', () => {
    fixture.componentInstance.openEditAddress(
      SavedAddressDto.fromJS({ id: 'addr-1', label: 'Home', countryId: 'cze-id', isDefault: true }),
    );

    expect(fixture.componentInstance.addressForm.controls.country.value).toBe('cze-id');
  });

  it('renders the country select in the dialog, fed by the market directory', async () => {
    fixture.componentInstance.openAddAddress();
    // The value reaches the PrimeNG control through an asynchronous ngModel write.
    for (let round = 0; round < 2; round++) {
      fixture.detectChanges();
      await fixture.whenStable();
    }

    const select = fixture.debugElement
      .queryAll(By.directive(CleansiaSelectComponent))
      .find((debugElement) => (debugElement.componentInstance as CleansiaSelectComponent).label() === 'pages.profile.address_country');
    expect(select).toBeTruthy();
    expect((select?.componentInstance as CleansiaSelectComponent).options()).toEqual([
      { label: 'Czechia', value: 'cze-id' },
      { label: 'Slovakia', value: 'svk-id' },
    ]);
    expect((select?.nativeElement as HTMLElement).querySelector('.p-select-label')?.textContent?.trim()).toBe('Slovakia');
  });
});
