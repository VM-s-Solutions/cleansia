import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { ButtonModule } from 'primeng/button';
import { InputTextarea } from 'primeng/inputtextarea';

export interface AddNoteDialogData {
  orderId: string;
}

export interface AddNoteDialogResult {
  content: string;
}

@Component({
  selector: 'cleansia-add-note-dialog',
  standalone: true,
  imports: [FormsModule, TranslateModule, ButtonModule, InputTextarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './add-note-dialog.component.html',
  styleUrl: './add-note-dialog.component.scss',
})
export class AddNoteDialogComponent {
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly data = this.config.data as AddNoteDialogData;
  content = '';

  onCancel(): void {
    this.dialogRef.close();
  }

  onSave(): void {
    if (!this.content.trim()) return;
    const result: AddNoteDialogResult = {
      content: this.content.trim(),
    };
    this.dialogRef.close(result);
  }
}
