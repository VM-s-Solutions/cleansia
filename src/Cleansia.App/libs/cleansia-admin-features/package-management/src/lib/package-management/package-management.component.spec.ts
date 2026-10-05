import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AdminClient, PackageListItem, PagedDataOfPackageListItem } from '@cleansia/admin-services';
import { DialogService, PermissionService, SnackbarService } from '@cleansia/services';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { PackageManagementComponent } from './package-management.component';

/**
 * A deactivated service stays inside every package that includes it, and the list says so on the
 * package's row. The facade spec pins which packages hold one; this pins that the name column
 * actually renders the marker, and only on those rows. → /product/business-rules#deactivated-catalogue
 */
describe('PackageManagementComponent', () => {
  let fixture: ComponentFixture<PackageManagementComponent>;

  const plain = PackageListItem.fromJS({
    id: 'pkg-1',
    name: 'Basic',
    includedServices: [{ serviceId: 'svc-a', name: 'Windows' }],
  });
  const holdsRetired = PackageListItem.fromJS({
    id: 'pkg-2',
    name: 'Move-out',
    includedServices: [
      { serviceId: 'svc-a', name: 'Windows' },
      { serviceId: 'svc-r', name: 'Oven' },
    ],
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [PackageManagementComponent, TranslateModule.forRoot()],
      providers: [
        provideNoopAnimations(),
        provideRouter([]),
        {
          provide: AdminClient,
          useValue: {
            adminPackageClient: {
              getPaged: jest.fn().mockReturnValue(
                of(
                  PagedDataOfPackageListItem.fromJS({
                    data: [plain.toJSON(), holdsRetired.toJSON()],
                    total: 2,
                  })
                )
              ),
            },
            adminCurrencyClient: {
              getOverview: jest.fn().mockReturnValue(of([{ id: 'cur-czk', code: 'CZK', isDefault: true }])),
            },
            adminServiceClient: {
              getPaged: jest.fn().mockReturnValue(of({ data: [{ id: 'svc-r', name: 'Oven' }], total: 1 })),
            },
          },
        },
        { provide: PermissionService, useValue: { hasPolicy: () => true } },
        { provide: DialogService, useValue: { confirmTranslated: jest.fn() } },
        {
          provide: SnackbarService,
          useValue: { showSuccessTranslated: jest.fn(), showErrorTranslated: jest.fn() },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(PackageManagementComponent);
    fixture.detectChanges();
  });

  const nameCells = (): HTMLElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('tbody .table__row')).map(
      (row) => (row as HTMLElement).querySelector('td') as HTMLElement
    );

  it('marks the package that includes a retired service, beside its name', () => {
    const [, retiredRow] = nameCells();

    expect(retiredRow.textContent).toContain('Move-out');
    const marker = retiredRow.querySelector('.status-badge--warning');
    expect(marker?.textContent?.trim()).toBe('pages.package_management.includes_inactive_service');
  });

  it('leaves a package without one unmarked', () => {
    const [plainRow] = nameCells();

    expect(plainRow.textContent).toContain('Basic');
    expect(plainRow.querySelector('.status-badge--warning')).toBeNull();
  });
});
