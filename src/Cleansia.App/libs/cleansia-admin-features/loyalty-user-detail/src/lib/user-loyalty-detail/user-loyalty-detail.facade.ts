import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminCurrencyListItem,
  AdminGdprClient,
  AdminReferralListItem,
  CreditTransactionReason,
  GetUserCreditResponse,
  GetUserLoyaltyAccountResponse,
  GetUserLoyaltyActivityActivityItem,
  GrantPointsManuallyCommand,
  ExpireCustomerCreditCommand,
  FileResponse,
  GdprExportDto,
  IssueCustomerCreditCommand,
  OrderListItem,
  RevokePointsManuallyCommand,
  SortDefinition,
  SortDirection,
  incidentFileName,
} from '@cleansia/admin-services';
import { ICleansiaSelectOption } from '@cleansia/components';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { FileDownloadService, SnackbarService } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

export interface ManualPointsInput {
  points: number;
  reason: string;
}

export interface IssueCreditInput {
  amount: number;
  currencyId: string;
  reason: CreditTransactionReason;
  note: string;
}

export interface ExpireCreditInput {
  currencyId: string;
  note: string;
}

/** A currency the admin may issue credit in — id for the command, code for the screen. */
export interface CreditCurrencyOption {
  id: string;
  code: string;
}

const SUBJECT_ORDERS_LIMIT = 100;

@Injectable()
export class UserLoyaltyDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly gdprClient = inject(AdminGdprClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly router = inject(Router);
  private readonly fileDownload = inject(FileDownloadService);

  readonly account = signal<GetUserLoyaltyAccountResponse | null>(null);
  readonly accountLoading = signal<boolean>(false);

  readonly activity = signal<GetUserLoyaltyActivityActivityItem[]>([]);
  readonly activityLoading = signal<boolean>(false);
  readonly activityTotal = signal<number>(0);

  readonly referralsAsReferrer = signal<AdminReferralListItem[]>([]);
  readonly referralsAsReferred = signal<AdminReferralListItem[]>([]);
  readonly referralsLoading = signal<boolean>(false);
  readonly referralsError = signal<boolean>(false);

  /**
   * The customer's credit balance and ledger.
   *
   * <p>Loaded beside loyalty and kept separate from it in every other respect — owner ruling
   * 2026-09-05, credit is its own feature. The LEDGER is the point: an admin about to compensate
   * someone needs to see whether a colleague already did, and that question is most of what
   * human-in-the-loop means.</p>
   */
  readonly credit = signal<GetUserCreditResponse | null>(null);
  readonly creditLoading = signal<boolean>(false);
  readonly creditSubmitting = signal<boolean>(false);
  readonly creditExpiring = signal<boolean>(false);

  /**
   * The currencies an admin may issue credit in: the ones the platform OPERATES in. Credit is spendable
   * only on an order in the same currency, and an order can only be placed in an active one, so a
   * grant in an inactive currency is money the customer could never spend. The server refuses it too
   * (IssueCustomerCredit.Validator); this keeps the choice off the screen.
   */
  readonly currencies = signal<CreditCurrencyOption[]>([]);

  readonly submitting = signal<boolean>(false);
  readonly exporting = signal<boolean>(false);
  readonly incidentFileExporting = signal<boolean>(false);

  /**
   * The account's bookings, offered as the incident file's optional scope. Every status counts —
   * a cancelled or completed booking is as much an incident's subject as a live one — and whether
   * a picked order is this customer's stays the server's rule.
   */
  readonly subjectOrders = signal<OrderListItem[]>([]);
  readonly subjectOrdersTotal = signal<number>(0);
  readonly subjectOrdersLoading = signal<boolean>(false);
  readonly subjectOrdersError = signal<boolean>(false);
  readonly subjectOrdersTruncated = computed<boolean>(
    () => this.subjectOrdersTotal() > this.subjectOrders().length
  );
  readonly incidentOrderOptions = computed<ICleansiaSelectOption[]>(() =>
    this.subjectOrders()
      .filter((order): order is OrderListItem & { id: string } => !!order.id)
      .map((order) => ({ label: this.incidentOrderLabel(order), value: order.id }))
  );
  readonly hasSubjectOrders = computed<boolean>(() => this.incidentOrderOptions().length > 0);

  private currentUserId: string | null = null;
  private currentActivityOffset = 0;
  private currentActivityLimit = 20;

  loadAccount(userId: string): void {
    this.currentUserId = userId;
    this.accountLoading.set(true);
    this.adminClient.adminLoyaltyClient
      .userAccount(userId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.accountLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.account.set(response);
        }
      });
  }

  loadActivity(userId: string, offset = 0, limit = 20): void {
    this.currentUserId = userId;
    this.currentActivityOffset = offset;
    this.currentActivityLimit = limit;
    this.activityLoading.set(true);
    this.adminClient.adminLoyaltyClient
      .userActivity(userId, offset, limit)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.activityLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.activity.set(response.data ?? []);
          this.activityTotal.set(response.total ?? 0);
        }
      });
  }

  loadReferrals(userId: string): void {
    this.referralsLoading.set(true);
    this.referralsError.set(false);
    this.adminClient.adminReferralClient
      .byUser(userId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.referralsError.set(true);
          return of(null);
        }),
        finalize(() => this.referralsLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.referralsAsReferrer.set(response.asReferrer ?? []);
          this.referralsAsReferred.set(response.asReferred ?? []);
        }
      });
  }

  onActivityPageChange(offset: number, limit: number): void {
    if (!this.currentUserId) return;
    this.loadActivity(this.currentUserId, offset, limit);
  }

  loadSubjectOrders(userId: string): void {
    this.currentUserId = userId;
    this.subjectOrdersLoading.set(true);
    this.subjectOrdersError.set(false);
    // Parameters order: id, isActive, customerName, customerEmail, customerPhone,
    // displayOrderNumber, employeeId, cleaningDateFrom, cleaningDateTo,
    // paymentStatuses, paymentTypes, minTotalPrice, maxTotalPrice, orderStatuses,
    // hasAvailableSpots, isUnassigned, excludeEmployeeId, currencyId, userId, sort, offset, limit
    this.adminClient.adminOrderClient
      .getPaged(
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        userId,
        [new SortDefinition({ field: 'cleaningDateTime', direction: SortDirection.Descending })],
        0,
        SUBJECT_ORDERS_LIMIT
      )
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.subjectOrdersError.set(true);
          return of(null);
        }),
        finalize(() => this.subjectOrdersLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.subjectOrders.set(response.data ?? []);
          this.subjectOrdersTotal.set(response.total ?? 0);
        }
      });
  }

  loadCredit(userId: string): void {
    this.currentUserId = userId;
    this.creditLoading.set(true);
    this.adminClient.adminCreditClient
      .user(userId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null)),
        finalize(() => this.creditLoading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.credit.set(response);
        }
      });
  }

  loadCurrencies(): void {
    this.adminClient.adminCurrencyClient
      .getOverview()
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of([] as AdminCurrencyListItem[]))
      )
      .subscribe((currencies) => {
        // `?? []`: the generated client returns null, not an empty list, for a 204 — reasoned out in
        // service-management/service-form.facade.ts; the `.filter` below is this file's crash site.
        this.currencies.set(
          (currencies ?? [])
            .filter(
              (c): c is AdminCurrencyListItem & { id: string; code: string } =>
                c.isActive && Boolean(c.id) && Boolean(c.code)
            )
            .map((c) => ({ id: c.id, code: c.code }))
        );
      });
  }

  /**
   * Put money on the balance. The company owes it from the moment this succeeds, and there is no
   * "undo" endpoint — a mistake is corrected by spending it or by a payout, both of which involve a
   * person. That is the ruling, not a gap.
   */
  issueCredit(input: IssueCreditInput, onSuccess?: () => void): void {
    if (!this.currentUserId) return;
    this.creditSubmitting.set(true);

    const command = new IssueCustomerCreditCommand();
    command.userId = this.currentUserId;
    command.amount = input.amount;
    // The unit of the amount, chosen by the admin. The server used to fill in the platform default
    // while this screen labelled the field with the customer's largest balance's currency.
    command.currencyId = input.currencyId;
    command.reason = input.reason;
    command.note = input.note;
    // S7a. One id per submission attempt: a network-layer retry reuses this command and the server's
    // unique index collapses it onto one grant, while a fresh click generates a new id and is a
    // genuinely new grant. Without it a double-click gives the money twice.
    command.requestId = crypto.randomUUID();

    this.adminClient.adminCreditClient
      .issue(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant('pages.loyalty_user_detail.credit.error.generic')
          );
          return of(null);
        }),
        finalize(() => this.creditSubmitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.loyalty_user_detail.credit.success')
          );
          if (this.currentUserId) {
            this.loadCredit(this.currentUserId);
          }
          onSuccess?.();
        }
      });
  }

  /**
   * Take one currency's whole balance off the books.
   *
   * <p>The reason this exists is erasure: GdprDeletionService refuses to erase a customer while a
   * balance is positive, and there are no Stripe payouts, so a leaving customer with credit was
   * previously stuck. The admin discharges it, the erasure proceeds. → ExpireCustomerCredit</p>
   *
   * <p>No amount — the server only ever takes the lot, in the ONE currency named: a customer holds
   * one account per currency, and a discharge that picked for itself could not say what it took.
   * The note is required and is the only record of why money the company owed stopped being owed.</p>
   */
  expireCredit(input: ExpireCreditInput, onSuccess?: () => void): void {
    if (!this.currentUserId) return;
    this.creditExpiring.set(true);

    const command = new ExpireCustomerCreditCommand();
    command.userId = this.currentUserId;
    command.currencyId = input.currencyId;
    command.note = input.note;
    // S7a, same shape as the issue path: a transport retry replays this id and the ledger's unique
    // index collapses it, while a second deliberate click is a new id. Less load-bearing here —
    // draining an already-empty balance is a no-op — but the two paths stay the same shape.
    command.requestId = crypto.randomUUID();

    this.adminClient.adminCreditClient
      .expire(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant('pages.loyalty_user_detail.credit.expire_error')
          );
          return of(null);
        }),
        finalize(() => this.creditExpiring.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant('pages.loyalty_user_detail.credit.expire_success', {
              amount: response.amountExpired,
              currency: response.currencyCode,
            })
          );
          if (this.currentUserId) {
            this.loadCredit(this.currentUserId);
          }
          onSuccess?.();
        }
      });
  }

  refresh(): void {
    if (!this.currentUserId) return;
    this.loadAccount(this.currentUserId);
    this.loadCredit(this.currentUserId);
    this.loadActivity(
      this.currentUserId,
      this.currentActivityOffset,
      this.currentActivityLimit
    );
  }

  grantPoints(input: ManualPointsInput, onSuccess?: () => void): void {
    if (!this.currentUserId) return;
    this.submitting.set(true);

    const command = new GrantPointsManuallyCommand();
    command.userId = this.currentUserId;
    command.points = input.points;
    command.reason = input.reason;
    // Client-stable idempotency key: one id per submission attempt — a
    // network-layer retry reuses this same command (same id → server collapses the
    // duplicate), while a fresh user click generates a new id.
    command.requestId = crypto.randomUUID();

    this.adminClient.adminLoyaltyClient
      .grantPoints(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.error.generic'
            )
          );
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.success_grant'
            )
          );
          this.refresh();
          onSuccess?.();
        }
      });
  }

  revokePoints(input: ManualPointsInput, onSuccess?: () => void): void {
    if (!this.currentUserId) return;
    this.submitting.set(true);

    const command = new RevokePointsManuallyCommand();
    command.userId = this.currentUserId;
    command.points = input.points;
    command.reason = input.reason;
    // Client-stable idempotency key — see grantPoints above.
    command.requestId = crypto.randomUUID();

    this.adminClient.adminLoyaltyClient
      .revokePoints(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showError(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.error.generic'
            )
          );
          return of(null);
        }),
        finalize(() => this.submitting.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.snackbarService.showSuccess(
            this.translate.instant(
              'pages.loyalty_user_detail.grant_dialog.success_revoke'
            )
          );
          this.refresh();
          onSuccess?.();
        }
      });
  }

  /**
   * The admin subject export, with the customer's audit trail in it. The server records the act as an
   * admin audit row and a GdprRequest — this only asks for the file and hands it to the browser.
   */
  exportSubjectData(): void {
    const userId = this.currentUserId;
    if (!userId || this.exporting()) return;

    this.exporting.set(true);
    this.gdprClient
      .export(userId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showApiError(error, 'pages.customer_detail.export_error');
          return of(null);
        }),
        finalize(() => this.exporting.set(false))
      )
      .subscribe((data: GdprExportDto | null) => {
        if (data) {
          this.downloadJson(data, subjectExportFileName(userId, new Date()));
          this.snackbarService.showSuccess(
            this.translate.instant('pages.customer_detail.export_success')
          );
        }
      });
  }

  /**
   * The incident file: the server builds the PDF from the database and records the build as an
   * admin audit row. No scope asks for the whole account; a picked order id narrows it to that
   * order.
   */
  exportIncidentFile(orderScope: string | null): void {
    const userId = this.currentUserId;
    if (!userId || this.incidentFileExporting()) return;

    const orderId = orderScope || undefined;
    this.incidentFileExporting.set(true);
    this.gdprClient
      .incidentFile(userId, orderId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showApiError(
            error,
            'pages.customer_detail.incident_file.error'
          );
          return of(null);
        }),
        finalize(() => this.incidentFileExporting.set(false))
      )
      .subscribe((file: FileResponse | null) => {
        if (file) {
          this.fileDownload.downloadBlob(
            file.data,
            file.fileName ?? incidentFileName(userId, new Date())
          );
          this.snackbarService.showSuccess(
            this.translate.instant('pages.customer_detail.incident_file.success')
          );
        }
      });
  }

  navigateBack(): void {
    this.router.navigate(['/admin-user-management']);
  }

  private incidentOrderLabel(order: OrderListItem): string {
    const number = order.displayOrderNumber || order.id || '';
    if (!order.cleaningDateTime) return number;
    const date = new Intl.DateTimeFormat(this.translate.currentLang ?? 'en', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(order.cleaningDateTime);
    return `${number} · ${date}`;
  }

  private downloadJson(data: unknown, fileName: string): void {
    const json = JSON.stringify(data, null, 2);
    this.fileDownload.downloadBlob(
      new Blob([json], { type: 'application/json' }),
      fileName
    );
  }
}

export function subjectExportFileName(userId: string, exportedAt: Date): string {
  return `subject-export-${userId}-${exportedAt.toISOString().slice(0, 10)}.json`;
}
