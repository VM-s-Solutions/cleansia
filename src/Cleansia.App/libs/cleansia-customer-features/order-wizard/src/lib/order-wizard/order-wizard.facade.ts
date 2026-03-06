import { inject, Injectable, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { CustomerAuthService, CustomerClient } from '@cleansia/customer-services';
import {
  loadCustomerPackages,
  loadCustomerServices,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import {
  AddressDto,
  CountryListItem,
  CreateOrderCommand,
  PackageListItem,
  PaymentType,
  ServiceListItem,
} from '@cleansia/partner-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ORDER_WIZARD_INITIAL_DATA, OrderWizardFormData } from './order-wizard.models';

@Injectable()
export class OrderWizardFacade {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  isAuthenticated = signal(false);

  services = toSignal(this.store.select(selectCustomerServices), { initialValue: [] as ServiceListItem[] });
  packages = toSignal(this.store.select(selectCustomerPackages), { initialValue: [] as PackageListItem[] });
  countries = signal<CountryListItem[]>([]);

  activeStep = signal(0);
  formData = signal<OrderWizardFormData>({ ...ORDER_WIZARD_INITIAL_DATA });
  submitting = signal(false);

  steps = [
    'pages.order.steps.services',
    'pages.order.steps.address',
    'pages.order.steps.datetime',
    'pages.order.steps.payment',
    'pages.order.steps.summary',
  ];

  totalPrice = computed(() => {
    const data = this.formData();
    let total = 0;
    const allServices = this.services();
    const allPackages = this.packages();

    for (const id of data.selectedServiceIds) {
      const svc = allServices.find((s) => s.id === id);
      if (svc) {
        total += svc.basePrice + svc.perRoomPrice * data.rooms;
      }
    }
    for (const id of data.selectedPackageIds) {
      const pkg = allPackages.find((p) => p.id === id);
      if (pkg) {
        total += pkg.price;
      }
    }
    return total;
  });

  initialize(): void {
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
    this.customerClient.countryClient.getOverview().subscribe({
      next: (countries) => this.countries.set(countries),
    });

    const loggedIn = this.authService.isLoggedIn();
    this.isAuthenticated.set(loggedIn);

    if (loggedIn) {
      this.customerClient.userClient.getCurrent().subscribe({
        next: (user) => {
          this.updateFormData({
            customerName: `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim(),
            customerEmail: user.email ?? '',
            customerPhone: user.phoneNumber ?? '',
          });
        },
      });
    }
  }

  updateFormData(partial: Partial<OrderWizardFormData>): void {
    this.formData.update((current) => ({ ...current, ...partial }));
  }

  nextStep(): void {
    if (this.activeStep() < this.steps.length - 1) {
      this.activeStep.update((s) => s + 1);
    }
  }

  prevStep(): void {
    if (this.activeStep() > 0) {
      this.activeStep.update((s) => s - 1);
    }
  }

  goToStep(step: number): void {
    if (step >= 0 && step < this.steps.length) {
      this.activeStep.set(step);
    }
  }

  canProceed(): boolean {
    const data = this.formData();
    switch (this.activeStep()) {
      case 0:
        return data.selectedServiceIds.length > 0 || data.selectedPackageIds.length > 0;
      case 1: {
        const addressValid = !!(data.address.street && data.address.city && data.address.zipCode);
        if (!this.isAuthenticated()) {
          const contactValid = !!(data.customerName && data.customerEmail && data.customerPhone);
          return addressValid && contactValid;
        }
        return addressValid;
      }
      case 2:
        return !!data.cleaningDate;
      case 3:
        return true;
      default:
        return true;
    }
  }

  submitOrder(): void {
    const data = this.formData();
    if (!data.cleaningDate) return;

    this.submitting.set(true);

    const cleaningDate = new Date(data.cleaningDate);
    const [hours, minutes] = data.cleaningTime.split(':').map(Number);
    cleaningDate.setHours(hours, minutes, 0, 0);

    const command = new CreateOrderCommand({
      customerName: data.customerName,
      customerEmail: data.customerEmail,
      customerPhone: data.customerPhone,
      customerAddress: new AddressDto(data.address),
      selectedServiceIds: data.selectedServiceIds,
      selectedPackageIds: data.selectedPackageIds,
      rooms: data.rooms,
      bathrooms: data.bathrooms,
      extras: data.extras,
      cleaningDate: cleaningDate,
      paymentType: data.paymentType,
      currencyId: undefined,
      totalPrice: this.totalPrice(),
      language: this.translate.currentLang || this.translate.getDefaultLang(),
    });

    if (data.paymentType === PaymentType.Card) {
      this.customerClient.paymentClient.createOrder(command).subscribe({
        next: (response) => {
          this.submitting.set(false);
          if (response.stripeSessionId) {
            window.location.href = response.stripeSessionId;
          } else {
            this.router.navigate([CleansiaCustomerRoute.CHECKOUT_SUCCESS]);
          }
        },
        error: () => {
          this.submitting.set(false);
          this.snackbarService.showError(
            this.translate.instant('pages.order.submit_error')
          );
        },
      });
    } else {
      this.customerClient.orderClient.createOrder(command).subscribe({
        next: () => {
          this.submitting.set(false);
          this.router.navigate([CleansiaCustomerRoute.CHECKOUT_SUCCESS]);
        },
        error: () => {
          this.submitting.set(false);
          this.snackbarService.showError(
            this.translate.instant('pages.order.submit_error')
          );
        },
      });
    }
  }
}
