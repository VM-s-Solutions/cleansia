import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  Input,
  OnInit,
} from '@angular/core';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { ProfileLegalDocumentsFacade } from '../../profile/profile-legal-documents.facade';

@Component({
  selector: 'cleansia-partner-profile-legal-documents',
  standalone: true,
  imports: [
    DatePipe,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
  ],
  templateUrl: './profile-legal-documents.component.html',
  styleUrl: './profile-legal-documents.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileLegalDocumentsComponent implements OnInit {
  @Input({ required: true }) facade!: ProfileLegalDocumentsFacade;

  ngOnInit(): void {
    this.facade.load();
  }
}
