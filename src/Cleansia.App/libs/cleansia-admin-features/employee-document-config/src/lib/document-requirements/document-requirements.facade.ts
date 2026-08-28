import { Injectable, inject, signal } from '@angular/core';
import {
  AdminClient,
  CountryListItem,
  DocumentRequirementDto,
  DocumentType,
  SaveDocumentRequirementRequest,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

/**
 * The document types a country expects of its cleaners.
 *
 * These rows are what `ApproveEmployee` gates on, which is why they are administered rather than
 * compiled in: requirements change with the law, and a change that needs a release is a change
 * that waits for one.
 */
@Injectable()
export class DocumentRequirementsFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly countries = signal<CountryListItem[]>([]);
  readonly requirements = signal<DocumentRequirementDto[]>([]);
  readonly selectedCountryId = signal<string | null>(null);

  /** Three states, kept apart: the first paint, a reload, and loaded. */
  readonly initialLoading = signal(true);
  readonly loading = signal(false);
  readonly saving = signal(false);

  loadCountries(): void {
    this.adminClient.adminCountryClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.initialLoading.set(false)),
      )
      .subscribe((countries) => {
        if (countries) {
          this.countries.set(countries);
        }
      });
  }

  selectCountry(countryId: string | null): void {
    this.selectedCountryId.set(countryId);
    // Not a reload of the previous country's rows — clear first, or the table shows one country's
    // requirements under another country's name for the length of the round trip.
    this.requirements.set([]);
    if (countryId) {
      this.loadRequirements(countryId);
    }
  }

  loadRequirements(countryId: string): void {
    this.loading.set(true);
    this.adminClient.adminEmployeeDocumentClient
      .requirementsGet(countryId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.loading.set(false)),
      )
      .subscribe((requirements) => {
        if (requirements && this.selectedCountryId() === countryId) {
          this.requirements.set(requirements);
        }
      });
  }

  /**
   * An upsert, not an insert. `(CountryId, DocumentType)` is unique server-side, so saving the same
   * pair twice edits the rule rather than creating a second one — two rows for one pair would not
   * be a variant of the rule, it would be two rules disagreeing.
   */
  saveRequirement(
    countryId: string,
    documentType: DocumentType,
    isRequired: boolean,
    sortOrder: number,
  ): void {
    // Constructed then assigned: a `new SaveDocumentRequirementRequest({...})` object literal is
    // banned by eslint.generated-dto.config.mjs across every generated Command/Request/Dto/Query.
    const request = new SaveDocumentRequirementRequest();
    request.countryId = countryId;
    request.documentType = documentType;
    request.isRequired = isRequired;
    request.sortOrder = sortOrder;

    this.saving.set(true);
    this.adminClient.adminEmployeeDocumentClient
      .requirementsPut(request)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false)),
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.document_requirements.messages.save_success'),
          );
          this.loadRequirements(countryId);
        }
      });
  }

  /**
   * Removing a row un-gates the type. It does not un-approve anybody: approval is decided at the
   * moment an admin approves, and these rows are an input to that decision rather than a standing
   * property of the cleaner.
   */
  deleteRequirement(requirementId: string): void {
    const countryId = this.selectedCountryId();
    if (!countryId) {
      return;
    }

    this.saving.set(true);
    this.adminClient.adminEmployeeDocumentClient
      .requirementsDelete(requirementId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.saving.set(false)),
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.document_requirements.messages.delete_success'),
          );
          this.loadRequirements(countryId);
        }
      });
  }
}
