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
  template: `
    <div class="add-note-dialog">
      <div class="add-note-dialog__header">
        <i class="pi pi-file-edit add-note-dialog__icon"></i>
        <h3>{{ 'pages.order_details.add_note' | translate }}</h3>
      </div>
      <div class="add-note-dialog__body">
        <label class="add-note-dialog__label">
          {{ 'pages.order_details.note_content' | translate }}
        </label>
        <textarea
          pInputTextarea
          [(ngModel)]="content"
          [rows]="5"
          [placeholder]="'pages.order_details.note_content_placeholder' | translate"
          class="add-note-dialog__textarea"
        ></textarea>
      </div>
      <div class="add-note-dialog__footer">
        <p-button
          [label]="'global.actions.cancel' | translate"
          severity="secondary"
          [outlined]="true"
          (onClick)="onCancel()"
        />
        <p-button
          [label]="'global.actions.save' | translate"
          [disabled]="!content.trim()"
          (onClick)="onSave()"
        />
      </div>
    </div>
  `,
  styles: [`
    .add-note-dialog {
      padding: 1rem;
    }
    .add-note-dialog__header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      margin-bottom: 1.5rem;
    }
    .add-note-dialog__header h3 {
      margin: 0;
      font-size: 1.25rem;
    }
    .add-note-dialog__icon {
      color: var(--blue-500);
      font-size: 1.5rem;
    }
    .add-note-dialog__label {
      display: block;
      margin-bottom: 0.5rem;
      font-weight: 500;
    }
    .add-note-dialog__textarea {
      width: 100%;
    }
    .add-note-dialog__footer {
      display: flex;
      justify-content: flex-end;
      gap: 0.75rem;
      margin-top: 1.5rem;
    }
  `],
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
