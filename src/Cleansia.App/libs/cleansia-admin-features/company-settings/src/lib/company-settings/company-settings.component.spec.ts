import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import {
  AdminClient,
  GetTenantSettingsResponse,
  TenantSettingDto,
  TenantSettingValueType,
} from '@cleansia/admin-services';
import { DialogService, PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { CompanySettingsComponent } from './company-settings.component';

describe('CompanySettingsComponent', () => {
  let fixture: ComponentFixture<CompanySettingsComponent>;
  let getAllMock: jest.Mock;

  const staleDevices = TenantSettingDto.fromJS({
    key: 'retention.stale_devices.days',
    category: 'retention',
    valueType: TenantSettingValueType.Int,
    min: 1,
    max: 36500,
    defaultValue: '90',
    effectiveValue: '120',
    isOverridden: true,
  });

  const expiredCodes = TenantSettingDto.fromJS({
    key: 'retention.expired_codes.enabled',
    category: 'retention',
    valueType: TenantSettingValueType.Bool,
    defaultValue: 'true',
    effectiveValue: 'true',
    isOverridden: false,
  });

  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CompanySettingsComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: AdminClient, useValue: { adminTenantSettingsClient: { getAll: getAllMock } } },
        { provide: SnackbarService, useValue: { showSuccessTranslated: jest.fn() } },
        { provide: DialogService, useValue: { confirmTranslated: jest.fn() } },
        { provide: PermissionService, useValue: { hasPolicy: () => true } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CompanySettingsComponent);
    fixture.detectChanges();
  }

  it('shows the loader until the catalogue arrives', async () => {
    getAllMock = jest.fn().mockReturnValue(new Subject<GetTenantSettingsResponse>());

    await render();

    expect(fixture.nativeElement.querySelector('cleansia-loader')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('cleansia-table')).toBeNull();
  });

  it('renders one row per catalogue key with the raw key and the formatted values', async () => {
    getAllMock = jest.fn().mockReturnValue(
      of(GetTenantSettingsResponse.fromJS({ settings: [staleDevices, expiredCodes] }))
    );

    await render();

    expect(fixture.nativeElement.querySelectorAll('tbody tr.table__row')).toHaveLength(2);
    expect(text()).toContain('retention.stale_devices.days');
    expect(text()).toContain('retention.expired_codes.enabled');
    expect(text()).toContain('120');
    expect(text()).toContain('pages.company_settings.source.overridden');
    expect(text()).toContain('global.yes');
  });

  it('renders the empty state when the catalogue has no keys', async () => {
    getAllMock = jest.fn().mockReturnValue(of(GetTenantSettingsResponse.fromJS({ settings: [] })));

    await render();

    expect(text()).toContain('pages.company_settings.no_settings');
  });

  it('renders the error state with a retry that re-reads', async () => {
    getAllMock = jest
      .fn()
      .mockReturnValueOnce(throwError(() => new Error('boom')))
      .mockReturnValueOnce(of(GetTenantSettingsResponse.fromJS({ settings: [staleDevices] })));

    await render();

    expect(text()).toContain('pages.company_settings.load_error');
    expect(fixture.nativeElement.querySelector('cleansia-table')).toBeNull();

    (fixture.nativeElement.querySelector('cleansia-button button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(getAllMock).toHaveBeenCalledTimes(2);
    expect(fixture.nativeElement.querySelectorAll('tbody tr.table__row')).toHaveLength(1);
  });

  it('swaps the value cell for the typed input of the row being edited', async () => {
    getAllMock = jest.fn().mockReturnValue(
      of(GetTenantSettingsResponse.fromJS({ settings: [staleDevices, expiredCodes] }))
    );
    await render();

    fixture.componentInstance['facade'].beginEdit(staleDevices);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('cleansia-text-input input[type="number"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('cleansia-checkbox')).toBeNull();

    fixture.componentInstance['facade'].beginEdit(expiredCodes);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('cleansia-text-input')).toBeNull();
    expect(fixture.nativeElement.querySelector('cleansia-checkbox')).not.toBeNull();
  });
});
