import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { DocumentRequirementDto, DocumentType } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaCheckboxComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
  ICleansiaSelectOption,
  TableAction,
  TableColumn,
  TableConfig,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DOCUMENT_TYPE_LABEL_KEYS } from '../document-type-labels';
import { DocumentRequirementsFacade } from './document-requirements.facade';

@Component({
  selector: 'cleansia-admin-document-requirements',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    ConfirmDialogModule,
    CleansiaButtonComponent,
    CleansiaCheckboxComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTableComponent,
    CleansiaTextInputComponent,
    CleansiaTitleComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './document-requirements.component.html',
  providers: [DocumentRequirementsFacade, ConfirmationService],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentRequirementsComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(DocumentRequirementsFacade);
  protected readonly Policy = Policy;

  private readonly fb = inject(FormBuilder);
  private readonly translate = inject(TranslateService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly destroyRef = inject(DestroyRef);

  /** Null when the form is closed. Non-null while adding or editing. */
  protected readonly editing = signal<DocumentRequirementDto | 'new' | null>(null);

  /** Its own control rather than ngModel: this component is reactive-forms throughout. */
  protected readonly countryControl = new FormControl<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    documentType: [null as DocumentType | null, [Validators.required]],
    isRequired: [true],
    // Mirrors the server's rule: SaveDocumentRequirement refuses a negative sort order.
    sortOrder: [0, [Validators.required, Validators.min(0)]],
  });

  protected readonly countryOptions = computed<ICleansiaSelectOption[]>(() =>
    this.facade.countries().map((country) => ({
      label: country.name ?? country.isoCode ?? '',
      value: country.id ?? '',
    })),
  );

  protected readonly documentTypeOptions = computed<ICleansiaSelectOption[]>(() =>
    Object.entries(DOCUMENT_TYPE_LABEL_KEYS).map(([value, key]) => ({
      label: this.translate.instant(key),
      value: Number(value) as DocumentType,
    })),
  );

  protected readonly columns: TableColumn<DocumentRequirementDto>[] = [
    {
      id: 'documentType',
      field: 'documentType',
      header: 'pages.document_requirements.columns.document_type',
      getValue: (row) => this.translate.instant(this.documentTypeLabelKey(row.documentType)),
    },
    {
      id: 'isRequired',
      field: 'isRequired',
      header: 'pages.document_requirements.columns.required',
      align: 'center',
      getValue: (row) =>
        this.translate.instant(row.isRequired
            ? 'pages.document_requirements.required_yes'
            : 'pages.document_requirements.required_no'),
    },
    {
      id: 'sortOrder',
      field: 'sortOrder',
      header: 'pages.document_requirements.columns.sort_order',
      align: 'center',
    },
  ];

  protected readonly actions: TableAction<DocumentRequirementDto>[] = [
    {
      icon: 'pi pi-pencil',
      tooltip: 'common.edit',
      onClick: (row) => this.startEdit(row),
    },
    {
      icon: 'pi pi-trash',
      color: 'danger',
      tooltip: 'common.delete',
      onClick: (row) => this.confirmDelete(row),
    },
  ];

  /**
   * No paginator: `requirementsGet` returns every row for one country in one array, and a country
   * has at most one rule per document type — ten of them.
   */
  protected readonly tableConfig: TableConfig = {
    paginator: false,
    hover: true,
    emptyMessage: 'pages.document_requirements.empty',
  };

  ngOnInit(): void {
    this.facade.loadCountries();
    this.countryControl.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((countryId) => this.onCountryChange(countryId));
  }

  ngOnDestroy(): void {
    this.facade.ngOnDestroy();
  }

  protected onCountryChange(countryId: string | null): void {
    this.editing.set(null);
    this.facade.selectCountry(countryId);
  }

  protected startCreate(): void {
    this.form.reset({ documentType: null, isRequired: true, sortOrder: 0 });
    this.editing.set('new');
  }

  /**
   * Edit reuses the same form and the same PUT. The server upserts on (country, type), so editing
   * is saving the pair again — there is no separate update route and no id to carry.
   */
  protected startEdit(requirement: DocumentRequirementDto): void {
    this.form.reset({
      documentType: requirement.documentType,
      isRequired: requirement.isRequired,
      sortOrder: requirement.sortOrder,
    });
    this.editing.set(requirement);
  }

  protected cancelEdit(): void {
    this.editing.set(null);
  }

  protected save(): void {
    const countryId = this.facade.selectedCountryId();
    if (this.form.invalid || !countryId) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    if (value.documentType === null) {
      return;
    }

    this.facade.saveRequirement(
      countryId,
      value.documentType,
      value.isRequired,
      value.sortOrder,
    );
    this.editing.set(null);
  }

  protected documentTypeLabelKey(type: DocumentType | null | undefined): string {
    return type == null
      ? 'pages.employee_detail.document_types.unknown'
      : (DOCUMENT_TYPE_LABEL_KEYS[type] ?? 'pages.employee_detail.document_types.unknown');
  }

  private confirmDelete(requirement: DocumentRequirementDto): void {
    this.confirmationService.confirm({
      header: this.translate.instant('pages.document_requirements.delete_confirm_title'),
      message: this.translate.instant('pages.document_requirements.delete_confirm'),
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        if (requirement.id) {
          this.facade.deleteRequirement(requirement.id);
        }
      },
    });
  }
}
