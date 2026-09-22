import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaAdminRoute } from '@cleansia/services';
import { TranslatePipe } from '@ngx-translate/core';
import { TabsModule } from 'primeng/tabs';

export type AuditLogSegment = 'admin' | 'customer';

@Component({
  selector: 'cleansia-admin-audit-log-segment',
  standalone: true,
  imports: [TabsModule, TranslatePipe],
  templateUrl: './audit-log-segment.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditLogSegmentComponent {
  private readonly router = inject(Router);

  readonly active = input.required<AuditLogSegment>();

  onSegmentChange(value: string | number | undefined): void {
    if (value === this.active()) return;
    if (value === 'customer') {
      this.router.navigate([CleansiaAdminRoute.AUDIT_LOG, 'customers']);
    } else if (value === 'admin') {
      this.router.navigate([CleansiaAdminRoute.AUDIT_LOG]);
    }
  }
}
