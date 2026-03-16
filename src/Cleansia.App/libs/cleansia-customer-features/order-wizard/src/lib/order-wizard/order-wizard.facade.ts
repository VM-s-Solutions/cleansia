import { inject, Injectable, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import {
  CustomerAuthService,
  CustomerClient,
} from '@cleansia/customer-services';
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
import {
  ORDER_WIZARD_INITIAL_DATA,
  OrderWizardFormData,
  RebookParams,
} from './order-wizard.models';

@Injectable()
export class OrderWizardFacade {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly customerClient = inject(CustomerClient);
  private readonly authService = inject(CustomerAuthService);
  private readonly translate = inject(TranslateService);
  private readonly snackbarService = inject(SnackbarService);

  isAuthenticated = signal(false);

  services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });
  packages = toSignal(this.store.select(selectCustomerPackages), {
    initialValue: [] as PackageListItem[],
  });
  countries = signal<CountryListItem[]>([]);
  savedAddresses = signal<{ id: string; street: string; city: string; zip: string; country: string; isDefault: boolean }[]>([]);

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

  stepIcons = [
    'pi pi-list',
    'pi pi-map-marker',
    'pi pi-calendar',
    'pi pi-credit-card',
    'pi pi-check-circle',
  ];

  totalPrice = computed(() => {
    const data = this.formData();
    let total = 0;
    const allServices = this.services();
    const allPackages = this.packages();

    for (const id of data.selectedServiceIds) {
      const svc = allServices.find((s) => s.id === id);
      if (svc) {
        total += svc.basePrice + svc.perRoomPrice * (data.rooms + data.bathrooms);
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
      next: (countries) => {
        this.countries.set(countries);
        // Auto-select first country if address has no country set
        if (countries.length > 0 && !this.formData().address.countryId) {
          this.updateFormData({
            address: new AddressDto({
              ...this.formData().address,
              countryId: countries[0].id ?? '',
            }),
          });
        }
      },
    });

    const loggedIn = this.authService.isLoggedIn();
    this.isAuthenticated.set(loggedIn);

    // Load saved addresses from localStorage
    try {
      const stored = localStorage.getItem('cleansia_saved_addresses');
      if (stored) {
        this.savedAddresses.set(JSON.parse(stored));
      }
    } catch {
      // ignore
    }

    if (loggedIn) {
      this.customerClient.userClient.getCurrent().subscribe({
        next: (user) => {
          this.updateFormData({
            customerFirstName: user.firstName ?? '',
            customerLastName: user.lastName ?? '',
            customerEmail: user.email ?? '',
            customerPhone: user.phoneNumber ?? '',
          });

          // Prefill with default saved address if address is still empty
          const currentAddr = this.formData().address;
          if (!currentAddr.street && !currentAddr.city) {
            const defaultAddr = this.savedAddresses().find(a => a.isDefault);
            if (defaultAddr) {
              this.selectSavedAddress(defaultAddr.id);
            }
          }
        },
      });
    }
  }

  selectSavedAddress(addressId: string): void {
    const addr = this.savedAddresses().find(a => a.id === addressId);
    if (addr) {
      this.updateFormData({
        address: new AddressDto({
          street: addr.street,
          city: addr.city,
          zipCode: addr.zip,
          countryId: addr.country || '',
          state: '',
        }),
      });
    }
  }

  updateFormData(partial: Partial<OrderWizardFormData>): void {
    this.formData.update((current) => ({ ...current, ...partial }));
  }

  prefillFromRebook(params: RebookParams): string[] {
    const availableServices = this.services();
    const availablePackages = this.packages();

    const availableServiceIds = availableServices.map((s) => s.id);
    const availablePackageIds = availablePackages.map((p) => p.id);

    const validServiceIds = params.selectedServiceIds.filter((id) =>
      availableServiceIds.includes(id)
    );
    const validPackageIds = params.selectedPackageIds.filter((id) =>
      availablePackageIds.includes(id)
    );

    const unavailableItems: string[] = [];
    params.selectedServiceIds.forEach((id, i) => {
      if (!availableServiceIds.includes(id)) {
        unavailableItems.push(params.selectedServiceNames[i] || id);
      }
    });
    params.selectedPackageIds.forEach((id, i) => {
      if (!availablePackageIds.includes(id)) {
        unavailableItems.push(params.selectedPackageNames[i] || id);
      }
    });

    const update: Partial<OrderWizardFormData> = {
      selectedServiceIds: validServiceIds,
      selectedPackageIds: validPackageIds,
      rooms: params.rooms,
      bathrooms: params.bathrooms,
    };

    if (params.address) {
      update.address = new AddressDto(params.address);
    }

    this.updateFormData(update);

    return unavailableItems;
  }

  nextStep(): void {
    if (this.activeStep() < this.steps.length - 1) {
      this.activeStep.update((s) => s + 1);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  prevStep(): void {
    if (this.activeStep() > 0) {
      this.activeStep.update((s) => s - 1);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  goToStep(step: number): void {
    if (step >= 0 && step < this.steps.length) {
      this.activeStep.set(step);
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  private readonly emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  private readonly phoneRegex = /^[+]?[\d\s()-]{6,20}$/;
  private readonly zipRegex = /^[\d\s-]{3,20}$/;

  isSavedAddressSelected(): boolean {
    const saved = this.savedAddresses();
    if (saved.length === 0) return false;
    const current = this.formData().address;
    return saved.some(
      (a) =>
        a.street === current.street &&
        a.city === current.city &&
        a.zip === current.zipCode
    );
  }

  canProceed(): boolean {
    const data = this.formData();
    switch (this.activeStep()) {
      case 0:
        return (
          data.selectedServiceIds.length > 0 ||
          data.selectedPackageIds.length > 0
        );
      case 1: {
        // Phone is always required
        const phoneValid = !!(data.customerPhone && this.phoneRegex.test(data.customerPhone.replace(/\s/g, '')));

        // If authenticated user selected a saved address, only require non-empty fields
        const usingSaved = this.isAuthenticated() && this.isSavedAddressSelected();
        const addressValid = usingSaved
          ? !!(data.address.street && data.address.city && data.address.zipCode)
          : !!(
              data.address.street &&
              data.address.street.length >= 5 &&
              data.address.street.length <= 255 &&
              data.address.city &&
              data.address.city.length >= 2 &&
              data.address.city.length <= 100 &&
              data.address.zipCode &&
              this.zipRegex.test(data.address.zipCode)
            );
        const contactValid = !!(
          data.customerFirstName &&
          data.customerFirstName.length >= 2 &&
          data.customerFirstName.length <= 50 &&
          data.customerLastName &&
          data.customerLastName.length >= 2 &&
          data.customerLastName.length <= 50 &&
          data.customerEmail &&
          this.emailRegex.test(data.customerEmail) &&
          data.customerEmail.length <= 50
        );
        return addressValid && contactValid && phoneValid;
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

    const selectedDate = new Date(data.cleaningDate);
    const [hours, minutes] = data.cleaningTime.split(':').map(Number);
    const cleaningDate = new Date(Date.UTC(
      selectedDate.getFullYear(),
      selectedDate.getMonth(),
      selectedDate.getDate(),
      hours, minutes, 0, 0
    ));

    const command = new CreateOrderCommand({
      customerName: `${data.customerFirstName} ${data.customerLastName}`.trim(),
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
