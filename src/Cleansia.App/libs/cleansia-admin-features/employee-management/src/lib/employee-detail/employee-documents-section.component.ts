import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { EmployeeDocumentItem } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
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
    CleansiaPermissionDirective,
  ],
  templateUrl: './employee-documents-section.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeDocumentsSectionComponent {
  protected readonly Policy = Policy;
  @Input({ required: true }) facade!: EmployeeDocumentsFacade;
  @Input() employeeId?: string;
  @Output() rejectDocument = new EventEmitter<EmployeeDocumentItem>();
}
