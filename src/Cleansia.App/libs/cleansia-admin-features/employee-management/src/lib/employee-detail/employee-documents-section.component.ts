import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { EmployeeDocumentItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { EmployeeDocumentsFacade } from './employee-documents.facade';

@Component({
  selector: 'cleansia-employee-documents-section',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
  ],
  templateUrl: './employee-documents-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeDocumentsSectionComponent {
  @Input({ required: true }) facade!: EmployeeDocumentsFacade;
  @Input() employeeId?: string;
  @Output() rejectDocument = new EventEmitter<EmployeeDocumentItem>();
}
