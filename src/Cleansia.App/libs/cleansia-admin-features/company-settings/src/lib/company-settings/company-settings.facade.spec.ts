import { FormControl } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import {
  AdminClient,
  GetTenantSettingsResponse,
  ResetTenantSettingResponse,
  SetTenantSettingCommand,
  SetTenantSettingResponse,
  TenantSettingDto,
  TenantSettingValueType,
} from '@cleansia/admin-services';
import { DialogService, SnackbarService } from '@cleansia/services';
import { of, Subject, throwError } from 'rxjs';
import { CompanySettingsFacade } from './company-settings.facade';

describe('CompanySettingsFacade', () => {
  let facade: CompanySettingsFacade;
  let getAllMock: jest.Mock;
  let setMock: jest.Mock;
  let resetMock: jest.Mock;
  let confirmMock: jest.Mock;
  let snackbar: { showSuccessTranslated: jest.Mock };
  let intDraft: FormControl<string>;
  let boolDraft: FormControl<boolean>;

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

  const catalogue = (...settings: TenantSettingDto[]) => GetTenantSettingsResponse.fromJS({ settings });

  beforeEach(() => {
    getAllMock = jest.fn().mockReturnValue(of(catalogue(staleDevices, expiredCodes)));
    setMock = jest.fn().mockImplementation((command: SetTenantSettingCommand) =>
      of(SetTenantSettingResponse.fromJS({ key: command.key, value: command.value }))
    );
    resetMock = jest.fn().mockImplementation((key: string) =>
      of(ResetTenantSettingResponse.fromJS({ key, value: '90' }))
    );
    confirmMock = jest.fn().mockReturnValue(of(true));
    snackbar = { showSuccessTranslated: jest.fn() };
    intDraft = new FormControl<string>('', { nonNullable: true });
    boolDraft = new FormControl<boolean>(false, { nonNullable: true });

    TestBed.configureTestingModule({
      providers: [
        CompanySettingsFacade,
        {
          provide: AdminClient,
          useValue: {
            adminTenantSettingsClient: { getAll: getAllMock, set: setMock, reset: resetMock },
          },
        },
        { provide: SnackbarService, useValue: snackbar },
        { provide: DialogService, useValue: { confirmTranslated: confirmMock } },
      ],
    });

    facade = TestBed.inject(CompanySettingsFacade);
    facade.connectDraft(intDraft, boolDraft);
  });

  describe('loadSettings', () => {
    it('loads the whole catalogue and settles the loading flags', () => {
      facade.loadSettings();

      expect(getAllMock).toHaveBeenCalledTimes(1);
      expect(facade.settings()).toEqual([staleDevices, expiredCodes]);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('leaves the list an empty array when the response carries no settings', () => {
      getAllMock.mockReturnValue(of(GetTenantSettingsResponse.fromJS({})));

      facade.loadSettings();

      expect(facade.settings()).toEqual([]);
      expect(facade.initialLoading()).toBe(false);
      expect(facade.hasError()).toBe(false);
    });

    it('settles the error state and stops loading when the read fails', () => {
      getAllMock.mockReturnValue(throwError(() => new Error('boom')));

      facade.loadSettings();

      expect(facade.hasError()).toBe(true);
      expect(facade.settings()).toEqual([]);
      expect(facade.loading()).toBe(false);
      expect(facade.initialLoading()).toBe(false);
    });

    it('clears a previous error on the next successful read', () => {
      getAllMock.mockReturnValueOnce(throwError(() => new Error('boom')));
      facade.loadSettings();
      expect(facade.hasError()).toBe(true);

      facade.loadSettings();

      expect(facade.hasError()).toBe(false);
      expect(facade.settings()).toEqual([staleDevices, expiredCodes]);
    });

    it('keeps loading across a reload issued before the first response arrived', () => {
      const first = new Subject<GetTenantSettingsResponse>();
      const second = new Subject<GetTenantSettingsResponse>();
      getAllMock.mockReturnValueOnce(first).mockReturnValueOnce(second);

      facade.loadSettings();
      facade.loadSettings();

      expect(facade.loading()).toBe(true);
      second.next(catalogue(expiredCodes));
      second.complete();
      expect(facade.loading()).toBe(false);
      expect(facade.settings()).toEqual([expiredCodes]);
    });

    it('drops a slow earlier response once a newer reload was issued', () => {
      const slow = new Subject<GetTenantSettingsResponse>();
      const fast = new Subject<GetTenantSettingsResponse>();
      getAllMock.mockReturnValueOnce(slow).mockReturnValueOnce(fast);

      facade.loadSettings();
      facade.loadSettings();
      fast.next(catalogue(expiredCodes));
      fast.complete();
      slow.next(catalogue(staleDevices));
      slow.complete();

      expect(facade.settings()).toEqual([expiredCodes]);
      expect(facade.loading()).toBe(false);
    });
  });

  describe('beginEdit / cancelEdit', () => {
    beforeEach(() => facade.loadSettings());

    it('opens the int row with its effective value in the int draft', () => {
      facade.beginEdit(staleDevices);

      expect(facade.editingKey()).toBe('retention.stale_devices.days');
      expect(facade.isEditing(staleDevices)).toBe(true);
      expect(facade.isEditing(expiredCodes)).toBe(false);
      expect(intDraft.value).toBe('120');
    });

    it('opens the bool row with its effective value in the bool draft', () => {
      facade.beginEdit(expiredCodes);

      expect(facade.editingKey()).toBe('retention.expired_codes.enabled');
      expect(boolDraft.value).toBe(true);
    });

    it('moves the edit to another row without saving the first', () => {
      facade.beginEdit(staleDevices);
      intDraft.setValue('7');

      facade.beginEdit(expiredCodes);

      expect(facade.editingKey()).toBe('retention.expired_codes.enabled');
      expect(setMock).not.toHaveBeenCalled();
    });

    it('closes the edit on cancel', () => {
      facade.beginEdit(staleDevices);

      facade.cancelEdit();

      expect(facade.editingKey()).toBeNull();
      expect(facade.isEditing(staleDevices)).toBe(false);
    });
  });

  describe('save', () => {
    beforeEach(() => facade.loadSettings());

    it('sends the int draft trimmed as the command body, toasts, closes the edit and re-reads', () => {
      facade.beginEdit(staleDevices);
      intDraft.setValue(' 45 ');

      facade.save();

      const command: SetTenantSettingCommand = setMock.mock.calls[0][0];
      expect(command).toBeInstanceOf(SetTenantSettingCommand);
      expect(command.toJSON()).toEqual({ key: 'retention.stale_devices.days', value: '45' });
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_settings.messages.save_success');
      expect(facade.editingKey()).toBeNull();
      expect(facade.busyKey()).toBeNull();
      expect(getAllMock).toHaveBeenCalledTimes(2);
    });

    it('sends the bool draft in the catalogue canonical form', () => {
      facade.beginEdit(expiredCodes);
      boolDraft.setValue(false);

      facade.save();

      const command: SetTenantSettingCommand = setMock.mock.calls[0][0];
      expect(command.toJSON()).toEqual({ key: 'retention.expired_codes.enabled', value: 'false' });
    });

    it('keeps the edit open, toasts nothing and clears the in-flight key when the write is refused', () => {
      setMock.mockReturnValue(throwError(() => new Error('tenant_setting.invalid_value')));
      facade.beginEdit(staleDevices);
      intDraft.setValue('0');

      facade.save();

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.editingKey()).toBe('retention.stale_devices.days');
      expect(facade.busyKey()).toBeNull();
      expect(getAllMock).toHaveBeenCalledTimes(1);
    });

    it('marks the row busy while the write is in flight and ignores a second save', () => {
      const pending = new Subject<SetTenantSettingResponse>();
      setMock.mockReturnValue(pending);
      facade.beginEdit(staleDevices);

      facade.save();
      facade.save();

      expect(facade.busyKey()).toBe('retention.stale_devices.days');
      expect(facade.isBusy(staleDevices)).toBe(true);
      expect(setMock).toHaveBeenCalledTimes(1);
      pending.next(SetTenantSettingResponse.fromJS({ key: staleDevices.key, value: '120' }));
      pending.complete();
      expect(facade.busyKey()).toBeNull();
    });

    it('does nothing when no row is being edited', () => {
      facade.save();

      expect(setMock).not.toHaveBeenCalled();
    });
  });

  describe('reset', () => {
    beforeEach(() => facade.loadSettings());

    it('confirms with the key, deletes the override, toasts and re-reads', () => {
      facade.reset(staleDevices);

      expect(confirmMock).toHaveBeenCalledWith(
        'pages.company_settings.confirm_reset',
        'pages.company_settings.confirm_reset_title',
        { key: 'retention.stale_devices.days' }
      );
      expect(resetMock).toHaveBeenCalledWith('retention.stale_devices.days');
      expect(snackbar.showSuccessTranslated).toHaveBeenCalledWith('pages.company_settings.messages.reset_success');
      expect(facade.busyKey()).toBeNull();
      expect(getAllMock).toHaveBeenCalledTimes(2);
    });

    it('does nothing when the confirmation is declined', () => {
      confirmMock.mockReturnValue(of(false));

      facade.reset(staleDevices);

      expect(resetMock).not.toHaveBeenCalled();
      expect(getAllMock).toHaveBeenCalledTimes(1);
    });

    it('closes an open edit of the same row once the reset lands', () => {
      facade.beginEdit(staleDevices);

      facade.reset(staleDevices);

      expect(facade.editingKey()).toBeNull();
    });

    it('toasts nothing, clears the in-flight key and leaves the list alone when the reset is refused', () => {
      resetMock.mockReturnValue(throwError(() => new Error('tenant_setting.unknown_key')));

      facade.reset(staleDevices);

      expect(snackbar.showSuccessTranslated).not.toHaveBeenCalled();
      expect(facade.busyKey()).toBeNull();
      expect(getAllMock).toHaveBeenCalledTimes(1);
    });

    it('marks the row busy while the reset is in flight and ignores a second reset', () => {
      const pending = new Subject<ResetTenantSettingResponse>();
      resetMock.mockReturnValue(pending);

      facade.reset(staleDevices);
      facade.reset(staleDevices);

      expect(facade.isBusy(staleDevices)).toBe(true);
      expect(confirmMock).toHaveBeenCalledTimes(1);
      pending.next(ResetTenantSettingResponse.fromJS({ key: staleDevices.key, value: '90' }));
      pending.complete();
      expect(facade.busyKey()).toBeNull();
    });
  });
});
