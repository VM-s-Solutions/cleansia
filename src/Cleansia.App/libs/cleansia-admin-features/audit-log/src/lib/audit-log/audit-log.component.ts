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
import { Router } from '@angular/router';
import { AdminActionAuditDto } from '@cleansia/admin-services';
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
import { AuditLogSegmentComponent } from '../audit-log-segment/audit-log-segment.component';
import { AuditLogFacade } from './audit-log.facade';
import {
  getAuditLogTableActions,
  getAuditLogTableColumns,
  getOutcomeClass,
  getOutcomeLabelKey,
} from './audit-log.models';

@Component({
  selector: 'cleansia-admin-audit-log',
  standalone: true,
  imports: [
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
  templateUrl: './audit-log.component.html',
  providers: [AuditLogFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(AuditLogFacade);

  private readonly outcomeTemplate = viewChild<TemplateRef<AdminActionAuditDto>>('outcomeTemplate');

  protected readonly table = computed(() => {
    this.facade.lang();
    return {
      columns: getAuditLogTableColumns(this.translate, this.outcomeTemplate()),
      actions: getAuditLogTableActions(this.translate, (audit) => this.viewEntry(audit)),
    };
  });

  ngOnInit(): void {
    this.facade.loadAudits();
  }

  getOutcomeClass(audit: AdminActionAuditDto): string {
    return getOutcomeClass(audit.success);
  }

  getOutcomeLabelKey(audit: AdminActionAuditDto): string {
    return getOutcomeLabelKey(audit.success);
  }

  viewEntry(audit: AdminActionAuditDto): void {
    if (!audit.id) return;
    this.router.navigate([CleansiaAdminRoute.AUDIT_LOG, 'entry', audit.id]);
  }
}
