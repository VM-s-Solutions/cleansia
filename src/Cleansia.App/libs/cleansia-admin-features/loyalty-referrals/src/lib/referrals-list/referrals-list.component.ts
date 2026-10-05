import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { AdminReferralListItem } from '@cleansia/admin-services';
import {
  CleansiaCalendarComponent,
  CleansiaFilterChipsComponent,
  CleansiaFilterDrawerComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaStatusBadgeComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  PaginationState,
} from '@cleansia/components';
import { PermissionService, Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ReferralInterventionDialogComponent } from '../referral-intervention-dialog/referral-intervention-dialog.component';
import {
  ReferralInterventionMode,
  ReferralInterventionSubmit,
} from '../referral-intervention-dialog/referral-intervention-dialog.models';
import { ReferralsListFacade } from './referrals-list.facade';
import {
  formatHoldReasons,
  formatReferralCredit,
  getReferralInterventionActions,
  getReferralTableColumns,
} from './referrals-list.models';

@Component({
  selector: 'cleansia-admin-referrals-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaCalendarComponent,
    CleansiaFilterChipsComponent,
    CleansiaFilterDrawerComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaStatusBadgeComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    ReferralInterventionDialogComponent,
  ],
  templateUrl: './referrals-list.component.html',
  providers: [ReferralsListFacade],
})
export class ReferralsListComponent implements OnInit {
  private readonly translate = inject(TranslateService);
  private readonly permissionService = inject(PermissionService);
  protected readonly facade = inject(ReferralsListFacade);

  private readonly statusTemplate = viewChild<TemplateRef<AdminReferralListItem>>('statusTemplate');

  readonly dialogVisible = signal<boolean>(false);
  readonly dialogMode = signal<ReferralInterventionMode>('reverse');
  private readonly interventionTarget = signal<AdminReferralListItem | null>(null);
  protected readonly interventionCredit = computed(() => {
    this.facade.lang();
    const row = this.interventionTarget();
    if (!row || (row.creditAwardedToReferrer == null && row.creditAwardedToReferred == null)) return null;
    return formatReferralCredit(row, this.translate);
  });

  protected readonly table = computed(() => {
    this.facade.lang();
    return {
      columns: getReferralTableColumns(this.translate, this.statusTemplate()),
      actions: getReferralInterventionActions(
        {
          canIntervene: this.permissionService.hasPolicy(Policy.CanInterveneReferral),
          onReverse: (row) => this.openIntervention(row, 'reverse'),
          onForceQualify: (row) => this.openIntervention(row, 'forceQualify'),
          onRelease: (row) => this.openIntervention(row, 'release'),
          onReject: (row) => this.openIntervention(row, 'reject'),
        },
        this.translate
      ),
    };
  });

  ngOnInit(): void {
    this.facade.loadReferrals();
  }

  holdReasons(row: AdminReferralListItem): string | null {
    return formatHoldReasons(row, this.translate);
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  openIntervention(row: AdminReferralListItem, mode: ReferralInterventionMode): void {
    this.interventionTarget.set(row);
    this.dialogMode.set(mode);
    this.dialogVisible.set(true);
  }

  onDialogVisibleChange(value: boolean): void {
    this.dialogVisible.set(value);
    if (!value) {
      this.interventionTarget.set(null);
    }
  }

  onInterventionSubmit(payload: ReferralInterventionSubmit): void {
    const id = this.interventionTarget()?.id;
    if (!id) return;

    const close = () => this.onDialogVisibleChange(false);
    switch (payload.mode) {
      case 'reverse':
        this.facade.reverseReferral(id, payload.reason, close);
        break;
      case 'reject':
        this.facade.rejectReferral(id, payload.reason, close);
        break;
      case 'forceQualify':
        this.facade.forceQualifyReferral(id, payload.reason, close);
        break;
      case 'release':
        this.facade.releaseReferral(id, payload.reason, close);
        break;
    }
  }
}
