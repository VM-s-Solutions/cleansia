import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, computed, effect } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  CleansiaButtonComponent,
  CleansiaLanguageSwitcherComponent,
  CleansiaLoaderComponent,
  CleansiaSectionComponent,
  CleansiaTitleComponent,
  CleansiaTextInputComponent,
  CleansiaTelephoneComponent,
  CleansiaCalendarComponent,
  CleansiaSelectComponent,
} from '@cleansia/components';
import { TranslatePipe } from '@ngx-translate/core';
import { OrderDetailsFacade } from './order-details.facade';

@Component({
  selector: 'cleansia-partner-order-details',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslatePipe,
    CleansiaTitleComponent,
    CleansiaButtonComponent,
    CleansiaSelectComponent,
    CleansiaLoaderComponent,
    CleansiaSectionComponent,
    CleansiaCalendarComponent,
    CleansiaTextInputComponent,
    CleansiaTelephoneComponent,
    CleansiaLanguageSwitcherComponent,
  ],
  templateUrl: './order-details.component.html',
  styleUrls: ['./order-details.component.scss'],
  providers: [OrderDetailsFacade],
})
export class OrderDetailsComponent implements OnInit {
  protected readonly facade = inject(OrderDetailsFacade);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  protected formGroup: FormGroup = this.createFormGroup();

  // Computed properties for better performance
  protected readonly orderDetails = this.facade.orderDetails;
  protected readonly loading = this.facade.loading;
  protected readonly error = this.facade.error;

  // Status and payment type options for selects
  protected readonly statusOptions = computed(() => [
    { label: this.orderDetails()?.orderStatus.name || '', value: this.orderDetails()?.orderStatus.name || '' },
  ]);

  protected readonly paymentStatusOptions = computed(() => [
    { label: this.orderDetails()?.paymentStatus.name || '', value: this.orderDetails()?.paymentStatus.name || '' },
  ]);

  protected readonly paymentTypeOptions = computed(() => [
    { label: this.orderDetails()?.paymentType.name || '', value: this.orderDetails()?.paymentType.name || '' },
  ]);

  protected readonly currencyOptions = computed(() => [
    {
      label: this.orderDetails()?.currency
        ? `${this.orderDetails()?.currency.name} (${this.orderDetails()?.currency.code})`
        : '',
      value: this.orderDetails()?.currency
        ? `${this.orderDetails()?.currency.name} (${this.orderDetails()?.currency.code})`
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
    return order?.notes || order?.specialInstructions || order?.accessInstructions;
  });

  protected readonly hasStatusHistory = computed(() => {
    return this.orderDetails()?.statusHistory && this.orderDetails()!.statusHistory!.length > 0;
  });

  constructor() {
    // Update form when order details change using effect
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
    } else {
      this.navigateToOrders();
    }
  }

  protected navigateToOrders(): void {
    this.router.navigate(['/orders']);
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

      // Package Information
      packageName: [{ value: '', disabled: true }],
      packagePrice: [{ value: '', disabled: true }],
      packageDescription: [{ value: '', disabled: true }],

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
      estimatedTime: `${orderDetails.estimatedTime} ${this.getMinutesTranslation()}`,
      packageName: orderDetails.selectedPackage?.name || '',
      packagePrice: orderDetails.selectedPackage ? this.formatCurrency(orderDetails.selectedPackage.price, orderDetails.currency.symbol) : '',
      packageDescription: orderDetails.selectedPackage?.description || '',
      paymentType: orderDetails.paymentType.name,
      totalPrice: this.formatCurrency(orderDetails.totalPrice, orderDetails.currency.symbol),
      currency: `${orderDetails.currency.name} (${orderDetails.currency.code})`,
      assignedEmployeeName: orderDetails.assignedEmployeeName || '',
      assignedEmployeePhone: orderDetails.assignedEmployeePhone || '',
      notes: orderDetails.notes || '',
      specialInstructions: orderDetails.specialInstructions || '',
      accessInstructions: orderDetails.accessInstructions || '',
      createdOn: this.formatDateTime(orderDetails.createdOn),
      updatedOn: orderDetails.updatedOn ? this.formatDateTime(orderDetails.updatedOn) : '',
    });
  }

  private getMinutesTranslation(): string {
    // This would typically use TranslateService, but for now return a static string
    return 'minutes';
  }
}
