import { CommonModule, Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomerActionAuditDetailDto } from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuditFieldDiff } from '../audit-entry/audit-entry.models';
import {
  formatTimestamp,
  getOutcomeClass,
  getOutcomeLabelKey,
} from '../audit-log/audit-log.models';
import { formatActionLabel } from '../timeline/timeline.models';
import { CustomerAuditEntryFacade } from './customer-audit-entry.facade';
import { PayloadRow } from './customer-audit-entry.models';

@Component({
  selector: 'cleansia-admin-customer-audit-entry',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTitleComponent,
    TranslatePipe,
  ],
  templateUrl: './customer-audit-entry.component.html',
  providers: [CustomerAuditEntryFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerAuditEntryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(CustomerAuditEntryFacade);

  ngOnInit(): void {
    const auditId = this.route.snapshot.paramMap.get('auditId');
    if (auditId) {
      this.facade.loadEntry(auditId);
    }
  }

  formatTimestamp(value: Date | undefined): string {
    return formatTimestamp(value);
  }

  actionLabel(entry: CustomerActionAuditDetailDto): string {
    return formatActionLabel(entry.action, this.translate);
  }

  outcomeClass(entry: CustomerActionAuditDetailDto): string {
    return getOutcomeClass(entry.success);
  }

  outcomeLabelKey(entry: CustomerActionAuditDetailDto): string {
    return getOutcomeLabelKey(entry.success);
  }

  customerRoute(entry: CustomerActionAuditDetailDto): string[] | null {
    return entry.userId ? ['/customers', entry.userId] : null;
  }

  resourceHistoryRoute(
    entry: CustomerActionAuditDetailDto
  ): (string | CleansiaAdminRoute)[] | null {
    if (!entry.resourceType || !entry.resourceId) return null;
    return [CleansiaAdminRoute.AUDIT_LOG, 'resource', entry.resourceType, entry.resourceId];
  }

  trackByKey(_index: number, row: PayloadRow): string {
    return row.key;
  }

  trackByField(_index: number, row: AuditFieldDiff): string {
    return row.field;
  }

  goBack(): void {
    this.location.back();
  }
}
