import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CleansiaButtonComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  ICleansiaSelectOption,
} from '@cleansia/components';
import { DocumentType } from '@cleansia/partner-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Skeleton } from 'primeng/skeleton';
import { ProfileFacade } from '../../profile/profile.facade';

@Component({
  selector: 'cleansia-profile-documents',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaButtonComponent,
    CleansiaSelectComponent,
    Skeleton,
  ],
  templateUrl: './profile-documents.component.html',
})
export class ProfileDocumentsComponent implements OnInit {
  @Input({ required: true }) facade!: ProfileFacade;

  private readonly translate = inject(TranslateService);

  selectedDocumentType: DocumentType = DocumentType.Other;
  documentTypeOptions: ICleansiaSelectOption[] = [];

  ngOnInit(): void {
    this.buildDocumentTypeOptions();
    this.facade.loadEmployeeDocuments();
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const filesArray = Array.from(input.files);
      this.facade.onEmployeeDocumentFilesSelected(
        filesArray,
        this.selectedDocumentType
      );
      input.value = '';
    }
  }

  private buildDocumentTypeOptions(): void {
    this.documentTypeOptions = [
      {
        label: this.translate.instant('global.document_types.1'),
        value: DocumentType.IdentityCard,
      },
      {
        label: this.translate.instant('global.document_types.2'),
        value: DocumentType.Passport,
      },
      {
        label: this.translate.instant('global.document_types.3'),
        value: DocumentType.DriversLicense,
      },
      {
        label: this.translate.instant('global.document_types.4'),
        value: DocumentType.WorkPermit,
      },
      {
        label: this.translate.instant('global.document_types.5'),
        value: DocumentType.Contract,
      },
      {
        label: this.translate.instant('global.document_types.6'),
        value: DocumentType.Certificate,
      },
      {
        label: this.translate.instant('global.document_types.7'),
        value: DocumentType.BankStatement,
      },
      {
        label: this.translate.instant('global.document_types.8'),
        value: DocumentType.TaxDocument,
      },
      {
        label: this.translate.instant('global.document_types.9'),
        value: DocumentType.InsuranceDocument,
      },
      {
        label: this.translate.instant('global.document_types.10'),
        value: DocumentType.Other,
      },
    ];
  }
}
