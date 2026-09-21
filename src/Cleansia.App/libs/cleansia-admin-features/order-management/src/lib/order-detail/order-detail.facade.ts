import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AdminClient,
  AdminGdprClient,
  Code,
  CustomerAuditClient,
  FileResponse,
  OrderItem,
  OrderStatus,
  PaymentStatus,
  UserItem,
  incidentFileName,
} from '@cleansia/admin-services';
import { UnsubscribeControlDirective } from '@cleansia/directives';
import {
  AuditResourceType,
  FileDownloadService,
  extractApiErrorCode,
  CleansiaAdminRoute,
  PermissionService,
  Policy,
  SnackbarService,
} from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import {
  Observable,
  Subscription,
  catchError,
  finalize,
  map,
  of,
  switchMap,
  takeUntil,
} from 'rxjs';
import { AdminWorkContractDialogComponent, AdminWorkContractDialogData } from './components';
import {
  INCIDENT_SUBJECT_LOOKUP_LIMIT,
  buildCrewEntries,
  resolveIncidentSubject,
} from './order-detail.models';

@Injectable()
export class OrderDetailFacade extends UnsubscribeControlDirective {
  private readonly adminClient = inject(AdminClient);
  private readonly gdprClient = inject(AdminGdprClient);
  private readonly customerAuditClient = inject(CustomerAuditClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);
  private readonly fileDownload = inject(FileDownloadService);

  private readonly router = inject(Router);
  private readonly permissions = inject(PermissionService);
  private readonly dialogService = inject(DialogService);
  private detailRequest?: Subscription;

  readonly order = signal<OrderItem | null>(null);
  readonly customer = signal<UserItem | null>(null);
  readonly customerLoading = signal(false);
  readonly customerError = signal(false);
  readonly detailError = signal(false);
  readonly customerId = computed(() => {
    const customer = this.customer();
    return customer && !customer.customerOfAnotherCompany ? customer.id : undefined;
  });
  readonly loading = signal<boolean>(false);
  readonly incidentFileExporting = signal<boolean>(false);
  readonly crew = computed(() => buildCrewEntries(this.order()));

  /**
   * Entry instructions are NOT on the order payload for an admin — the server withholds them and hands
   * them out only through a reveal, which is a command so the action is recorded against the admin who
   * asked. Null means "not revealed in this session"; it is never cached across a reload.
   * → /decisions/adr-0034
   */
  readonly revealedAccessInstructions = signal<string | null>(null);
  readonly revealingAccessInstructions = signal<boolean>(false);

  loadOrderDetail(orderId: string): void {
    this.detailRequest?.unsubscribe();
    this.order.set(null);
    this.customer.set(null);
    this.customerError.set(false);
    this.detailError.set(false);
    this.revealedAccessInstructions.set(null);
    this.customerLoading.set(false);
    this.loading.set(true);

    this.detailRequest = this.adminClient.adminOrderClient.details(orderId)
      .pipe(
        catchError((error: unknown) => {
          this.detailError.set(true);
          this.snackbarService.showApiError(error, 'pages.order_detail.load_error');
          return of(null);
        }),
        switchMap((order) => {
          this.order.set(order);
          this.loading.set(false);
          if (!order || !this.permissions.hasPolicy(Policy.CanViewOrderCustomer)) return of(null);
          this.customerLoading.set(true);
          return this.adminClient.adminOrderClient.customer(orderId).pipe(
            catchError((error: unknown) => {
              if (extractApiErrorCode(error) === 'order.not_found') return of(null);
              this.customerError.set(true);
              this.snackbarService.showApiError(error, 'pages.order_detail.customer_account_error');
              return of(null);
            })
          );
        }),
        takeUntil(this.destroyed$),
        finalize(() => {
          this.loading.set(false);
          this.customerLoading.set(false);
        })
      )
      .subscribe((customer) => this.customer.set(customer));
  }

  openCustomer(): void {
    const userId = this.customerId();
    if (userId) this.router.navigate([CleansiaAdminRoute.CUSTOMERS, userId]);
  }

  readWorkContract(acceptanceId: string): void {
    if (!acceptanceId) return;
    const data: AdminWorkContractDialogData = { acceptanceId };
    this.dialogService.open(AdminWorkContractDialogComponent, {
      data,
      width: '720px',
      modal: true,
      dismissableMask: true,
    });
  }

  revealAccessInstructions(orderId: string): void {
    if (this.revealingAccessInstructions()) return;
    this.revealingAccessInstructions.set(true);

    this.adminClient.accessInstructionsClient
      .reveal(orderId)
      .pipe(
        takeUntil(this.destroyed$),
        catchError(() => {
          this.snackbarService.showErrorTranslated(
            'pages.order_detail.access_instructions_reveal_failed'
          );
          return of(null);
        }),
        finalize(() => this.revealingAccessInstructions.set(false))
      )
      .subscribe((response) => {
        if (response) {
          this.revealedAccessInstructions.set(response.accessInstructions ?? '');
        }
      });
  }

  /**
   * The incident file for this order's customer, scoped to this order. The subject is read off the
   * order's trail (order-detail.models.ts says why); the server builds the PDF, decides whether the
   * order is theirs, and records the build.
   */
  exportIncidentFile(): void {
    const orderId = this.order()?.id;
    if (!orderId || this.incidentFileExporting()) return;
    this.incidentFileExporting.set(true);

    this.readIncidentSubject(orderId, 0)
      .pipe(
        switchMap((userId) => {
          if (!userId) {
            this.snackbarService.showErrorTranslated(
              'pages.order_detail.incident_file.no_subject'
            );
            return of(null);
          }
          return this.gdprClient
            .incidentFile(userId, orderId)
            .pipe(map((file: FileResponse) => ({ userId, file })));
        }),
        takeUntil(this.destroyed$),
        catchError((error: unknown) => {
          this.snackbarService.showApiError(
            error,
            'pages.order_detail.incident_file.error'
          );
          return of(null);
        }),
        finalize(() => this.incidentFileExporting.set(false))
      )
      .subscribe((result) => {
        if (result) {
          this.fileDownload.downloadBlob(
            result.file.data,
            result.file.fileName ?? incidentFileName(result.userId, new Date())
          );
          this.snackbarService.showSuccessTranslated(
            'pages.order_detail.incident_file.success'
          );
        }
      });
  }

  /**
   * The trail is newest-first and the customer's own rows are the oldest on an order that admins and
   * cleaners have worked on since, so the lookup walks page by page until a subject turns up or the
   * trail runs out.
   */
  private readIncidentSubject(
    orderId: string,
    offset: number
  ): Observable<string | null> {
    return this.customerAuditClient
      .timeline(
        undefined,
        AuditResourceType.Order,
        orderId,
        undefined,
        offset,
        INCIDENT_SUBJECT_LOOKUP_LIMIT
      )
      .pipe(
        switchMap((page) => {
          const entries = page.data ?? [];
          const subject = resolveIncidentSubject(entries);
          const read = offset + entries.length;
          if (subject || entries.length === 0 || read >= (page.total ?? 0)) {
            return of(subject);
          }
          return this.readIncidentSubject(orderId, read);
        })
      );
  }

  formatDate(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleDateString('en-GB');
  }

  formatDateTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleString('en-GB');
  }

  formatTime(date: string | Date | null | undefined): string {
    if (!date) return '-';
    const dateObj = date instanceof Date ? date : new Date(date);
    return dateObj.toLocaleTimeString('en-GB', {
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(price: number | null | undefined): string {
    if (price === null || price === undefined) return '-';
    return `${price.toFixed(2)} ${this.order()?.currency?.symbol ?? ''}`.trimEnd();
  }

  formatDuration(minutes: number | null | undefined): string {
    if (!minutes) return '-';
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours > 0) {
      return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
    }
    return `${mins}m`;
  }

  getOrderStatusClass(status: Code | undefined): string {
    if (!status) return 'order-status-badge status-pending';
    switch (status.value) {
      case OrderStatus.New:
        return 'order-status-badge status-new';
      case OrderStatus.Pending:
        return 'order-status-badge status-pending';
      case OrderStatus.Confirmed:
        return 'order-status-badge status-confirmed';
      case OrderStatus.OnTheWay:
        return 'order-status-badge status-ontheway';
      case OrderStatus.InProgress:
        return 'order-status-badge status-inprogress';
      case OrderStatus.Completed:
        return 'order-status-badge status-completed';
      case OrderStatus.Cancelled:
        return 'order-status-badge status-cancelled';
      default:
        return 'order-status-badge status-pending';
    }
  }

  getOrderStatusLabel(status: Code | undefined): string {
    if (!status?.name) return '';
    const key = `pages.order_management.order_status.${status.name
      .replace(/([A-Z])/g, '_$1')
      .toLowerCase()
      .replace(/^_/, '')}`;
    const translated = this.translate.instant(key);
    return translated === key ? status.name : translated;
  }

  getPaymentStatusClass(status: Code | undefined): string {
    if (!status) return 'payment-status-badge status-pending';
    switch (status.value) {
      case PaymentStatus.Pending:
        return 'payment-status-badge status-pending';
      case PaymentStatus.Paid:
        return 'payment-status-badge status-paid';
      case PaymentStatus.Failed:
        return 'payment-status-badge status-failed';
      case PaymentStatus.Refunded:
        return 'payment-status-badge status-refunded';
      case PaymentStatus.Disputed:
        return 'payment-status-badge status-disputed';
      default:
        return 'payment-status-badge status-pending';
    }
  }

  getOrderStatusIcon(status: Code | undefined): string {
    if (!status) return 'pi pi-circle';
    switch (status.value) {
      case OrderStatus.New:
        return 'pi pi-circle';
      case OrderStatus.Pending:
        return 'pi pi-clock';
      case OrderStatus.Confirmed:
        return 'pi pi-check';
      case OrderStatus.OnTheWay:
        return 'pi pi-car';
      case OrderStatus.InProgress:
        return 'pi pi-spinner';
      case OrderStatus.Completed:
        return 'pi pi-check-circle';
      case OrderStatus.Cancelled:
        return 'pi pi-times-circle';
      default:
        return 'pi pi-circle';
    }
  }

  getActiveExtras(): string[] {
    const extras = this.order()?.extras;
    if (!extras) return [];
    return Object.entries(extras)
      .filter(([, value]) => value)
      .map(([key]) => key);
  }
}
