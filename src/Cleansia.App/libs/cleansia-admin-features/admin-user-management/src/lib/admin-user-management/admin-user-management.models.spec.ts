import { AdminRole, AdminUserListItem } from '@cleansia/admin-services';
import { TableColumn } from '@cleansia/components';
import { PermissionService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { getAdminUserTableDefinition } from './admin-user-management.models';

describe('getAdminUserTableDefinition - role column', () => {
  const translate = { instant: (key: string) => `t:${key}` } as unknown as TranslateService;
  const permissions = { hasPolicy: () => true } as unknown as PermissionService;
  const defs = { onEdit: () => undefined, onToggleStatus: () => undefined, onViewCustomer: () => undefined };

  function getRoleColumn(): TableColumn<AdminUserListItem> {
    const { columns } = getAdminUserTableDefinition(defs, translate, permissions);
    const column = columns.find((c) => c.id === 'adminRole');
    expect(column).toBeDefined();
    return column as TableColumn<AdminUserListItem>;
  }

  it('sits beside the email with the translated header', () => {
    const { columns } = getAdminUserTableDefinition(defs, translate, permissions);
    const ids = columns.map((c) => c.id);

    expect(ids.indexOf('adminRole')).toBe(ids.indexOf('email') + 1);
    expect(getRoleColumn().header).toBe('t:pages.admin_user_management.columns.role');
  });

  it.each([
    [AdminRole.Administrator, 'admin_roles.administrator'],
    [AdminRole.Manager, 'admin_roles.manager'],
    [AdminRole.Support, 'admin_roles.support'],
    [AdminRole.Accountant, 'admin_roles.accountant'],
  ])('renders role %s through its label key', (role, key) => {
    const row = AdminUserListItem.fromJS({ adminRole: role });

    expect(getRoleColumn().getValue?.(row)).toBe(`t:${key}`);
  });

  it('renders nothing for a row that carries no role', () => {
    const row = AdminUserListItem.fromJS({});

    expect(getRoleColumn().getValue?.(row)).toBe('');
  });
});

describe('getAdminUserTableDefinition - last login column', () => {
  const translate = {
    instant: (key: string) => key,
    currentLang: 'cs',
  } as unknown as TranslateService;

  const permissions = {
    hasPolicy: () => true,
  } as unknown as PermissionService;

  const defs = {
    onEdit: () => undefined,
    onToggleStatus: () => undefined,
    onViewCustomer: () => undefined,
  };

  function getLastLoginColumn(): TableColumn<AdminUserListItem> {
    const { columns } = getAdminUserTableDefinition(defs, translate, permissions);
    const column = columns.find((c) => c.id === 'lastLoginAt');
    expect(column).toBeDefined();
    return column as TableColumn<AdminUserListItem>;
  }

  it('exposes a last_login column header', () => {
    const column = getLastLoginColumn();
    expect(column.header).toBe('pages.admin_user_management.columns.last_login');
    expect(column.field).toBe('lastLoginAt');
  });

  it('renders the last login day the way the session language writes it', () => {
    const column = getLastLoginColumn();
    const lastLoginAt = new Date(2026, 4, 20, 10, 15);
    const row = { lastLoginAt } as AdminUserListItem;

    expect(column.getValue?.(row)).toBe('20. 5. 2026');
  });

  it('renders an empty value when last login is null', () => {
    const column = getLastLoginColumn();
    const row = { lastLoginAt: undefined } as AdminUserListItem;

    expect(column.getValue?.(row)).toBe('');
  });
});
