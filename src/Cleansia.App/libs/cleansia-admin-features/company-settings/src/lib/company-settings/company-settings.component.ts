import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  inject,
  OnDestroy,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TenantSettingDto, TenantSettingValueType } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  TableAction,
  TableColumn,
} from '@cleansia/components';
import { PermissionService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import { CompanySettingsFacade } from './company-settings.facade';
import { formatSettingValue, getCompanySettingsTableDefinition } from './company-settings.models';

@Component({
  selector: 'cleansia-admin-company-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './company-settings.component.html',
  providers: [CompanySettingsFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompanySettingsComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly cd = inject(ChangeDetectorRef);
  private readonly translate = inject(TranslateService);
  private readonly permissions = inject(PermissionService);
  protected readonly facade = inject(CompanySettingsFacade);
  protected readonly TenantSettingValueType = TenantSettingValueType;

  readonly intDraft = new FormControl<string>('', { nonNullable: true });
  readonly boolDraft = new FormControl<boolean>(false, { nonNullable: true });

  valueTemplate = viewChild<TemplateRef<TenantSettingDto>>('valueTemplate');

  columns!: TableColumn<TenantSettingDto>[];
  actions!: TableAction<TenantSettingDto>[];

  private readonly destroy$ = new Subject<void>();

  ngOnInit(): void {
    this.facade.connectDraft(this.intDraft, this.boolDraft);
  }

  ngAfterViewInit(): void {
    this.rebuildTableDefinition();
    this.cd.detectChanges();

    this.translate.onLangChange.pipe(takeUntil(this.destroy$)).subscribe(() => {
      this.rebuildTableDefinition();
      this.cd.detectChanges();
    });

    this.facade.loadSettings();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  effectiveValue(setting: TenantSettingDto): string {
    return formatSettingValue(setting, setting.effectiveValue, this.translate);
  }

  private rebuildTableDefinition(): void {
    const definition = getCompanySettingsTableDefinition(
      {
        onEdit: (row) => this.facade.beginEdit(row),
        onSave: () => this.facade.save(),
        onCancel: () => this.facade.cancelEdit(),
        onReset: (row) => this.facade.reset(row),
        isEditing: (row) => this.facade.isEditing(row),
        isBusy: (row) => this.facade.isBusy(row),
      },
      this.translate,
      this.permissions,
      this.valueTemplate()
    );
    this.columns = definition.columns;
    this.actions = definition.actions;
  }
}
