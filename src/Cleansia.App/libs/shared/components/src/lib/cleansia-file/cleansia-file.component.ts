import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, forwardRef, input, output, OnInit, ViewChild, ElementRef, inject } from '@angular/core';
import { NG_VALUE_ACCESSOR, ValidationErrors } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ErrorPipe } from '@cleansia/pipes';
import { ButtonModule } from 'primeng/button';
import { CleansiaBaseFormInputComponent } from '../cleansia-base-form';

export interface FileItem {
  name: string;
  size: number;
  type: string;
}

@Component({
  selector: 'cleansia-file',
  standalone: true,
  imports: [CommonModule, ButtonModule, ErrorPipe, TranslateModule],
  templateUrl: './cleansia-file.component.html',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CleansiaFileComponent),
      multi: true,
    },
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CleansiaFileComponent extends CleansiaBaseFormInputComponent implements OnInit {
  @ViewChild('fileInput', { static: true }) fileInput!: ElementRef<HTMLInputElement>;

  private readonly translate = inject(TranslateService);

  multiple = input(true);
  accept = input('image/*,.pdf,.doc,.docx');
  maxFileSize = input(10_000_000); // 10MB default
  id = input<string>(this.getDefaultLabelId());

  filesChanged = output<File[]>();

  selectedFiles: File[] = [];
  isDragOver = false;

  override ngOnInit(): void {
    super.ngOnInit();

    // Watch for changes in form control validation state
    if (this.formControl) {
      this.formControl.statusChanges.subscribe(() => {
        this.updateValidationDisplay();
      });
    }
  }

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = input.files;

    if (!files) return;

    const fileArray = Array.from(files);
    this.processFiles(fileArray);
    this.markAsTouched();
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;

    const files = event.dataTransfer?.files;
    if (!files) return;

    const fileArray = Array.from(files);
    this.processFiles(fileArray);
    this.markAsTouched();
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }

  private processFiles(files: File[]): void {
    const validFiles: File[] = [];
    const allValidationErrors: ValidationErrors = {};

    for (const file of files) {
      const validation = this.validateFile(file);
      if (validation.isValid) {
        validFiles.push(file);
      } else {
        // Merge validation errors for all invalid files
        Object.assign(allValidationErrors, validation.validationErrors);
      }
    }

    // Add valid files to selection
    if (this.multiple()) {
      this.selectedFiles = [...this.selectedFiles, ...validFiles];
    } else {
      this.selectedFiles = validFiles.slice(0, 1);
    }

    // Set validation errors if any
    if (Object.keys(allValidationErrors).length > 0) {
      this.setValidationErrors(allValidationErrors);
    } else if (this.selectedFiles.length > 0) {
      // Only clear validation errors if we actually have files
      this.clearValidationErrors();
    }

    this.updateFormControl();
  }

  private validateFile(file: File): { isValid: boolean; validationErrors: ValidationErrors } {
    const validationErrors: ValidationErrors = {};

    // Validate file size
    if (file.size > this.maxFileSize()) {
      const maxSizeMB = parseFloat((this.maxFileSize() / (1024 * 1024)).toFixed(1));
      validationErrors['fileSize'] = {
        maxSize: maxSizeMB,
        fileName: file.name,
        actualSize: parseFloat((file.size / (1024 * 1024)).toFixed(1))
      };
    }

    // Validate file type
    if (!this.isValidFileType(file)) {
      validationErrors['fileType'] = {
        fileName: file.name,
        acceptedTypes: this.accept(),
        actualType: file.type || file.name.split('.').pop()
      };
    }

    return {
      isValid: Object.keys(validationErrors).length === 0,
      validationErrors
    };
  }

  private isValidFileType(file: File): boolean {
    const acceptedTypes = this.accept().split(',').map(type => type.trim());
    const fileName = file.name.toLowerCase();
    const fileType = file.type.toLowerCase();
    const fileExtension = '.' + fileName.split('.').pop();

    return acceptedTypes.some(acceptedType => {
      acceptedType = acceptedType.toLowerCase();

      // Check extension (e.g., ".pdf")
      if (acceptedType.startsWith('.')) {
        return fileName.endsWith(acceptedType);
      }

      // Check MIME type with wildcard (e.g., "image/*")
      if (acceptedType.includes('*')) {
        const baseType = acceptedType.split('/')[0];
        return fileType.startsWith(baseType + '/');
      }

      // Check exact MIME type
      return fileType === acceptedType;
    });
  }

  removeFile(index: number): void {
    this.selectedFiles = this.selectedFiles.filter((_, i) => i !== index);
    this.clearValidationErrors();
    this.updateFormControl();
    this.markAsTouched();
  }

  triggerFileSelect(): void {
    this.fileInput.nativeElement.click();
    this.markAsTouched();
  }

  private updateFormControl(): void {
    this.onChange(this.selectedFiles);
    this.filesChanged.emit(this.selectedFiles);

    if (this.formControl) {
      this.formControl.markAsDirty();
    }

    // Update validation display after form control update
    this.updateValidationDisplay();
  }

  private markAsTouched(): void {
    this.onTouch();
    if (this.formControl) {
      this.formControl.markAsTouched();
    }
  }

  private setValidationErrors(validationErrors: ValidationErrors): void {
    if (this.formControl) {
      const currentErrors = this.formControl.errors || {};
      // Remove any existing file validation errors
      delete currentErrors['fileType'];
      delete currentErrors['fileSize'];
      delete currentErrors['fileValidation'];

      this.formControl.setErrors({
        ...currentErrors,
        ...validationErrors
      });
    }
  }

  private clearValidationErrors(): void {
    if (this.formControl?.errors) {
      const errors = { ...this.formControl.errors };

      // Only clear file validation errors from the component, not form validator errors
      delete errors['fileType'];
      delete errors['fileSize'];
      delete errors['fileValidation'];

      // Don't clear fileRequired error if there are still no files
      if (this.selectedFiles.length === 0 && errors['fileRequired']) {
        // Keep the fileRequired error
      } else if (this.selectedFiles.length > 0) {
        // Clear fileRequired error when files are present
        delete errors['fileRequired'];
      }

      const hasRemainingErrors = Object.keys(errors).length > 0;
      this.formControl.setErrors(hasRemainingErrors ? errors : null);
    }
  }

  override writeValue(value: File[] | null): void {
    this.selectedFiles = value || [];
    // Update validation display when value is written
    setTimeout(() => this.updateValidationDisplay(), 0);
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
  }

  private updateValidationDisplay(): void {
    if (!this.formControl || !this.formControl.errors) {
      return;
    }

    // Check if there's a fileRequired error and no files selected
    if (this.formControl.errors['fileRequired'] && this.selectedFiles.length === 0) {
      // Ensure the component visually reflects the error state
      // The error display will be handled by the template through the base component's error handling
    }
  }

  private getDefaultLabelId(): string {
    return 'cleansia-file-' + Math.random().toString(36).substring(2);
  }
}