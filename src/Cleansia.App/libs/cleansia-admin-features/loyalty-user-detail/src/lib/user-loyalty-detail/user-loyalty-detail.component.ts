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
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TimelineComponent } from '@cleansia/admin-features/audit-log';
import {
  AdminReferralListItem,
  CreditTransactionReason,
  GetUserCreditCurrencyAccount,
  GetUserCreditLedgerEntry,
  GetUserLoyaltyActivityActivityItem,
  LoyaltyEarnSource,
  LoyaltyTier,
  LoyaltyTransactionType,
  ReferralStatus,
} from '@cleansia/admin-services';
import {
  CleansiaButtonComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaSelectComponent,
  CleansiaTableComponent,
  CleansiaTitleComponent,
  PaginationState,
  TableColumn,
} from '@cleansia/components';
import { CleansiaPermissionDirective } from '@cleansia/directives';
import { Policy } from '@cleansia/services';
import { formatDate } from '@cleansia/utils';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subject, takeUntil } from 'rxjs';
import {
  GrantPointsDialogComponent,
  GrantPointsDialogMode,
  GrantPointsDialogSubmit,
} from '../grant-points-dialog/grant-points-dialog.component';
import {
  IssueCreditDialogComponent,
  IssueCreditDialogSubmit,
} from '../issue-credit-dialog/issue-credit-dialog.component';
import {
  ExpireCreditDialogComponent,
  ExpireCreditDialogSubmit,
} from '../expire-credit-dialog/expire-credit-dialog.component';
import { UserLoyaltyDetailFacade } from './user-loyalty-detail.facade';

@Component({
  selector: 'cleansia-admin-user-loyalty-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaSelectComponent,
    CleansiaTableComponent,
    CleansiaTitleComponent,
    GrantPointsDialogComponent,
    IssueCreditDialogComponent,
    ExpireCreditDialogComponent,
    CleansiaPermissionDirective,
    TimelineComponent,
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

  readonly userId = signal<string | null>(null);
  readonly userEmail = signal<string | null>(null);

  readonly incidentOrderControl = new FormControl<string | null>(null);

  // One dialog reused for both grant + revoke; mode flips to drive copy/colors.
  readonly dialogVisible = signal<boolean>(false);
  readonly dialogMode = signal<GrantPointsDialogMode>('grant');

  // Credit gets its OWN dialog, not a third mode of the points one — owner ruling 2026-09-05, and
  // the two forms have nothing in common beyond a free-text reason.
  readonly creditDialogVisible = signal<boolean>(false);
  readonly expireCreditDialogVisible = signal<boolean>(false);
  /** The one account the open discharge dialog is about; null while it is closed. */
  readonly expireCreditAccount = signal<GetUserCreditCurrencyAccount | null>(null);
  readonly expireCreditBalanceLabel = computed(() => {
    const account = this.expireCreditAccount();
    return account ? this.facade.formatBalance(account.balance, account.currencyCode) : '';
  });

  activityColumns!: TableColumn<GetUserLoyaltyActivityActivityItem>[];
  private readonly creditColumnsByCurrency = new Map<string, TableColumn<GetUserCreditLedgerEntry>[]>();
  referralsAsReferrerColumns!: TableColumn<AdminReferralListItem>[];
  referralsAsReferredColumns!: TableColumn<AdminReferralListItem>[];

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

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('userId');
    if (!id) {
      this.router.navigate(['/admin-user-management']);
      return;
    }
    this.userId.set(id);
    // Email is optionally passed as a query param to avoid an extra fetch.
    const emailParam = this.route.snapshot.queryParamMap.get('email');
    if (emailParam) {
      this.userEmail.set(emailParam);
    }

    this.facade.loadAccount(id);
    this.facade.loadActivity(id, 0, 20);
    this.facade.loadReferrals(id);
    this.facade.loadCredit(id);
    this.facade.loadCurrencies();
    this.facade.loadSubjectOrders(id);
  }

  ngAfterViewInit(): void {
    this.rebuildActivityColumns();
    this.rebuildReferralColumns();
    this.translate.onLangChange
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.rebuildActivityColumns();
        this.rebuildReferralColumns();
        this.creditColumnsByCurrency.clear();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.facade.ngOnDestroy();
  }

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
    return formatDate(d, this.translate.currentLang, 'dateTime') || '—';
  }

  formatPoints(value: number): string {
    return value > 0 ? `+${value}` : `${value}`;
  }

  /**
   * The ledger, as a statement. Amount FIRST and signed, because the question an admin brings to this
   * table is "how much, and which way" — a reason column read before the number tells them nothing.
   * A ledger row carries no currency of its own; the account it belongs to does, so the columns are
   * built once per currency and kept until the language changes.
   */
  creditColumnsFor(account: GetUserCreditCurrencyAccount): TableColumn<GetUserCreditLedgerEntry>[] {
    const currencyCode = account.currencyCode ?? '';
    let columns = this.creditColumnsByCurrency.get(currencyCode);
    if (!columns) {
      columns = this.buildCreditColumns(account.currencyCode);
      this.creditColumnsByCurrency.set(currencyCode, columns);
    }
    return columns;
  }

  private buildCreditColumns(currencyCode: string | undefined): TableColumn<GetUserCreditLedgerEntry>[] {
    const t = this.translate;
    return [
      {
        id: 'createdOn',
        field: 'createdOn',
        header: t.instant('pages.loyalty_user_detail.credit.column.date'),
        getValue: (row) => this.formatDate(row.createdOn),
        numeric: true,
        width: '22%',
      },
      {
        id: 'amount',
        field: 'amount',
        header: t.instant('pages.loyalty_user_detail.credit.column.amount'),
        getValue: (row) => this.facade.formatLedgerAmount(row.amount, currencyCode),
        numeric: true,
        width: '16%',
      },
      {
        id: 'reason',
        field: 'reason',
        header: t.instant('pages.loyalty_user_detail.credit.column.reason'),
        getValue: (row) => t.instant(this.creditReasonKey(row.reason)),
        width: '24%',
      },
      {
        id: 'note',
        field: 'note',
        header: t.instant('pages.loyalty_user_detail.credit.column.note'),
        getValue: (row) => row.note ?? '—',
        width: '38%',
      },
    ];
  }

  private rebuildActivityColumns(): void {
    const t = this.translate;
    this.activityColumns = [
      {
        id: 'occurredOn',
        field: 'occurredOn',
        header: t.instant('pages.loyalty_user_detail.activity.column.date'),
        getValue: (row) => this.formatDate(row.occurredOn),
        numeric: true,
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
        numeric: true,
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

  private rebuildReferralColumns(): void {
    this.referralsAsReferrerColumns = this.buildReferralColumns(
      'pages.loyalty_referrals.column.referred',
      (row) => row.referredEmail || '—'
    );
    this.referralsAsReferredColumns = this.buildReferralColumns(
      'pages.loyalty_referrals.column.referrer',
      (row) => row.referrerEmail || '—'
    );
  }

  private buildReferralColumns(
    counterpartHeaderKey: string,
    counterpartValue: (row: AdminReferralListItem) => string
  ): TableColumn<AdminReferralListItem>[] {
    const t = this.translate;
    return [
      {
        id: 'counterpart',
        field: 'id',
        header: t.instant(counterpartHeaderKey),
        getValue: counterpartValue,
        width: '26%',
      },
      {
        id: 'status',
        field: 'status',
        header: t.instant('pages.loyalty_referrals.column.status'),
        getValue: (row) => this.referralStatusLabel(row.status),
        width: '14%',
      },
      {
        id: 'acceptedOn',
        field: 'acceptedOn',
        header: t.instant('pages.loyalty_referrals.column.accepted_on'),
        getValue: (row) => this.formatDate(row.acceptedOn),
        numeric: true,
        width: '20%',
      },
      {
        id: 'qualifiedOn',
        field: 'firstQualifyingOrderOn',
        header: t.instant('pages.loyalty_referrals.column.qualified_on'),
        getValue: (row) => this.formatDate(row.firstQualifyingOrderOn),
        numeric: true,
        width: '20%',
      },
      {
        id: 'points',
        field: 'pointsAwardedToReferrer',
        header: t.instant('pages.loyalty_referrals.column.points_awarded'),
        getValue: (row) =>
          row.pointsAwardedToReferrer == null &&
          row.pointsAwardedToReferred == null
            ? '—'
            : t.instant('pages.loyalty_referrals.points_format', {
                referrer: row.pointsAwardedToReferrer ?? 0,
                referred: row.pointsAwardedToReferred ?? 0,
              }),
        numeric: true,
        width: '20%',
      },
    ];
  }

  referralStatusLabel(status: ReferralStatus | undefined): string {
    switch (status) {
      case ReferralStatus.Accepted:
        return this.translate.instant('pages.loyalty_referrals.status.Accepted');
      case ReferralStatus.Qualified:
        return this.translate.instant(
          'pages.loyalty_referrals.status.Qualified'
        );
      case ReferralStatus.Expired:
        return this.translate.instant('pages.loyalty_referrals.status.Expired');
      case ReferralStatus.Reversed:
        return this.translate.instant('pages.loyalty_referrals.status.Reversed');
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

  openIssueCredit(): void {
    this.creditDialogVisible.set(true);
  }

  onCreditDialogVisibleChange(value: boolean): void {
    this.creditDialogVisible.set(value);
  }

  onIssueCredit(payload: IssueCreditDialogSubmit): void {
    this.facade.issueCredit(payload, () => this.creditDialogVisible.set(false));
  }

  openExpireCredit(account: GetUserCreditCurrencyAccount): void {
    this.expireCreditAccount.set(account);
    this.expireCreditDialogVisible.set(true);
  }

  onExpireCreditDialogVisibleChange(value: boolean): void {
    this.expireCreditDialogVisible.set(value);
    if (!value) {
      this.expireCreditAccount.set(null);
    }
  }

  onExpireCredit(payload: ExpireCreditDialogSubmit): void {
    this.facade.expireCredit(payload, () => this.onExpireCreditDialogVisibleChange(false));
  }

  creditReasonKey(reason: CreditTransactionReason | undefined): string {
    switch (reason) {
      case CreditTransactionReason.DisputeSettlement:
        return 'pages.loyalty_user_detail.credit.reason.dispute_settlement';
      case CreditTransactionReason.CleanerNoShow:
        return 'pages.loyalty_user_detail.credit.reason.cleaner_no_show';
      case CreditTransactionReason.Goodwill:
        return 'pages.loyalty_user_detail.credit.reason.goodwill';
      case CreditTransactionReason.OrderPayment:
        return 'pages.loyalty_user_detail.credit.reason.order_payment';
      case CreditTransactionReason.OrderPaymentReturned:
        return 'pages.loyalty_user_detail.credit.reason.order_payment_returned';
      case CreditTransactionReason.Expired:
        return 'pages.loyalty_user_detail.credit.reason.expired';
      default:
        return 'pages.loyalty_user_detail.credit.reason.unknown';
    }
  }

  exportSubjectData(): void {
    this.facade.exportSubjectData();
  }

  toggleIncidentPanel(): void {
    this.facade.toggleIncidentPanel();
  }

  exportIncidentFile(): void {
    this.facade.exportIncidentFile(this.incidentOrderControl.value);
  }

  reloadSubjectOrders(): void {
    const id = this.userId();
    if (id) {
      this.facade.loadSubjectOrders(id);
    }
  }

  onBack(): void {
    this.facade.navigateBack();
  }
}
