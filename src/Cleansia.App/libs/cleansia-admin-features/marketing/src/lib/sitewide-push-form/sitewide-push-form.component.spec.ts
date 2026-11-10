import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AdminClient } from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { NEVER } from 'rxjs';
import { SitewidePushFormComponent } from './sitewide-push-form.component';

describe('SitewidePushFormComponent', () => {
  let fixture: ComponentFixture<SitewidePushFormComponent>;
  let confirmMock: jest.Mock;
  let snackbar: {
    showSuccessTranslated: jest.Mock;
    showErrorTranslated: jest.Mock;
  };

  beforeEach(async () => {
    confirmMock = jest.fn().mockReturnValue(NEVER);
    snackbar = {
      showSuccessTranslated: jest.fn(),
      showErrorTranslated: jest.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [SitewidePushFormComponent, TranslateModule.forRoot()],
      providers: [
        {
          provide: AdminClient,
          useValue: { adminMarketingClient: { sendSitewidePromo: jest.fn() } },
        },
        { provide: SnackbarService, useValue: snackbar },
        {
          provide: DialogService,
          useValue: { confirmTranslated: confirmMock },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SitewidePushFormComponent);
    fixture.detectChanges();
  });

  it('says above the first section that e-mail campaigns are not available yet', () => {
    const banner: HTMLElement = fixture.nativeElement.querySelector('.cleansia-info-banner');
    const firstSection: HTMLElement = fixture.nativeElement.querySelector('cleansia-section');

    expect(banner.getAttribute('role')).toBe('note');
    expect(banner.querySelector('i.pi-info-circle')).not.toBeNull();
    expect(banner.textContent?.trim()).toBe('pages.sitewide_push.email_not_available');
    expect(banner.compareDocumentPosition(firstSection) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('asks to confirm when the filled form is sent with the rendered button', () => {
    fixture.componentInstance.form.setValue({
      titleEn: 'Hi',
      titleCs: 'Ahoj',
      titleSk: 'Ahoj',
      titleUk: 'Pryvit',
      titleRu: 'Privet',
      bodyEn: 'Body',
      bodyCs: 'Telo',
      bodySk: 'Telo',
      bodyUk: 'Tilo',
      bodyRu: 'Telo',
    });
    fixture.detectChanges();

    const button: HTMLButtonElement = fixture.nativeElement.querySelector(
      '.form-actions button'
    );
    button.click();

    expect(confirmMock).toHaveBeenCalledWith(
      'pages.sitewide_push.confirm_send',
      'pages.sitewide_push.confirm_title'
    );
  });
});
