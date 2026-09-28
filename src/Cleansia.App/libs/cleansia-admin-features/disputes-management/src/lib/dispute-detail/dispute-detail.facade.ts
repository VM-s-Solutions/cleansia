import { Injectable, computed, inject, signal } from '@angular/core';
import {
  AddDisputeMessageCommand,
  AdminDisputeClient,
  AdminOrderClient,
  AssignedEmployeeDto,
  DisputeDetails,
  DisputeSettlementPreference,
  DisputeStatus,
  ResolveDisputeCleanerCharge,
  ResolveDisputeCommand,
  UpdateDisputeStatusCommand,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import { PermissionService, Policy, SnackbarService } from '@cleansia/services';
import { formatMoney, localeFor } from '@cleansia/utils';
import { TranslateService } from '@ngx-translate/core';
import { catchError, finalize, of, takeUntil } from 'rxjs';

const TERMINAL_STATUSES: ReadonlySet<DisputeStatus> = new Set([
  DisputeStatus.Resolved,
  DisputeStatus.Closed,
]);

export interface DisputeCleanerChargeInput {
  employeeId: string;
  amount: number;
  reason: string;
}

@Injectable()
export class DisputeDetailFacade extends UnsubscribeControlDirective {
  private readonly disputeClient = inject(AdminDisputeClient);
  private readonly orderClient = inject(AdminOrderClient);
  private readonly permissions = inject(PermissionService);
  private readonly snackbar = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  readonly dispute = signal<DisputeDetails | null>(null);
  readonly crew = signal<AssignedEmployeeDto[]>([]);
  readonly loading = signal<boolean>(false);
  readonly hasError = signal<boolean>(false);

  readonly resolving = signal<boolean>(false);
  readonly updatingStatus = signal<boolean>(false);
  readonly sendingMessage = signal<boolean>(false);

  readonly refundAmountLabel = computed(() => this.moneyLabel(this.dispute()?.refundAmount));
  readonly cardRefundedLabel = computed(() => this.moneyLabel(this.dispute()?.cardRefundedAmount));
  readonly creditReturnedLabel = computed(() => this.moneyLabel(this.dispute()?.creditReturnedAmount));

  readonly settlesInCredit = computed(
    () => this.dispute()?.settlementPreference === DisputeSettlementPreference.Credit
  );

  readonly isTerminal = computed(() => {
    const status = this.dispute()?.status?.value;
    return status != null && TERMINAL_STATUSES.has(status);
  });

  loadDispute(disputeId: string): void {
    this.loading.set(true);
    this.hasError.set(false);

    this.disputeClient
      .details(disputeId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.hasError.set(true);
          return of(null);
        }),
        finalize(() => this.loading.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.dispute.set(response);
          this.loadCrew(response.orderId);
        }
      });
  }

  resolve(
    disputeId: string,
    refundAmount: number | null,
    resolutionNotes: string | null,
    chargeToCleaner: DisputeCleanerChargeInput | null = null
  ): void {
    if (!disputeId || this.resolving()) return;

    this.resolving.set(true);
    const command = new ResolveDisputeCommand();
    command.disputeId = disputeId;
    command.refundAmount = refundAmount ?? undefined;
    command.resolutionNotes = resolutionNotes?.trim() || undefined;
    if (chargeToCleaner) {
      const charge = new ResolveDisputeCleanerCharge();
      charge.employeeId = chargeToCleaner.employeeId;
      charge.amount = chargeToCleaner.amount;
      charge.reason = chargeToCleaner.reason.trim();
      command.chargeToCleaner = charge;
    }

    this.disputeClient
      .resolve(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of('error' as const)),
        finalize(() => this.resolving.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccessTranslated('pages.disputes_management.resolve.submitted');
        this.loadDispute(disputeId);
      });
  }

  updateStatus(disputeId: string, newStatus: DisputeStatus): void {
    if (!disputeId || this.updatingStatus()) return;

    this.updatingStatus.set(true);
    const command = new UpdateDisputeStatusCommand();
    command.disputeId = disputeId;
    command.newStatus = newStatus;

    this.disputeClient
      .updateStatus(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of('error' as const)),
        finalize(() => this.updatingStatus.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccessTranslated('pages.disputes_management.status_update.success');
        this.loadDispute(disputeId);
      });
  }

  addMessage(disputeId: string, message: string, onSuccess: () => void): void {
    const trimmed = message.trim();
    if (!disputeId || !trimmed || this.sendingMessage()) return;

    this.sendingMessage.set(true);
    const command = new AddDisputeMessageCommand();
    command.disputeId = disputeId;
    command.message = trimmed;
    command.isStaffMessage = true;

    this.disputeClient
      .addMessage(command)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of('error' as const)),
        finalize(() => this.sendingMessage.set(false))
      )
      .subscribe((result) => {
        if (result === 'error') return;
        this.snackbar.showSuccessTranslated('pages.disputes_management.message.sent');
        onSuccess();
        this.loadDispute(disputeId);
      });
  }

  private loadCrew(orderId: string | undefined): void {
    if (
      !orderId ||
      this.isTerminal() ||
      !this.permissions.hasPolicy(Policy.CanResolveDispute) ||
      !this.permissions.hasPolicy(Policy.CanViewOrderDetailAdmin)
    ) {
      return;
    }

    this.orderClient
      .details(orderId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => of(null))
      )
      .subscribe((order) => this.crew.set(order?.assignedEmployees ?? []));
  }

  private moneyLabel(amount: number | undefined): string {
    if (amount == null) return '';
    return formatMoney(amount, this.dispute()?.currency?.code, localeFor(this.translate.currentLang), {
      fractionDigits: 2,
    });
  }
}
