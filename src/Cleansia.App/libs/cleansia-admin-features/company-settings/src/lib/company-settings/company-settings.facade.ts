import { Injectable, inject, signal } from '@angular/core';
import { FormControl } from '@angular/forms';
import { AdminClient, TenantSettingDto, TenantSettingValueType } from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { DialogService, SnackbarService } from '@cleansia/services';
import { catchError, filter, finalize, of, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { buildSetTenantSettingCommand, formatBoolSetting, parseBoolSetting } from './company-settings.models';

@Injectable()
export class CompanySettingsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbar = inject(SnackbarService);
  private readonly dialog = inject(DialogService);

  // Not paginated: get-all returns the whole catalogue, so there is no totalRecords.
  readonly settings = signal<TenantSettingDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly initialLoading = signal<boolean>(true);
  readonly hasError = signal<boolean>(false);

  readonly editingKey = signal<string | null>(null);
  readonly busyKey = signal<string | null>(null);

  private intDraft: FormControl<string> | null = null;
  private boolDraft: FormControl<boolean> | null = null;
  private readonly reload$ = new Subject<void>();

  constructor() {
    super();
    this.reload$
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.hasError.set(false);
        }),
        // The flag settles on emission, not in an inner finalize: switchMap tears the superseded
        // request down before the next one subscribes, and its finalize would clear the loader early.
        switchMap(() =>
          this.adminClient.adminTenantSettingsClient.getAll().pipe(
            catchError(() => {
              this.hasError.set(true);
              return of(null);
            })
          )
        ),
        takeUntil(this.destroyed$)
      )
      .subscribe((response) => {
        this.settings.set(response?.settings ?? []);
        this.loading.set(false);
        if (this.initialLoading()) {
          this.initialLoading.set(false);
        }
      });
  }

  loadSettings(): void {
    this.reload$.next();
  }

  connectDraft(intDraft: FormControl<string>, boolDraft: FormControl<boolean>): void {
    this.intDraft = intDraft;
    this.boolDraft = boolDraft;
  }

  isEditing(setting: TenantSettingDto): boolean {
    return !!setting.key && this.editingKey() === setting.key;
  }

  isBusy(setting: TenantSettingDto): boolean {
    return !!setting.key && this.busyKey() === setting.key;
  }

  beginEdit(setting: TenantSettingDto): void {
    if (!setting.key || this.busyKey()) return;
    if (setting.valueType === TenantSettingValueType.Bool) {
      this.boolDraft?.setValue(parseBoolSetting(setting.effectiveValue));
    } else {
      this.intDraft?.setValue(setting.effectiveValue ?? '');
    }
    this.editingKey.set(setting.key);
  }

  cancelEdit(): void {
    this.editingKey.set(null);
  }

  save(): void {
    const key = this.editingKey();
    const setting = this.settings().find((s) => s.key === key);
    if (!key || !setting || this.busyKey()) return;

    this.busyKey.set(key);
    this.adminClient.adminTenantSettingsClient
      .set(buildSetTenantSettingCommand(key, this.draftValueOf(setting)))
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.busyKey.set(null))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated('pages.company_settings.messages.save_success');
          this.editingKey.set(null);
          this.loadSettings();
        }
      });
  }

  reset(setting: TenantSettingDto): void {
    const key = setting.key;
    if (!key || this.busyKey()) return;

    this.dialog
      .confirmTranslated('pages.company_settings.confirm_reset', 'pages.company_settings.confirm_reset_title', { key })
      .pipe(
        filter((confirmed) => confirmed),
        tap(() => this.busyKey.set(key)),
        switchMap(() =>
          this.adminClient.adminTenantSettingsClient.reset(key).pipe(
            catchError(() => of(null)),
            finalize(() => this.busyKey.set(null))
          )
        ),
        takeUntil(this.destroyed$)
      )
      .subscribe((response) => {
        if (response) {
          this.snackbar.showSuccessTranslated('pages.company_settings.messages.reset_success');
          if (this.editingKey() === key) this.editingKey.set(null);
          this.loadSettings();
        }
      });
  }

  private draftValueOf(setting: TenantSettingDto): string {
    if (setting.valueType === TenantSettingValueType.Bool) {
      return formatBoolSetting(this.boolDraft?.value ?? false);
    }
    return (this.intDraft?.value ?? '').trim();
  }
}
