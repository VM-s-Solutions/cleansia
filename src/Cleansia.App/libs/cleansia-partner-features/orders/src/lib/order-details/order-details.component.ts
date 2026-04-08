import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '@cleansia/services';
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
import {
  formatCurrency,
  formatDate,
  formatDateTime,
  formatAddress,
  translateEnum,
  buildTranslatedOption,
  getStatusHistoryClass,
  getStatusHistoryIcon,
  canTakeOrder,
  canStartOrder,
  canCompleteOrder,
  canManagePhotos,
  canUploadPhotos,
  computeElapsedTime,
  buildCurrencyOptions,
  hasExtras,
  getExtrasEntries,
} from './order-details.helpers';

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
    DatePipe,
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

  private readonly currentLang = toSignal(
    this.translateService.onLangChange.pipe(
      map((event) => event.lang),
      startWith(this.translateService.currentLang)
    )
  );

  protected readonly statusOptions = computed(() => {
    this.currentLang();
    return buildTranslatedOption(this.translateService, 'order_status', this.orderDetails()?.orderStatus);
  });

  protected readonly paymentStatusOptions = computed(() => {
    this.currentLang();
    return buildTranslatedOption(this.translateService, 'payment_status', this.orderDetails()?.paymentStatus);
  });

  protected readonly paymentTypeOptions = computed(() => {
    this.currentLang();
    return buildTranslatedOption(this.translateService, 'payment_type', this.orderDetails()?.paymentType);
  });

  protected readonly orderStatusLabel = computed(() => {
    this.currentLang();
    return translateEnum(this.translateService, 'order_status', this.orderDetails()?.orderStatus?.name);
  });

  protected readonly paymentStatusLabel = computed(() => {
    this.currentLang();
    return translateEnum(this.translateService, 'payment_status', this.orderDetails()?.paymentStatus?.name);
  });

  protected readonly formattedCreatedOn = computed(() => formatDateTime(this.orderDetails()?.createdOn));

  protected readonly currencyOptions = computed(() => buildCurrencyOptions(this.orderDetails()?.currency));

  protected readonly hasExtras = computed(() => hasExtras(this.orderDetails()?.extras));

  protected readonly extrasEntries = computed(() => getExtrasEntries(this.orderDetails()?.extras));

  protected readonly hasNotes = computed(() => {
    const order = this.orderDetails();
    return order?.notes || order?.specialInstructions || order?.accessInstructions;
  });

  protected readonly hasStatusHistory = computed(() => {
    const history = this.orderDetails()?.statusHistory;
    return history && history.length > 0;
  });

  protected readonly canTakeOrder = computed(() => {
    const order = this.orderDetails();
    const eid = this.currentEmployeeId();
    if (!order || !eid) return false;
    return canTakeOrder(order.orderStatus.value, order.assignedEmployees, eid);
  });

  protected readonly canStartOrder = computed(() => {
    const order = this.orderDetails();
    const eid = this.currentEmployeeId();
    if (!order || !eid) return false;
    return canStartOrder(order.orderStatus.value, order.assignedEmployees, eid);
  });

  protected readonly canCompleteOrder = computed(() => {
    const order = this.orderDetails();
    const eid = this.currentEmployeeId();
    if (!order || !eid) return false;
    return canCompleteOrder(order.orderStatus.value, order.assignedEmployees, eid);
  });

  protected readonly hasInvoice = computed(() => !!this.orderDetails()?.receiptNumber);

  protected readonly isInProgress = computed(() => this.orderDetails()?.orderStatus.value === 3);

  protected readonly elapsedTime = computed(() => {
    const order = this.orderDetails();
    if (!order) return null;
    return computeElapsedTime(order.orderStatus.value, order.statusHistory);
  });

  protected readonly canManagePhotos = computed(() => {
    const order = this.orderDetails();
    const eid = this.currentEmployeeId();
    if (!order || !eid) return false;
    return canManagePhotos(order.orderStatus.value, order.assignedEmployees, eid);
  });

  protected readonly canUploadPhotos = computed((): boolean => {
    const order = this.orderDetails();
    const eid = this.currentEmployeeId();
    if (!order || !eid) return false;
    return canUploadPhotos(order.orderStatus.value, order.assignedEmployees, eid);
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

  protected formatCurrency = formatCurrency;
  protected formatDate = formatDate;
  protected formatDateTime = formatDateTime;

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

  protected getStatusHistoryClass(historyItem: { status: { value: number } }): string {
    return getStatusHistoryClass(historyItem.status?.value);
  }

  protected getStatusHistoryIcon(historyItem: { status: { value: number } }): string {
    return getStatusHistoryIcon(historyItem.status?.value);
  }

  protected getTranslatedStatusName(historyItem: { status: { name?: string } }): string {
    return translateEnum(this.translateService, 'order_status', historyItem.status?.name);
  }

  private createFormGroup(): FormGroup {
    return this.fb.group({
      orderStatus: [{ value: '', disabled: true }],
      paymentStatus: [{ value: '', disabled: true }],
      confirmationCode: [{ value: '', disabled: true }],
      customerName: [{ value: '', disabled: true }],
      customerEmail: [{ value: '', disabled: true }],
      customerPhone: [{ value: '', disabled: true }],
      address: [{ value: '', disabled: true }],
      cleaningDateTime: [{ value: '', disabled: true }],
      rooms: [{ value: '', disabled: true }],
      bathrooms: [{ value: '', disabled: true }],
      estimatedTime: [{ value: '', disabled: true }],
      paymentType: [{ value: '', disabled: true }],
      totalPrice: [{ value: '', disabled: true }],
      currency: [{ value: '', disabled: true }],
      assignedEmployeeName: [{ value: '', disabled: true }],
      assignedEmployeePhone: [{ value: '', disabled: true }],
      notes: [{ value: '', disabled: true }],
      specialInstructions: [{ value: '', disabled: true }],
      accessInstructions: [{ value: '', disabled: true }],
      createdOn: [{ value: '', disabled: true }],
      updatedOn: [{ value: '', disabled: true }],
    });
  }

  private updateFormWithOrderDetails(orderDetails: any): void {
    this.formGroup.patchValue({
      orderStatus: orderDetails.orderStatus.name,
      paymentStatus: orderDetails.paymentStatus.name,
      confirmationCode: orderDetails.confirmationCode,
      customerName: orderDetails.customerName,
      customerEmail: orderDetails.customerEmail,
      customerPhone: orderDetails.customerPhone,
      address: formatAddress(orderDetails.address),
      cleaningDateTime: formatDateTime(orderDetails.cleaningDateTime),
      rooms: orderDetails.rooms?.toString(),
      bathrooms: orderDetails.bathrooms?.toString(),
      estimatedTime: `${orderDetails.estimatedTime} minutes`,
      paymentType: orderDetails.paymentType.name,
      totalPrice: formatCurrency(orderDetails.totalPrice, orderDetails.currency.symbol),
      currency: `${orderDetails.currency.name} (${orderDetails.currency.code})`,
      assignedEmployeeName: orderDetails.assignedEmployeeName || '',
      assignedEmployeePhone: orderDetails.assignedEmployeePhone || '',
      notes: orderDetails.notes || '',
      specialInstructions: orderDetails.specialInstructions || '',
      accessInstructions: orderDetails.accessInstructions || '',
      createdOn: formatDateTime(orderDetails.createdOn),
      updatedOn: orderDetails.updatedOn ? formatDateTime(orderDetails.updatedOn) : '',
    });
  }
}
