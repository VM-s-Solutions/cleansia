import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaPartnerRoute } from '@cleansia/services';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTelephoneComponent,
  CleansiaTextInputComponent,
  CleansiaTitleComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { DialogService } from 'primeng/dynamicdialog';
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
    CommonModule,
    TranslatePipe,
    ReactiveFormsModule,
    OrderExtrasComponent,
    OrderHeaderComponent,
    OrderStatusComponent,
    OrderPackagesComponent,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    OrderPaymentInfoComponent,
    CleansiaTextInputComponent,
    CleansiaTelephoneComponent,
    OrderCustomerInfoComponent,
    OrderServiceDetailsComponent,
    OrderAdditionalServicesComponent,
    OrderPhotosComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './order-details.component.html',
  providers: [OrderDetailsFacade, DialogService],
})
export class OrderDetailsComponent implements OnInit {
  protected readonly facade = inject(OrderDetailsFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected formGroup: FormGroup = this.createFormGroup();

  protected readonly orderDetails = this.facade.orderDetails;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.error;
  protected readonly currentEmployeeId = this.facade.currentEmployeeId;

  protected readonly statusOptions = computed(() => [
    {
      label: this.orderDetails()?.orderStatus.name || '',
      value: this.orderDetails()?.orderStatus.name || '',
    },
  ]);
  protected readonly paymentStatusOptions = computed(() => [
    {
      label: this.orderDetails()?.paymentStatus.name || '',
      value: this.orderDetails()?.paymentStatus.name || '',
    },
  ]);

  protected readonly paymentTypeOptions = computed(() => [
    {
      label: this.orderDetails()?.paymentType.name || '',
      value: this.orderDetails()?.paymentType.name || '',
    },
  ]);

  protected readonly currencyOptions = computed(() => [
    {
      label: this.orderDetails()?.currency
        ? `${this.orderDetails()?.currency.name} (${
            this.orderDetails()?.currency.code
          })`
        : '',
      value: this.orderDetails()?.currency
        ? `${this.orderDetails()?.currency.name} (${
            this.orderDetails()?.currency.code
          })`
        : '',
    },
  ]);

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
    return `${amount.toLocaleString('cs-CZ', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })} ${currencySymbol}`;
  }

  protected formatDate(date: string | Date | undefined): string {
    if (!date) return '';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleDateString('cs-CZ');
  }

  protected formatDateTime(date: string | Date | undefined): string {
    if (!date) return '';
    const dateObj = typeof date === 'string' ? new Date(date) : date;
    return dateObj.toLocaleString('cs-CZ');
  }

  protected printOrder(): void {
    this.facade.printOrder();
  }

  protected retryLoadOrder(): void {
    const orderId = this.orderDetails()?.id;
    if (orderId) {
      this.facade.loadOrderDetails(orderId);
    }
  }

  protected downloadInvoice(): void {
    this.facade.downloadInvoice();
  }

  protected startOrder(): void {
    const orderId = this.orderDetails()?.id;
    const employeeId = this.currentEmployeeId();

    if (orderId && employeeId) {
      this.facade.startOrder(orderId, employeeId);
    }
  }

  protected completeOrder(): void {
    this.facade.openCompleteOrderDialog();
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
}
