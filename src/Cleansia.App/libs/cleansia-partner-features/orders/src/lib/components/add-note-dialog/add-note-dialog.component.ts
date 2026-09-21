import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaTextareaComponent } from '@cleansia/components';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { notBlank } from '../dialog-validators';

export interface AddNoteDialogData {
  orderId: string;
}

export interface AddNoteDialogResult {
  content: string;
}

@Component({
  selector: 'cleansia-partner-add-note-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, TranslateModule, CleansiaButtonComponent, CleansiaTextareaComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './add-note-dialog.component.html',
  styleUrl: './add-note-dialog.component.scss',
})
export class AddNoteDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as AddNoteDialogData;

  readonly form = this.fb.nonNullable.group({
    content: ['', [Validators.required, notBlank]],
  });

  onCancel(): void {
    this.dialogRef.close();
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const result: AddNoteDialogResult = { content: this.form.getRawValue().content.trim() };
    this.dialogRef.close(result);
  }
}
