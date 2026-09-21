import { ADMIN_ROLE_LABEL_KEYS, ADMIN_ROLES, AdminRole } from '@cleansia/admin-services';
import { ICleansiaSelectOption } from '@cleansia/components';
import { TranslateService } from '@ngx-translate/core';

/** Least privilege until an Administrator says otherwise. */
export const DEFAULT_ADMIN_ROLE = AdminRole.Support;

export function buildAdminRoleOptions(translate: TranslateService): ICleansiaSelectOption[] {
  return ADMIN_ROLES.map((role) => ({
    label: translate.instant(ADMIN_ROLE_LABEL_KEYS[role]),
    value: role,
  }));
}
