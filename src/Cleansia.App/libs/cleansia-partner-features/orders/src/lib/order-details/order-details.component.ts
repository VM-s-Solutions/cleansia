import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '@cleansia/services';
import { toSnakeCase } from '@cleansia/utils';
import {
  CleansiaButtonComponent,
  CleansiaDetailSkeletonComponent,
  CleansiaSectionComponent,
  CleansiaTextInputComponent,
} from '@cleansia/components';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
import { map, startWith } from 'rxjs';
import { OrderAdditionalServicesComponent } from './components/order-additional-services.component';
import { OrderCustomerInfoComponent } from './components/order-customer-info.component';
import { OrderExtrasComponent } from './components/order-extras.component';
import { OrderHeaderComponent } from './components/order-header.component';
import { OrderPackagesComponent } from './components/order-packages.component';
import { OrderPaymentInfoComponent } from './components/order-payment-info.component';
import { OrderPhotosComponent } from './components/order-photos.component';
import { OrderServiceDetailsComponent } from './components/order-service-details.component';
import { OrderStatusComponent } from './components/order-status.component';
import { OrderDetailsFacade } from './order-details.facade';

@Component({
  selector: 'cleansia-partner-order-details',
  standalone: true,
  imports: [
    TranslatePipe,
    ReactiveFormsModule,
    OrderExtrasComponent,
    OrderHeaderComponent,
    OrderStatusComponent,
    OrderPackagesComponent,
    CleansiaButtonComponent,
    CleansiaDetailSkeletonComponent,
    CleansiaSectionComponent,
    OrderPaymentInfoComponent,
    CleansiaTextInputComponent,
    OrderCustomerInfoComponent,
    OrderServiceDetailsComponent,
    OrderAdditionalServicesComponent,
    OrderPhotosComponent,
  ],
  templateUrl: './order-details.component.html',
  providers: [OrderDetailsFacade, DialogService],
})
export class OrderDetailsComponent implements OnInit {
  protected readonly facade = inject(OrderDetailsFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly translateService = inject(TranslateService);

  protected formGroup: FormGroup = this.createFormGroup();

  protected readonly orderDetails = this.facade.orderDetails;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.error;
  protected readonly currentEmployeeId = this.facade.currentEmployeeId;

  // Track language changes to make translations reactive
  private readonly currentLang = toSignal(
    this.translateService.onLangChange.pipe(
      map((event) => event.lang),
      startWith(this.translateService.currentLang)
    )
  );

  protected readonly statusOptions = computed(() => {
    // Access currentLang to trigger recomputation on language change
    this.currentLang();
    const status = this.orderDetails()?.orderStatus;
    if (!status?.name) return [];
    const translationKey = `enums.order_status.${toSnakeCase(status.name)}`;
    const translatedLabel = this.translateService.instant(translationKey);
    return [
      {
        label: translatedLabel !== translationKey ? translatedLabel : status.name,
        value: status.name,
      },
    ];
  });

  protected readonly paymentStatusOptions = computed(() => {
    // Access currentLang to trigger recomputation on language change
    this.currentLang();
    const status = this.orderDetails()?.paymentStatus;
    if (!status?.name) return [];
    const translationKey = `enums.payment_status.${toSnakeCase(status.name)}`;
    const translatedLabel = this.translateService.instant(translationKey);
    return [
      {
        label: translatedLabel !== translationKey ? translatedLabel : status.name,
        value: status.name,
      },
    ];
  });

  protected readonly paymentTypeOptions = computed(() => {
    // Access currentLang to trigger recomputation on language change
    this.currentLang();
    const paymentType = this.orderDetails()?.paymentType;
    if (!paymentType?.name) return [];
    const translationKey = `enums.payment_type.${toSnakeCase(paymentType.name)}`;
    const translatedLabel = this.translateService.instant(translationKey);
    return [
      {
        label: translatedLabel !== translationKey ? translatedLabel : paymentType.name,
        value: paymentType.name,
      },
    ];
  });

  // Translated status labels for the status banner
  protected readonly orderStatusLabel = computed(() => {
    this.currentLang();
    const status = this.orderDetails()?.orderStatus;
    if (!status?.name) return '';
    const translationKey = `enums.order_status.${toSnakeCase(status.name)}`;
    const translatedLabel = this.translateService.instant(translationKey);
    return translatedLabel !== translationKey ? translatedLabel : status.name;
  });

  protected readonly paymentStatusLabel = computed(() => {
    this.currentLang();
    const status = this.orderDetails()?.paymentStatus;
    if (!status?.name) return '';
    const translationKey = `enums.payment_status.${toSnakeCase(status.name)}`;
    const translatedLabel = this.translateService.instant(translationKey);
    return translatedLabel !== translationKey ? translatedLabel : status.name;
  });

  protected readonly formattedCreatedOn = computed(() => {
    const createdOn = this.orderDetails()?.createdOn;
    return this.formatDateTime(createdOn);
  });

  protected readonly currencyOptions = computed(() => {
    const currency = this.orderDetails()?.currency;
    if (!currency) return [];
    return [
      {
        label: `${currency.name} (${currency.code})`,
        value: `${currency.name} (${currency.code})`,
      },
    ];
  });

  protected readonly hasExtras = computed(() => {
    const extras = this.orderDetails()?.extras;
    return extras && Object.entries(extras).some(([_, value]) => value);
  });

  protected readonly extrasEntries = computed(() => {
    const extras = this.orderDetails()?.extras;
    return extras ? Object.entries(extras).filter(([_, value]) => value) : [];
  });

  protected readonly hasNotes = computed(() => {
    const order = this.orderDetails();
    return (
      order?.notes || order?.specialInstructions || order?.accessInstructions
    );
  });

  protected readonly hasStatusHistory = computed(() => {
    return (
      this.orderDetails()?.statusHistory &&
      this.orderDetails()!.statusHistory!.length > 0
    );
  });

  protected readonly canTakeOrder = computed(() => {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) return false;

    // Employee must not already be assigned
    const isNotAssigned = !order.assignedEmployees?.some(
      (e) => e.employeeId === employeeId
    );
    // Only for Pending (1) or Confirmed (2) orders
    const isPendingOrConfirmed =
      order.orderStatus.value === 1 || order.orderStatus.value === 2;

    return isPendingOrConfirmed && isNotAssigned;
  });

  protected readonly canStartOrder = computed(() => {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) return false;

    // OrderStatus.Confirmed = 2
    const isConfirmed = order.orderStatus.value === 2;
    const isAssigned = order.assignedEmployees?.some(
      (e) => e.employeeId === employeeId
    );

    return isConfirmed && isAssigned;
  });

  protected readonly canCompleteOrder = computed(() => {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) return false;

    // OrderStatus.InProgress = 3
    const isInProgress = order.orderStatus.value === 3;
    const isAssigned = order.assignedEmployees?.some(
      (e) => e.employeeId === employeeId
    );

    return isInProgress && isAssigned;
  });

  protected readonly hasInvoice = computed(() => {
    const order = this.orderDetails();
    return !!order?.receiptNumber;
  });

  protected readonly isInProgress = computed(() => {
    return this.orderDetails()?.orderStatus.value === 3;
  });

  protected readonly elapsedTime = computed(() => {
    const order = this.orderDetails();
    if (!order || order.orderStatus.value !== 3) return null;
    const startEntry = order.statusHistory?.find(h => h.status.value === 3);
    if (!startEntry) return null;
    const start = new Date(startEntry.createdOn);
    const elapsed = Math.floor((Date.now() - start.getTime()) / 60000);
    return { hours: Math.floor(elapsed / 60), minutes: elapsed % 60 };
  });

  protected readonly canManagePhotos = computed(() => {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) return false;

    // OrderStatus.InProgress = 3, OrderStatus.Completed = 4
    // Can view photos when InProgress or Completed
    const isInProgressOrCompleted =
      order.orderStatus.value === 3 || order.orderStatus.value === 4;
    const isAssigned = order.assignedEmployees?.some(
      (e) => e.employeeId === employeeId
    );

    return isInProgressOrCompleted && isAssigned;
  });

  protected readonly canUploadPhotos = computed((): boolean => {
    const order = this.orderDetails();
    const employeeId = this.currentEmployeeId();

    if (!order || !employeeId) return false;

    // OrderStatus.InProgress = 3
    // Can only upload photos when order is InProgress (not when Completed)
    const isInProgress = order.orderStatus.value === 3;
    const isAssigned = order.assignedEmployees?.some(
      (e) => e.employeeId === employeeId
    ) ?? false;

    return isInProgress && isAssigned;
  });

  constructor() {
    effect(() => {
      const orderDetails = this.orderDetails();
      if (orderDetails) {
        this.updateFormWithOrderDetails(orderDetails);
      }
    });
  }

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      this.facade.loadOrderDetails(orderId);
      this.facade.loadCurrentEmployee();
    } else {
      this.navigateToOrders();
    }
  }

  protected navigateToOrders(): void {
    this.router.navigate([CleansiaPartnerRoute.ORDERS]);
  }

  protected formatCurrency(amount: number, currencySymbol: string): string {
    return `${amount.toLocaleString('en-GB', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })} ${currencySymbol}`;
  }

  protected formatDate(date: string | Date | undefined): string {
    if (!date) return '';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    const day = dateObj.getDate().toString().padStart(2, '0');
    const month = (dateObj.getMonth() + 1).toString().padStart(2, '0');
    const year = dateObj.getFullYear();
    return `${day}.${month}.${year}`;
  }

  protected formatDateTime(date: string | Date | undefined): string {
    if (!date) return '';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    const day = dateObj.getDate().toString().padStart(2, '0');
    const month = (dateObj.getMonth() + 1).toString().padStart(2, '0');
    const year = dateObj.getFullYear();
    const hours = dateObj.getHours().toString().padStart(2, '0');
    const minutes = dateObj.getMinutes().toString().padStart(2, '0');
    return `${day}.${month}.${year} ${hours}:${minutes}`;
  }

  protected printOrder(): void {
    this.facade.printOrder();
  }

  protected retryLoadOrder(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      this.facade.loadOrderDetails(orderId);
    }
  }

  protected downloadInvoice(): void {
    this.facade.downloadInvoice();
  }

  protected takeOrder(): void {
    const orderId = this.orderDetails()?.id;
    const employeeId = this.currentEmployeeId();

    if (orderId && employeeId) {
      this.facade.takeOrder(orderId, employeeId);
    }
  }

  protected startOrder(): void {
    const orderId = this.orderDetails()?.id;
    const employeeId = this.currentEmployeeId();

    if (orderId && employeeId) {
      this.facade.startOrder(orderId, employeeId);
    }
  }

  protected completeOrder(): void {
    this.facade.completeOrder();
  }

  protected openReportIssue(): void {
    this.facade.openReportIssueDialog();
  }

  protected openAddNote(): void {
    this.facade.openAddNoteDialog();
  }

  private createFormGroup(): FormGroup {
    return this.fb.group({
      // Order Status
      orderStatus: [{ value: '', disabled: true }],
      paymentStatus: [{ value: '', disabled: true }],
      confirmationCode: [{ value: '', disabled: true }],

      // Customer Information
      customerName: [{ value: '', disabled: true }],
      customerEmail: [{ value: '', disabled: true }],
      customerPhone: [{ value: '', disabled: true }],
      address: [{ value: '', disabled: true }],

      // Service Details
      cleaningDateTime: [{ value: '', disabled: true }],
      rooms: [{ value: '', disabled: true }],
      bathrooms: [{ value: '', disabled: true }],
      estimatedTime: [{ value: '', disabled: true }],

      // Payment Information
      paymentType: [{ value: '', disabled: true }],
      totalPrice: [{ value: '', disabled: true }],
      currency: [{ value: '', disabled: true }],

      // Employee Assignment
      assignedEmployeeName: [{ value: '', disabled: true }],
      assignedEmployeePhone: [{ value: '', disabled: true }],

      // Notes
      notes: [{ value: '', disabled: true }],
      specialInstructions: [{ value: '', disabled: true }],
      accessInstructions: [{ value: '', disabled: true }],

      // Audit Information
      createdOn: [{ value: '', disabled: true }],
      updatedOn: [{ value: '', disabled: true }],
    });
  }

  private updateFormWithOrderDetails(orderDetails: any): void {
    const addressString = orderDetails.address
      ? `${orderDetails.address.street}, ${orderDetails.address.city}, ${orderDetails.address.zipCode}, ${orderDetails.address.country}`
      : '';

    this.formGroup.patchValue({
      orderStatus: orderDetails.orderStatus.name,
      paymentStatus: orderDetails.paymentStatus.name,
      confirmationCode: orderDetails.confirmationCode,
      customerName: orderDetails.customerName,
      customerEmail: orderDetails.customerEmail,
      customerPhone: orderDetails.customerPhone,
      address: addressString,
      cleaningDateTime: this.formatDateTime(orderDetails.cleaningDateTime),
      rooms: orderDetails.rooms?.toString(),
      bathrooms: orderDetails.bathrooms?.toString(),
      estimatedTime: `${
        orderDetails.estimatedTime
      } ${this.getMinutesTranslation()}`,
      paymentType: orderDetails.paymentType.name,
      totalPrice: this.formatCurrency(
        orderDetails.totalPrice,
        orderDetails.currency.symbol
      ),
      currency: `${orderDetails.currency.name} (${orderDetails.currency.code})`,
      assignedEmployeeName: orderDetails.assignedEmployeeName || '',
      assignedEmployeePhone: orderDetails.assignedEmployeePhone || '',
      notes: orderDetails.notes || '',
      specialInstructions: orderDetails.specialInstructions || '',
      accessInstructions: orderDetails.accessInstructions || '',
      createdOn: this.formatDateTime(orderDetails.createdOn),
      updatedOn: orderDetails.updatedOn
        ? this.formatDateTime(orderDetails.updatedOn)
        : '',
    });
  }

  private getMinutesTranslation(): string {
    // This would typically use TranslateService, but for now return a static string
    return 'minutes';
  }

  // Status History helper methods
  protected getStatusHistoryClass(historyItem: { status: { value: number } }): string {
    if (!historyItem.status) return 'status-history-item status-pending';
    switch (historyItem.status.value) {
      case 1: // Pending
        return 'status-history-item status-pending';
      case 2: // Confirmed
        return 'status-history-item status-confirmed';
      case 3: // InProgress
        return 'status-history-item status-inprogress';
      case 4: // Completed
        return 'status-history-item status-completed';
      case 5: // Cancelled
        return 'status-history-item status-cancelled';
      default:
        return 'status-history-item status-pending';
    }
  }

  protected getStatusHistoryIcon(historyItem: { status: { value: number } }): string {
    if (!historyItem.status) return 'pi pi-circle';
    switch (historyItem.status.value) {
      case 1: // Pending
        return 'pi pi-clock';
      case 2: // Confirmed
        return 'pi pi-check';
      case 3: // InProgress
        return 'pi pi-spinner';
      case 4: // Completed
        return 'pi pi-check-circle';
      case 5: // Cancelled
        return 'pi pi-times-circle';
      default:
        return 'pi pi-circle';
    }
  }

  protected getTranslatedStatusName(historyItem: { status: { name?: string } }): string {
    if (!historyItem.status?.name) return '';
    const translationKey = `enums.order_status.${toSnakeCase(historyItem.status.name)}`;
    const translatedLabel = this.translateService.instant(translationKey);
    return translatedLabel !== translationKey ? translatedLabel : historyItem.status.name;
  }
}
