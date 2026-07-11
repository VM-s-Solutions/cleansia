import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { AdminAuthService } from '@cleansia/admin-services';
import { CleansiaButtonComponent } from '@cleansia/components';
import { SnackbarService } from '@cleansia/services';
import { provideMockStore } from '@ngrx/store/testing';
import { TranslateModule } from '@ngx-translate/core';
import { AdminLoginComponent } from './admin-login.component';

describe('AdminLoginComponent', () => {
  let component: AdminLoginComponent;
  let fixture: ComponentFixture<AdminLoginComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminLoginComponent, TranslateModule.forRoot()],
      providers: [
        provideMockStore({ initialState: { loading: { loading: true } } }),
        provideRouter([]),
        {
          provide: AdminAuthService,
          useValue: { login: jest.fn(), isLoggedIn: jest.fn() },
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

    fixture = TestBed.createComponent(AdminLoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders the login button interactive even while global HTTP loading is on', () => {
    const button = fixture.debugElement.query(
      By.directive(CleansiaButtonComponent)
    ).componentInstance as CleansiaButtonComponent;

    expect(button.loading()).toBe(false);
    expect(button.disabled()).toBe(false);
  });
});
