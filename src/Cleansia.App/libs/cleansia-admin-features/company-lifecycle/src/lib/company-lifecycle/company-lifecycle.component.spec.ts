import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AdminClient, CompanyLifecycleDto, CompanyLifecycleState } from '@cleansia/admin-services';
import { DialogService, PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of, Subject, throwError } from 'rxjs';
import { CompanyLifecycleComponent } from './company-lifecycle.component';

describe('CompanyLifecycleComponent', () => {
  let fixture: ComponentFixture<CompanyLifecycleComponent>;
  let getMock: jest.Mock;
  let hasPolicy: jest.Mock;

  const settled = {
    openOrders: 0,
    openOrdersOnOrAfterWindDownFrom: 0,
    activeTemplates: 0,
    activeMemberships: 0,
    creditBalances: 0,
    pendingRefunds: 0,
    ordersAwaitingPay: 0,
    ordersAwaitingReceipt: 0,
    receiptsAwaitingFiscalRegistration: 0,
    openPayPeriods: 0,
    unpaidInvoices: 0,
    uninvoicedPayRows: 0,
    openDisputes: 0,
  };

  const deactivated = CompanyLifecycleDto.fromJS({
    name: 'Cleansia CZ',
    state: CompanyLifecycleState.Deactivated,
    operatesDefaultMarket: false,
    deactivatedOn: new Date('2026-10-01T06:00:00Z'),
    deactivatedByEmail: 'ops@cleansia.cz',
    windDownFrom: new Date('2026-10-01T00:00:00Z'),
    windDownRequestedOn: new Date('2026-09-01T08:00:00Z'),
    windDownRequestedByEmail: 'ops@cleansia.cz',
    ...settled,
    openOrders: 3,
  });

  const archived = CompanyLifecycleDto.fromJS({
    name: 'Cleansia CZ',
    state: CompanyLifecycleState.Archived,
    operatesDefaultMarket: false,
    archiveRequestedOn: new Date('2027-04-01T09:00:00Z'),
    archivedOn: new Date('2027-04-01T09:05:00Z'),
    archiveManifestSha256: 'f'.repeat(64),
    ...settled,
  });

  const element = () => fixture.nativeElement as HTMLElement;
  const text = () => element().textContent ?? '';
  const actButtons = () => Array.from(element().querySelectorAll('.cleansia-company-lifecycle__act cleansia-button button')) as HTMLButtonElement[];

  async function render(): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [CompanyLifecycleComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        { provide: AdminClient, useValue: { adminCompanyLifecycleClient: { get: getMock } } },
        { provide: SnackbarService, useValue: { showSuccessTranslated: jest.fn() } },
        { provide: DialogService, useValue: { confirmTranslated: jest.fn() } },
        { provide: PermissionService, useValue: { hasPolicy } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CompanyLifecycleComponent);
    fixture.detectChanges();
  }

  beforeEach(() => {
    hasPolicy = jest.fn().mockReturnValue(true);
  });

  it('shows the loader until the lifecycle arrives', async () => {
    getMock = jest.fn().mockReturnValue(new Subject<CompanyLifecycleDto>());

    await render();

    expect(element().querySelector('cleansia-loader')).not.toBeNull();
    expect(element().querySelector('cleansia-table')).toBeNull();
  });

  it('renders the error state with a retry that re-reads', async () => {
    getMock = jest.fn().mockReturnValueOnce(throwError(() => new Error('boom'))).mockReturnValueOnce(of(deactivated));

    await render();

    expect(text()).toContain('pages.company_lifecycle.load_error');
    expect(element().querySelector('cleansia-table')).toBeNull();

    (element().querySelector('cleansia-button button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(getMock).toHaveBeenCalledTimes(2);
    expect(element().querySelector('cleansia-table')).not.toBeNull();
  });

  it('renders the banner with the state and stamps, the four acts with their reasons, and one fact row per definition', async () => {
    getMock = jest.fn().mockReturnValue(of(deactivated));

    await render();

    expect(text()).toContain('Cleansia CZ');
    expect(text()).toContain('enums.company_lifecycle_state.deactivated');
    expect(text()).toContain('pages.company_lifecycle.stamps.wind_down');
    expect(text()).toContain('pages.company_lifecycle.stamps.deactivated');
    expect(actButtons()).toHaveLength(4);
    expect(actButtons().map((b) => b.disabled)).toEqual([true, false, false, true]);
    expect(text()).toContain('api.company.already_deactivated');
    expect(text()).toContain('pages.company_lifecycle.reasons.unsettled_facts');
    expect(element().querySelectorAll('tbody tr.table__row')).toHaveLength(16);
    expect(element().querySelector('tbody a[href="/order-management"]')).not.toBeNull();
    expect(element().querySelector('tbody a[href="/company-settings"]')).not.toBeNull();
  });

  it('shows the manifest hash once archived and no enabled act', async () => {
    getMock = jest.fn().mockReturnValue(of(archived));

    await render();

    expect(text()).toContain('f'.repeat(64));
    expect(text()).toContain('enums.company_lifecycle_state.archived');
    expect(actButtons().every((b) => b.disabled)).toBe(true);
  });

  it('hides an act the administrator has no policy for', async () => {
    hasPolicy = jest.fn().mockImplementation((policy: string) => policy !== 'CanArchiveCompany');
    getMock = jest.fn().mockReturnValue(of(deactivated));

    await render();

    expect(actButtons()).toHaveLength(3);
    expect(text()).not.toContain('pages.company_lifecycle.acts.archive');
  });
});
