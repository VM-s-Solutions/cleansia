import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminClient } from '@cleansia/admin-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { LanguageManagementComponent } from './language-management.component';

describe('LanguageManagementComponent', () => {
  let component: LanguageManagementComponent;
  let fixture: ComponentFixture<LanguageManagementComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LanguageManagementComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        {
          provide: AdminClient,
          useValue: {
            adminLanguageClient: {
              getOverview: jest.fn().mockReturnValue(of([])),
            },
          },
        },
        {
          provide: SnackbarService,
          useValue: {
            showSuccess: jest.fn(),
            showError: jest.fn(),
            showApiError: jest.fn(),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LanguageManagementComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
