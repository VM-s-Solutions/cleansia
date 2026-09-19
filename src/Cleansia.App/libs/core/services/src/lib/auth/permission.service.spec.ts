import { TestBed } from '@angular/core/testing';
import { AdminRoleName } from '../enums/admin-role.enum';
import { Role } from '../enums/role.enum';
import { AUTH_COOKIE_KEYS } from './auth-cookie-keys';
import { PermissionService } from './permission.service';
import { PhysicalPolicy } from './physical-policy';
import { Policy } from './policy';

const KEYS = {
  token: 't',
  refreshToken: 'rt',
  refreshTokenExp: 'exp',
  role: 'role',
  csrfToken: 'csrf',
  adminRole: 'admin_role',
};

const SETS = [
  PhysicalPolicy.AdministratorOnly,
  PhysicalPolicy.ManagerOrAbove,
  PhysicalPolicy.SupportOrAbove,
  PhysicalPolicy.AccountantOrAbove,
] as const;

/**
 * The lattice the backend registers, answered from the stored hint: Administrator ⊇ Manager ⊇
 * Support ∪ Accountant. `AdminOnly` stays "any administrator" and asks for no role, which is what
 * keeps a session minted before the roles shipped usable on those routes until its refresh.
 */
describe('PermissionService administrator sets', () => {
  let service: PermissionService;

  function session(role: Role | null, adminRole: AdminRoleName | null): void {
    localStorage.clear();
    if (role) localStorage.setItem(KEYS.role, role);
    if (adminRole) localStorage.setItem(KEYS.adminRole, adminRole);
  }

  function answers(): Record<string, boolean> {
    return Object.fromEntries(
      [...SETS, PhysicalPolicy.AdminOnly].map((p) => [p, service.satisfies(p)])
    );
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PermissionService, { provide: AUTH_COOKIE_KEYS, useValue: KEYS }],
    });
    service = TestBed.inject(PermissionService);
  });

  afterEach(() => localStorage.clear());

  it('admits an Administrator to every set', () => {
    session(Role.ADMINISTRATOR, AdminRoleName.ADMINISTRATOR);

    expect(answers()).toEqual({
      AdministratorOnly: true,
      ManagerOrAbove: true,
      SupportOrAbove: true,
      AccountantOrAbove: true,
      AdminOnly: true,
    });
  });

  it('admits a Manager to everything but the Administrator set', () => {
    session(Role.ADMINISTRATOR, AdminRoleName.MANAGER);

    expect(answers()).toEqual({
      AdministratorOnly: false,
      ManagerOrAbove: true,
      SupportOrAbove: true,
      AccountantOrAbove: true,
      AdminOnly: true,
    });
  });

  it('admits a Support to its branch only', () => {
    session(Role.ADMINISTRATOR, AdminRoleName.SUPPORT);

    expect(answers()).toEqual({
      AdministratorOnly: false,
      ManagerOrAbove: false,
      SupportOrAbove: true,
      AccountantOrAbove: false,
      AdminOnly: true,
    });
  });

  it('admits an Accountant to its branch only', () => {
    session(Role.ADMINISTRATOR, AdminRoleName.ACCOUNTANT);

    expect(answers()).toEqual({
      AdministratorOnly: false,
      ManagerOrAbove: false,
      SupportOrAbove: false,
      AccountantOrAbove: true,
      AdminOnly: true,
    });
  });

  it('refuses every set to an administrator session that carries no role, and keeps AdminOnly', () => {
    session(Role.ADMINISTRATOR, null);

    expect(answers()).toEqual({
      AdministratorOnly: false,
      ManagerOrAbove: false,
      SupportOrAbove: false,
      AccountantOrAbove: false,
      AdminOnly: true,
    });
  });

  it('refuses every set to a non-administrator profile whatever the stored role says', () => {
    session(Role.EMPLOYEE, AdminRoleName.ADMINISTRATOR);

    expect(answers()).toEqual({
      AdministratorOnly: false,
      ManagerOrAbove: false,
      SupportOrAbove: false,
      AccountantOrAbove: false,
      AdminOnly: false,
    });
  });

  it('refuses every set to a role name the lattice does not know', () => {
    session(Role.ADMINISTRATOR, 'Owner' as AdminRoleName);

    expect(SETS.map((p) => service.satisfies(p))).toEqual([false, false, false, false]);
  });

  it('resolves a policy through the mirror map to its set', () => {
    session(Role.ADMINISTRATOR, AdminRoleName.ACCOUNTANT);

    expect(service.hasPolicy(Policy.CanViewRevenueReport)).toBe(true);
    expect(service.hasPolicy(Policy.CanViewOrderDetailAdmin)).toBe(false);
    expect(service.hasPolicy(Policy.CanSetAdminRole)).toBe(false);
    expect(service.hasPolicy(Policy.CanViewAdminNotifications)).toBe(true);
  });

  it('reads no administrator role for an app whose keys carry none', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        PermissionService,
        { provide: AUTH_COOKIE_KEYS, useValue: { ...KEYS, adminRole: undefined } },
      ],
    });
    service = TestBed.inject(PermissionService);
    session(Role.ADMINISTRATOR, AdminRoleName.ADMINISTRATOR);

    expect(service.currentAdminRole()).toBeNull();
    expect(service.satisfies(PhysicalPolicy.AdministratorOnly)).toBe(false);
    expect(service.satisfies(PhysicalPolicy.AdminOnly)).toBe(true);
  });
});
