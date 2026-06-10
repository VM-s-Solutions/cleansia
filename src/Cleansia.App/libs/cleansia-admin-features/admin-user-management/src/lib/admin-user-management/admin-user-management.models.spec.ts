import { AdminUserListItem } from '@cleansia/admin-services';
import { TableColumn } from '@cleansia/components';
import { PermissionService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { getAdminUserTableDefinition } from './admin-user-management.models';

describe('getAdminUserTableDefinition - last login column', () => {
  const translate = {
    instant: (key: string) => key,
  } as unknown as TranslateService;

  const permissions = {
    hasPolicy: () => true,
  } as unknown as PermissionService;

  const defs = {
    onEdit: () => undefined,
    onToggleStatus: () => undefined,
    onViewLoyalty: () => undefined,
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

  it('renders the formatted last login date when present', () => {
    const column = getLastLoginColumn();
    const lastLoginAt = new Date('2026-05-20T10:15:00.000Z');
    const row = { lastLoginAt } as AdminUserListItem;

    expect(column.getValue?.(row)).toBe(lastLoginAt.toLocaleDateString());
  });

  it('renders an empty value when last login is null', () => {
    const column = getLastLoginColumn();
    const row = { lastLoginAt: undefined } as AdminUserListItem;

    expect(column.getValue?.(row)).toBe('');
  });
});
