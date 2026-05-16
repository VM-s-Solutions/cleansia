import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import {
  GetUserLoyaltyActivityActivityItem,
  LoyaltyEarnSource,
  LoyaltyTier,
  LoyaltyTransactionType,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableColumn,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import {
  GrantPointsDialogComponent,
  GrantPointsDialogMode,
  GrantPointsDialogSubmit,
} from '../grant-points-dialog/grant-points-dialog.component';
import { UserLoyaltyDetailFacade } from './user-loyalty-detail.facade';

@Component({
  selector: 'cleansia-admin-user-loyalty-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    GrantPointsDialogComponent,
    CleansiaPermissionDirective,
  ],
  templateUrl: './user-loyalty-detail.component.html',
  providers: [UserLoyaltyDetailFacade],
})
export class UserLoyaltyDetailComponent
  implements OnInit, AfterViewInit, OnDestroy
{
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  protected readonly facade = inject(UserLoyaltyDetailFacade);
  protected readonly Policy = Policy;

  private readonly destroy$ = new Subject<void>();

  private userId: string | null = null;
  readonly userEmail = signal<string | null>(null);

  // One dialog reused for both grant + revoke; mode flips to drive copy/colors.
  readonly dialogVisible = signal<boolean>(false);
  readonly dialogMode = signal<GrantPointsDialogMode>('grant');

  activityColumns!: TableColumn<GetUserLoyaltyActivityActivityItem>[];

  readonly headerTitle = computed(() => {
    const email = this.userEmail();
    if (email) {
      return this.translate.instant(
        'pages.loyalty_user_detail.title_for_user',
        { email }
      );
    }
    return this.translate.instant('pages.loyalty_user_detail.title');
  });

  readonly tierName = computed(() => {
    const acc = this.facade.account();
    if (!acc) return '';
    return this.translate.instant(this.tierKey(acc.currentTier));
  });

  readonly nextTierName = computed(() => {
    const acc = this.facade.account();
    if (!acc || acc.pointsToNextTier == null) return '';
    return this.translate.instant(this.tierKey(acc.nextTier));
  });

  readonly pointsToNextLabel = computed(() => {
    const acc = this.facade.account();
    if (!acc) return '';
    if (acc.pointsToNextTier == null) {
      return this.translate.instant(
        'pages.loyalty_user_detail.max_tier_reached'
      );
    }
    return this.translate.instant(
      'pages.loyalty_user_detail.points_to_next_tier',
      { count: acc.pointsToNextTier, tier: this.nextTierName() }
    );
  });

  readonly tierAchievedLabel = computed(() => {
    const acc = this.facade.account();
    if (!acc) return '';
    return this.translate.instant(
      'pages.loyalty_user_detail.tier_achieved_on',
      { date: this.formatDate(acc.tierAchievedOn) }
    );
  });

  readonly completedBookingsLabel = computed(() => {
    const acc = this.facade.account();
    if (!acc) return '';
    return this.translate.instant(
      'pages.loyalty_user_detail.completed_bookings',
      { count: acc.completedBookingsCount }
    );
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('userId');
    if (!id) {
      this.router.navigate(['/admin-user-management']);
      return;
    }
    this.userId = id;
    // Email is optionally passed as a query param to avoid an extra fetch.
    const emailParam = this.route.snapshot.queryParamMap.get('email');
    if (emailParam) {
      this.userEmail.set(emailParam);
    }

    this.facade.loadAccount(id);
    this.facade.loadActivity(id, 0, 20);
  }

  ngAfterViewInit(): void {
    this.rebuildActivityColumns();
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.rebuildActivityColumns());
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

  // ---- Display helpers ----

  tierKey(tier: LoyaltyTier): string {
    switch (tier) {
      case LoyaltyTier.BronzeCleaner:
        return 'pages.loyalty_user_detail.tier.BronzeCleaner';
      case LoyaltyTier.SilverMopper:
        return 'pages.loyalty_user_detail.tier.SilverMopper';
      case LoyaltyTier.GoldPolisher:
        return 'pages.loyalty_user_detail.tier.GoldPolisher';
      case LoyaltyTier.PlatinumSparkler:
        return 'pages.loyalty_user_detail.tier.PlatinumSparkler';
      default:
        return '';
    }
  }

  tierAccent(tier: LoyaltyTier): string {
    switch (tier) {
      case LoyaltyTier.BronzeCleaner:
        return 'bronze';
      case LoyaltyTier.SilverMopper:
        return 'silver';
      case LoyaltyTier.GoldPolisher:
        return 'gold';
      case LoyaltyTier.PlatinumSparkler:
        return 'platinum';
      default:
        return 'bronze';
    }
  }

  formatDate(d?: Date): string {
    if (!d) return '—';
    return new Intl.DateTimeFormat(this.translate.currentLang ?? 'en', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(d);
  }

  formatPoints(value: number): string {
    return value > 0 ? `+${value}` : `${value}`;
  }

  // ---- Activity table ----

  private rebuildActivityColumns(): void {
    const t = this.translate;
    this.activityColumns = [
      {
        id: 'occurredOn',
        field: 'occurredOn',
        header: t.instant('pages.loyalty_user_detail.activity.column.date'),
        getValue: (row) => this.formatDate(row.occurredOn),
        width: '22%',
      },
      {
        id: 'type',
        field: 'type',
        header: t.instant('pages.loyalty_user_detail.activity.column.type'),
        getValue: (row) => this.transactionTypeLabel(row.type),
        width: '15%',
      },
      {
        id: 'points',
        field: 'points',
        header: t.instant('pages.loyalty_user_detail.activity.column.points'),
        getValue: (row) => this.formatPoints(row.points),
        width: '15%',
      },
      {
        id: 'source',
        field: 'source',
        header: t.instant('pages.loyalty_user_detail.activity.column.source'),
        getValue: (row) => this.sourceLabel(row.source),
        width: '25%',
      },
      {
        id: 'orderDisplayNumber',
        field: 'orderDisplayNumber',
        header: t.instant('pages.loyalty_user_detail.activity.column.order'),
        getValue: (row) => row.orderDisplayNumber ?? '—',
        width: '23%',
      },
    ];
  }

  transactionTypeLabel(type: LoyaltyTransactionType): string {
    switch (type) {
      case LoyaltyTransactionType.Earn:
        return this.translate.instant(
          'pages.loyalty_user_detail.activity.type.Earn'
        );
      case LoyaltyTransactionType.Revoke:
        return this.translate.instant(
          'pages.loyalty_user_detail.activity.type.Revoke'
        );
      default:
        return '';
    }
  }

  sourceLabel(source: LoyaltyEarnSource): string {
    switch (source) {
      case LoyaltyEarnSource.OrderCompleted:
        return this.translate.instant(
          'pages.loyalty_user_detail.activity.source.OrderCompleted'
        );
      case LoyaltyEarnSource.OrderCancelled:
        return this.translate.instant(
          'pages.loyalty_user_detail.activity.source.OrderCancelled'
        );
      case LoyaltyEarnSource.Referral:
        return this.translate.instant(
          'pages.loyalty_user_detail.activity.source.Referral'
        );
      case LoyaltyEarnSource.ManualGrant:
        return this.translate.instant(
          'pages.loyalty_user_detail.activity.source.ManualGrant'
        );
      default:
        return '';
    }
  }

  // ---- Actions ----

  onActivityPageChange(event: PaginationState): void {
    this.facade.onActivityPageChange(event.first, event.rows);
  }

  openGrant(): void {
    this.dialogMode.set('grant');
    this.dialogVisible.set(true);
  }

  openRevoke(): void {
    this.dialogMode.set('revoke');
    this.dialogVisible.set(true);
  }

  onDialogVisibleChange(value: boolean): void {
    this.dialogVisible.set(value);
  }

  onDialogSubmit(payload: GrantPointsDialogSubmit): void {
    if (payload.mode === 'grant') {
      this.facade.grantPoints(
        { points: payload.points, reason: payload.reason },
        () => this.dialogVisible.set(false)
      );
    } else {
      this.facade.revokePoints(
        { points: payload.points, reason: payload.reason },
        () => this.dialogVisible.set(false)
      );
    }
  }

  onBack(): void {
    this.facade.navigateBack();
  }
}
