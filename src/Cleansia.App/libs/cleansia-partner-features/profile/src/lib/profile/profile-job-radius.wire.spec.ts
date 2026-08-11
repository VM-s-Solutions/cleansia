import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { APIBASEURL, EmployeeItem } from '@cleansia/partner-services';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { ProfileJobRadiusFacade } from './profile-job-radius.facade';

/**
 * A mocked client cannot tell `null` from `0` from an omitted member, and `0` is the one value
 * `JobProximity` refuses — so the clear is pinned over the bytes the generated client actually
 * puts on the socket, driven through the facade's real call path.
 */
describe('job radius wire shape', () => {
  let facade: ProfileJobRadiusFacade;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        // Relative, as dev and the deployed apps are — one origin for the
        // HttpOnly SameSite=Strict auth cookie.
        { provide: APIBASEURL, useValue: '' },
        ProfileJobRadiusFacade,
        {
          provide: SnackbarService,
          useValue: { showSuccess: jest.fn(), showError: jest.fn() },
        },
        {
          provide: TranslateService,
          useValue: { instant: (key: string) => key, currentLang: 'en' },
        },
      ],
    });

    facade = TestBed.inject(ProfileJobRadiusFacade);
    httpMock = TestBed.inject(HttpTestingController);
    facade.seed(EmployeeItem.fromJS({ id: 'emp-1', jobRadiusKm: 120 }));
  });

  afterEach(() => httpMock.verify());

  const submit = (value: {
    limitEnabled: boolean;
    radiusKm: string;
  }): TestRequest => {
    facade.formGroup.setValue(value);
    facade.onSubmit();

    return httpMock.expectOne('/api/Employee/UpdateJobRadius');
  };

  const settle = (request: TestRequest): void =>
    request.flush(new Blob(['{}'], { type: 'application/json' }));

  it('puts the change on a PUT to UpdateJobRadius', () => {
    const request = submit({ limitEnabled: true, radiusKm: '42' });

    expect(request.request.method).toBe('PUT');
    settle(request);
  });

  it('sends the chosen distance as a number, beside the caller id the command still carries', () => {
    const request = submit({ limitEnabled: true, radiusKm: '42' });

    expect(JSON.parse(request.request.body)).toEqual({
      employeeId: 'emp-1',
      radiusKm: 42,
    });
    settle(request);
  });

  it('clears the radius as an absent member — never a zero', () => {
    const request = submit({ limitEnabled: false, radiusKm: '120' });
    const body: string = request.request.body;

    expect(JSON.parse(body)).toEqual({ employeeId: 'emp-1' });
    expect(Object.keys(JSON.parse(body))).not.toContain('radiusKm');
    expect(body).not.toContain('"radiusKm":0');
    expect(body).not.toContain('"radiusKm": 0');
    settle(request);
  });
});
