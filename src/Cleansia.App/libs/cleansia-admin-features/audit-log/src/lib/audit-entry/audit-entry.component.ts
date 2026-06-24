import { CommonModule, Location } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
} from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { AuditEntryFacade } from './audit-entry.facade';
import { AuditFieldDiff } from './audit-entry.models';
import { formatTimestamp } from '../audit-log/audit-log.models';

@Component({
  selector: 'cleansia-admin-audit-entry',
  standalone: true,
  imports: [
    CommonModule,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
    TranslatePipe,
  ],
  templateUrl: './audit-entry.component.html',
  providers: [AuditEntryFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditEntryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  protected readonly facade = inject(AuditEntryFacade);

  ngOnInit(): void {
    const auditId = this.route.snapshot.paramMap.get('auditId');
    if (auditId) {
      this.facade.loadEntry(auditId);
    }
  }

  formatTimestamp(value: Date | undefined): string {
    return formatTimestamp(value);
  }

  trackByField(_index: number, row: AuditFieldDiff): string {
    return row.field;
  }

  goBack(): void {
    this.location.back();
  }
}
