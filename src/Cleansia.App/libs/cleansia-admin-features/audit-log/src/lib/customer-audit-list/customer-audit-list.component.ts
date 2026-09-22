import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerActionAuditDto } from '@cleansia/admin-services';
import {
  CleansiaCalendarComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { formatResource, getOutcomeClass, getOutcomeLabelKey } from '../audit-log/audit-log.models';
import { AuditLogSegmentComponent } from '../audit-log-segment/audit-log-segment.component';
import { CustomerAuditListFacade } from './customer-audit-list.facade';
import { getCustomerAuditTableDefinition } from './customer-audit-list.models';

@Component({
  selector: 'cleansia-admin-customer-audit-list',
  standalone: true,
  imports: [
    RouterLink,
    CleansiaCalendarComponent,
    CleansiaSelectComponent,
    CleansiaTextInputComponent,
    TranslatePipe,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaFilterDrawerComponent,
    CleansiaFilterChipsComponent,
    AuditLogSegmentComponent,
    ReactiveFormsModule,
  ],
  templateUrl: './customer-audit-list.component.html',
  providers: [CustomerAuditListFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CustomerAuditListComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(CustomerAuditListFacade);

  private readonly userTemplate = viewChild<TemplateRef<CustomerActionAuditDto>>('userTemplate');
  private readonly resourceTemplate = viewChild<TemplateRef<CustomerActionAuditDto>>('resourceTemplate');
  private readonly outcomeTemplate = viewChild<TemplateRef<CustomerActionAuditDto>>('outcomeTemplate');

  protected readonly table = computed(() => {
    this.facade.lang();
    return getCustomerAuditTableDefinition(
      { onView: (audit) => this.viewEntry(audit) },
      this.translate,
      {
        user: this.userTemplate(),
        resource: this.resourceTemplate(),
        outcome: this.outcomeTemplate(),
      }
    );
  });

  ngOnInit(): void {
    this.facade.loadAudits();
  }

  getOutcomeClass(audit: CustomerActionAuditDto): string {
    return getOutcomeClass(audit.success);
  }

  getOutcomeLabelKey(audit: CustomerActionAuditDto): string {
    return getOutcomeLabelKey(audit.success);
  }

  formatResource(audit: CustomerActionAuditDto): string {
    return formatResource(audit);
  }

  customerRoute(audit: CustomerActionAuditDto): string[] | null {
    return audit.userId ? ['/customers', audit.userId] : null;
  }

  resourceHistoryRoute(audit: CustomerActionAuditDto): (string | CleansiaAdminRoute)[] | null {
    if (!audit.resourceType || !audit.resourceId) return null;
    return [CleansiaAdminRoute.AUDIT_LOG, 'resource', audit.resourceType, audit.resourceId];
  }

  viewEntry(audit: CustomerActionAuditDto): void {
    if (!audit.id) return;
    this.router.navigate([CleansiaAdminRoute.AUDIT_LOG, 'customers', 'entry', audit.id]);
  }
}
