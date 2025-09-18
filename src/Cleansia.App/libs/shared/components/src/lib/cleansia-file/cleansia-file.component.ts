import { CommonModule } from '@angular/common';
import {
  HttpClient,
  HttpClientModule,
  HttpErrorResponse,
} from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { FileUploadModule } from 'primeng/fileupload';
import { MessageModule } from 'primeng/message';
import { ProgressBarModule } from 'primeng/progressbar';

export interface FileItem {
  name: string;
  size: number;
  type: string;
  url?: string;
}

@Component({
  selector: 'cleansia-file',
  standalone: true,
  imports: [
    CommonModule,
    FileUploadModule,
    ButtonModule,
    ProgressBarModule,
    MessageModule,
    HttpClientModule,
  ],
  templateUrl: './cleansia-file.component.html',
})
export class FileComponent {
  multiple = input(true);
  accept = input('image/*,.pdf,.doc,.docx');
  maxFileSize = input(10_000_000);

  private http = inject(HttpClient);

  files = signal<FileItem[]>([]);
  uploadedFiles = signal<FileItem[]>([]);
  progress = signal(0);
  messages = signal<{ severity: string; text: string }[]>([]);

  onSelect(event: any) {
    const filesArray: File[] = Array.isArray(event.files)
      ? event.files
      : Array.from(event.files ?? []);
    const newFiles = filesArray.map((file: File) => ({
      name: file.name,
      size: file.size,
      type: file.type,
    }));
    this.files.update((prev) => [...prev, ...newFiles]);
  }

  onRemove(event: any) {
    const removedFile = {
      name: event.file.name,
      size: event.file.size,
      type: event.file.type,
    };
    this.files.update((prev) =>
      prev.filter((f) => f.name !== removedFile.name)
    );
  }

  removeFile(file: FileItem) {
    this.files.update((prev) => prev.filter((f) => f !== file));
  }

  onUpload(event: any) {
    this.progress.set(100);
    this.messages.update((prev) => [
      ...prev,
      { severity: 'success', text: 'Upload successful!' },
    ]);
    this.files.set([]);
    // Handle uploaded files from event.files or response
    const uploaded = event.files.map((file: File) => ({
      name: file.name,
      size: file.size,
      type: file.type,
      url: `/api/files/${file.name}`, // Replace with actual URL
    }));
    this.uploadedFiles.update((prev) => [...prev, ...uploaded]);
    setTimeout(() => this.progress.set(0), 1000);
  }

  onError(event: HttpErrorResponse) {
    this.messages.update((prev) => [
      ...prev,
      { severity: 'error', text: `Upload failed: ${event.message}` },
    ]);
    this.progress.set(0);
  }
}
