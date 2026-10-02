import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { Router } from '@angular/router';
import {
  RegistrationCompletionResult,
  RegistrationCompletionService,
} from '@cleansia/partner-services';
import { Store } from '@ngrx/store';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { CleansiaRegistrationLockComponent } from './registration-lock.component';

const baseResult: RegistrationCompletionResult = {
  isComplete: false,
  hasUploadedDocuments: true,
  hasCompletedProfile: true,
  missingRequirements: [],
  contractStatus: null,
  awaitingApproval: false,
  isRejected: false,
  rejectionReason: null,
};

/**
 * The rejected approval row. A rejected cleaner can fix nothing on this screen, so beside the reason
 * it offers a mail to support, as both partner apps' rejected row does (owner decision D13).
 */
describe('CleansiaRegistrationLockComponent', () => {
  function render(result: Partial<RegistrationCompletionResult>): HTMLElement {
    TestBed.configureTestingModule({
      imports: [CleansiaRegistrationLockComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        { provide: Store, useValue: { dispatch: jest.fn(), select: () => of(null) } },
        { provide: Router, useValue: { navigate: jest.fn() } },
        {
          provide: RegistrationCompletionService,
          useValue: { checkRegistrationCompletion: () => ({ ...baseResult, ...result }) },
        },
      ],
    });
    const fixture = TestBed.createComponent(CleansiaRegistrationLockComponent);
    fixture.detectChanges();
    return fixture.nativeElement;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('offers a mail to support beside the reason on a rejected application', () => {
    const el = render({ isRejected: true, rejectionReason: 'The ID photo is unreadable.' });

    const link = el.querySelector<HTMLAnchorElement>('.cleansia-registration-lock__category-action a');
    expect(el.textContent).toContain('The ID photo is unreadable.');
    expect(link?.textContent).toContain('registration_lock.contact_support');
    // No dictionary is loaded, so the subject is its key — the point is the address and that the
    // subject is carried, encoded.
    expect(link?.getAttribute('href')).toBe(
      `mailto:support@cleansia.cz?subject=${encodeURIComponent('registration_lock.support_subject')}`,
    );
  });

  it.each([
    ['awaiting review', { awaitingApproval: true }],
    ['approved', { isComplete: true }],
    ['not yet submitted', { hasUploadedDocuments: false }],
  ])('offers no support mail while the application is %s', (_state, result) => {
    const el = render(result);

    expect(el.querySelector('.cleansia-registration-lock__category-action')).toBeNull();
  });
});
