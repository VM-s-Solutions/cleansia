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
import {
  ReferralInterventionDialogComponent,
  ReferralInterventionMode,
  ReferralInterventionSubmit,
} from '../referral-intervention-dialog/referral-intervention-dialog.component';
import { ReferralsListFacade } from './referrals-list.facade';
import { getReferralInterventionActions, getReferralTableColumns } from './referrals-list.models';

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
  private readonly pointsTemplate = viewChild<TemplateRef<AdminReferralListItem>>('pointsTemplate');

  readonly dialogVisible = signal<boolean>(false);
  readonly dialogMode = signal<ReferralInterventionMode>('reverse');
  private interventionTarget: AdminReferralListItem | null = null;

  protected readonly table = computed(() => {
    this.facade.lang();
    return {
      columns: getReferralTableColumns(this.translate, this.statusTemplate(), this.pointsTemplate()),
      actions: getReferralInterventionActions(
        {
          canIntervene: this.permissionService.hasPolicy(Policy.CanInterveneReferral),
          onReverse: (row) => this.openIntervention(row, 'reverse'),
          onForceQualify: (row) => this.openIntervention(row, 'forceQualify'),
        },
        this.translate
      ),
    };
  });

  ngOnInit(): void {
    this.facade.loadReferrals();
  }

  onPageChange(event: PaginationState): void {
    this.facade.onPageChange(event.first, event.rows);
  }

  openIntervention(row: AdminReferralListItem, mode: ReferralInterventionMode): void {
    this.interventionTarget = row;
    this.dialogMode.set(mode);
    this.dialogVisible.set(true);
  }

  onDialogVisibleChange(value: boolean): void {
    this.dialogVisible.set(value);
    if (!value) {
      this.interventionTarget = null;
    }
  }

  onInterventionSubmit(payload: ReferralInterventionSubmit): void {
    const id = this.interventionTarget?.id;
    if (!id) return;

    const close = () => this.onDialogVisibleChange(false);
    if (payload.mode === 'reverse') {
      this.facade.reverseReferral(id, payload.reason, close);
    } else {
      this.facade.forceQualifyReferral(id, payload.reason, close);
    }
  }

  formatPointsAwarded(row: AdminReferralListItem): string {
    if (row.pointsAwardedToReferrer == null && row.pointsAwardedToReferred == null) {
      return this.translate.instant('pages.loyalty_referrals.not_yet');
    }
    return this.translate.instant('pages.loyalty_referrals.points_format', {
      referrer: row.pointsAwardedToReferrer ?? 0,
      referred: row.pointsAwardedToReferred ?? 0,
    });
  }
}
