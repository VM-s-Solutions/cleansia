import {
  GdprRequestDto,
  GdprRequestStatus,
} from '@cleansia/admin-services';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import {
  getGdprRequestStatusOptions,
  getGdprRequestTableDefinition,
} from './data-protection.models';

describe('data-protection models', () => {
  const translate = {
    instant: (key: string) => key,
  } as unknown as TranslateService;

  const row = (
    status: GdprRequestStatus,
    requestType = 'Deletion',
    id: string | null = 'req-1'
  ) =>
    GdprRequestDto.fromJS({
      id: id ?? undefined,
      userId: 'user-1',
      requestType,
      status,
    });

  describe('getGdprRequestStatusOptions', () => {
    it('offers every status with its translated label', () => {
      expect(getGdprRequestStatusOptions(translate)).toEqual([
        { label: 'pages.data_protection.request_status.Pending', value: GdprRequestStatus.Pending },
        { label: 'pages.data_protection.request_status.Processing', value: GdprRequestStatus.Processing },
        { label: 'pages.data_protection.request_status.Completed', value: GdprRequestStatus.Completed },
        { label: 'pages.data_protection.request_status.Failed', value: GdprRequestStatus.Failed },
      ]);
    });
  });

  describe('retry action', () => {
    const build = (hasPolicy: boolean, retrying = false) => {
      const permissions = {
        hasPolicy: jest.fn((policy: string) =>
          policy === Policy.CanAdminDeleteUserAccount ? hasPolicy : false
        ),
      } as unknown as PermissionService;
      const defs = {
        onFulfil: jest.fn(),
        onRetry: jest.fn(),
        isRetrying: jest.fn(() => retrying),
      };
      const { actions } = getGdprRequestTableDefinition(
        defs,
        translate,
        permissions,
        () => ''
      );
      const retry = actions.find((a) => a.icon === 'pi pi-refresh');
      if (!retry) throw new Error('retry action missing');
      return { retry, defs };
    };

    it('shows on a Failed deletion request', () => {
      const { retry } = build(true);
      expect(retry.visible?.(row(GdprRequestStatus.Failed))).toBe(true);
    });

    it('shows on a Processing deletion request — whether it is stale enough is the server\'s call', () => {
      const { retry } = build(true);
      expect(retry.visible?.(row(GdprRequestStatus.Processing))).toBe(true);
    });

    it('hides on a Pending or Completed request', () => {
      const { retry } = build(true);
      expect(retry.visible?.(row(GdprRequestStatus.Pending))).toBe(false);
      expect(retry.visible?.(row(GdprRequestStatus.Completed))).toBe(false);
    });

    it('hides on an Export request whatever its status', () => {
      const { retry } = build(true);
      expect(retry.visible?.(row(GdprRequestStatus.Failed, 'Export'))).toBe(false);
    });

    it('hides on a row without an id', () => {
      const { retry } = build(true);
      expect(retry.visible?.(row(GdprRequestStatus.Failed, 'Deletion', null))).toBe(false);
    });

    it('hides without the admin-delete permission', () => {
      const { retry } = build(false);
      expect(retry.visible?.(row(GdprRequestStatus.Failed))).toBe(false);
    });

    it('is disabled while that row is being retried', () => {
      const { retry, defs } = build(true, true);
      expect(retry.disabled?.(row(GdprRequestStatus.Failed))).toBe(true);
      expect(defs.isRetrying).toHaveBeenCalled();
    });

    it('hands the clicked row to the retry callback', () => {
      const { retry, defs } = build(true);
      const failed = row(GdprRequestStatus.Failed);
      retry.onClick(failed);
      expect(defs.onRetry).toHaveBeenCalledWith(failed);
    });
  });
});
