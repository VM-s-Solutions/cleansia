import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, Signal, inject } from '@angular/core';
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
import { ProfileDocumentsFacade } from '../../profile/profile-documents.facade';

const FILE_BG_COLORS: Record<string, string> = {
  pdf: '#fef2f2',
  doc: '#eff6ff',
  docx: '#eff6ff',
  xls: '#f0fdf4',
  xlsx: '#f0fdf4',
  csv: '#f0fdf4',
  jpg: '#fefce8',
  jpeg: '#fefce8',
  png: '#fefce8',
  gif: '#fefce8',
  default: '#f3f4f6',
};

const FILE_TEXT_COLORS: Record<string, string> = {
  pdf: '#dc2626',
  doc: '#2563eb',
  docx: '#2563eb',
  xls: '#16a34a',
  xlsx: '#16a34a',
  csv: '#16a34a',
  jpg: '#ca8a04',
  jpeg: '#ca8a04',
  png: '#ca8a04',
  gif: '#ca8a04',
  default: '#6b7280',
};

const IMAGE_EXTENSIONS = new Set(['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp']);

interface DocumentGroup {
  key: string;
  titleKey: string;
  docs: Signal<any[]>;
}

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
  @Input({ required: true }) facade!: ProfileDocumentsFacade;

  private readonly translate = inject(TranslateService);

  documentTypeOptions: ICleansiaSelectOption[] = [];
  documentGroups: DocumentGroup[] = [];
  isDragOver = false;
  showValidation = false;

  ngOnInit(): void {
    this.buildDocumentTypeOptions();
    this.facade.loadEmployeeDocuments();
    this.documentGroups = [
      { key: 'pending', titleKey: 'pages.profile.pending_documents', docs: this.facade.pendingDocuments },
      { key: 'approved', titleKey: 'pages.profile.approved_documents', docs: this.facade.approvedDocuments },
      { key: 'rejected', titleKey: 'pages.profile.rejected_documents', docs: this.facade.rejectedDocuments },
    ];
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.facade.onEmployeeDocumentFilesSelected(
        Array.from(input.files),
        null as unknown as DocumentType
      );
      this.showValidation = false;
      input.value = '';
    }
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    const files = event.dataTransfer?.files;
    if (files && files.length > 0) {
      this.facade.onEmployeeDocumentFilesSelected(
        Array.from(files),
        null as unknown as DocumentType
      );
      this.showValidation = false;
    }
  }

  onStagedTypeChange(index: number, value: DocumentType): void {
    this.facade.updateStagedDocumentType(index, value);
  }

  onSaveDocuments(): void {
    const staged = this.facade.stagedDocuments();
    const hasUntyped = staged.some((d) => !d.documentType);
    if (hasUntyped) {
      this.showValidation = true;
      return;
    }
    this.showValidation = false;
    this.facade.saveEmployeeDocuments();
  }

  getFileExtension(fileName: string | undefined): string {
    if (!fileName) return '?';
    const ext = fileName.split('.').pop()?.toLowerCase() || '';
    return ext.toUpperCase();
  }

  getFileColor(fileName: string | undefined): string {
    if (!fileName) return FILE_BG_COLORS['default'];
    const ext = fileName.split('.').pop()?.toLowerCase() || '';
    return FILE_BG_COLORS[ext] || FILE_BG_COLORS['default'];
  }

  getFileTextColor(fileName: string | undefined): string {
    if (!fileName) return FILE_TEXT_COLORS['default'];
    const ext = fileName.split('.').pop()?.toLowerCase() || '';
    return FILE_TEXT_COLORS[ext] || FILE_TEXT_COLORS['default'];
  }

  isImageFile(fileName: string | undefined): boolean {
    if (!fileName) return false;
    const ext = fileName.split('.').pop()?.toLowerCase() || '';
    return IMAGE_EXTENSIONS.has(ext);
  }

  private buildDocumentTypeOptions(): void {
    this.documentTypeOptions = [
      { label: this.translate.instant('global.document_types.1'), value: DocumentType.IdentityCard },
      { label: this.translate.instant('global.document_types.2'), value: DocumentType.Passport },
      { label: this.translate.instant('global.document_types.3'), value: DocumentType.DriversLicense },
      { label: this.translate.instant('global.document_types.4'), value: DocumentType.WorkPermit },
      { label: this.translate.instant('global.document_types.5'), value: DocumentType.Contract },
      { label: this.translate.instant('global.document_types.6'), value: DocumentType.Certificate },
      { label: this.translate.instant('global.document_types.7'), value: DocumentType.BankStatement },
      { label: this.translate.instant('global.document_types.8'), value: DocumentType.TaxDocument },
      { label: this.translate.instant('global.document_types.9'), value: DocumentType.InsuranceDocument },
      { label: this.translate.instant('global.document_types.10'), value: DocumentType.Other },
    ];
  }
}
