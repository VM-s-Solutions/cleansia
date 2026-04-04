import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { CleansiaButtonComponent, CleansiaScrollTopComponent, CleansiaTelephoneComponent } from '@cleansia/components';
import { AddressDto, PackageListItem, PaymentType, ServiceListItem } from '@cleansia/partner-services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { OrderWizardFacade } from './order-wizard.facade';
import { RebookParams } from './order-wizard.models';

@Component({
  selector: 'cleansia-customer-order-wizard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslatePipe,
    InputTextModule,
    DatePickerModule,
    TextareaModule,
    SelectModule,
    DialogModule,
    CheckboxModule,
    CleansiaButtonComponent,
    CleansiaScrollTopComponent,
    CleansiaTelephoneComponent,
  ],
  templateUrl: './order-wizard.component.html',
  providers: [OrderWizardFacade],
})
export class OrderWizardComponent implements OnInit {
  protected readonly facade = inject(OrderWizardFacade);
  protected readonly translate = inject(TranslateService);
  private readonly route = inject(ActivatedRoute);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  protected readonly PaymentType = PaymentType;

  mobileBreakdownExpanded = signal(false);
  showRebookWarning = signal(false);
  unavailableItems = signal<string[]>([]);
  private pendingRebook = signal<RebookParams | null>(null);
  saveNewAddress = signal(false);
  touched = signal<Record<string, boolean>>({});

  markTouched(field: string): void {
    this.touched.update((t) => ({ ...t, [field]: true }));
  }

  isTouched(field: string): boolean {
    return !!this.touched()[field];
  }

  private readonly emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
  private readonly phoneRegex = /^[+]?[\d\s()-]{6,20}$/;
  private readonly zipRegex = /^[\d\s-]{3,20}$/;

  fieldError(field: string): string | null {
    if (!this.isTouched(field)) return null;
    const data = this.facade.formData();

    switch (field) {
      case 'customerFirstName':
        if (!data.customerFirstName) return this.translate.instant('global.validation.required');
        if (data.customerFirstName.length < 2) return this.translate.instant('global.validation.minlength', { min: 2 });
        if (data.customerFirstName.length > 50) return this.translate.instant('global.validation.maxlength', { max: 50 });
        return null;
      case 'customerLastName':
        if (!data.customerLastName) return this.translate.instant('global.validation.required');
        if (data.customerLastName.length < 2) return this.translate.instant('global.validation.minlength', { min: 2 });
        if (data.customerLastName.length > 50) return this.translate.instant('global.validation.maxlength', { max: 50 });
        return null;
      case 'customerEmail':
        if (!data.customerEmail) return this.translate.instant('global.validation.required');
        if (!this.emailRegex.test(data.customerEmail)) return this.translate.instant('global.validation.email');
        if (data.customerEmail.length > 50) return this.translate.instant('global.validation.maxlength', { max: 50 });
        return null;
      case 'customerPhone':
        if (!data.customerPhone) return this.translate.instant('global.validation.required');
        if (!this.phoneRegex.test(data.customerPhone.replace(/\s/g, ''))) return this.translate.instant('global.validation.phone');
        return null;
      case 'street':
        if (!data.address.street) return this.translate.instant('global.validation.required');
        if (data.address.street.length < 5) return this.translate.instant('global.validation.minlength', { min: 5 });
        if (data.address.street.length > 255) return this.translate.instant('global.validation.maxlength', { max: 255 });
        return null;
      case 'city':
        if (!data.address.city) return this.translate.instant('global.validation.required');
        if (data.address.city.length < 2) return this.translate.instant('global.validation.minlength', { min: 2 });
        if (data.address.city.length > 100) return this.translate.instant('global.validation.maxlength', { max: 100 });
        return null;
      case 'zipCode':
        if (!data.address.zipCode) return this.translate.instant('global.validation.required');
        if (!this.zipRegex.test(data.address.zipCode)) return this.translate.instant('global.validation.zip');
        return null;
      default:
        return null;
    }
  }

  allTimeOptions = this.generateTimeOptions();

  private todayHasSlots(): boolean {
    const now = new Date();
    const currentMinutes = now.getHours() * 60 + now.getMinutes();
    return this.allTimeOptions.some((opt) => {
      const [h, m] = opt.value.split(':').map(Number);
      return h * 60 + m > currentMinutes;
    });
  }

  minDate = computed(() => {
    const today = new Date();
    if (this.todayHasSlots()) return today;
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);
    return tomorrow;
  });

  timeOptions = computed(() => {
    const selectedDate = this.facade.formData().cleaningDate;
    if (!selectedDate) return this.allTimeOptions;

    const now = new Date();
    const isToday =
      selectedDate.getFullYear() === now.getFullYear() &&
      selectedDate.getMonth() === now.getMonth() &&
      selectedDate.getDate() === now.getDate();

    if (!isToday) return this.allTimeOptions;

    const currentMinutes = now.getHours() * 60 + now.getMinutes();
    return this.allTimeOptions.filter((opt) => {
      const [h, m] = opt.value.split(':').map(Number);
      return h * 60 + m > currentMinutes;
    });
  });

  selectedServices = computed(() => {
    const ids = this.facade.formData().selectedServiceIds;
    return this.facade.services().filter((s) => s.id && ids.includes(s.id));
  });

  selectedPackages = computed(() => {
    const ids = this.facade.formData().selectedPackageIds;
    return this.facade.packages().filter((p) => p.id && ids.includes(p.id));
  });

  private pendingServiceId = signal<string | null>(null);
  private pendingPackageId = signal<string | null>(null);

  private rebookEffect = effect(() => {
    const params = this.pendingRebook();
    if (!params) return;

    const services = this.facade.services();
    const packages = this.facade.packages();
    if (services.length === 0 && packages.length === 0) return;

    // Services/packages loaded — prefill now
    const missing = this.facade.prefillFromRebook(params);
    if (missing.length > 0) {
      this.unavailableItems.set(missing);
      this.showRebookWarning.set(true);
    }
    this.pendingRebook.set(null);
  });

  private preselectEffect = effect(() => {
    const serviceId = this.pendingServiceId();
    const packageId = this.pendingPackageId();
    if (!serviceId && !packageId) return;

    const services = this.facade.services();
    const packages = this.facade.packages();
    if (services.length === 0 && packages.length === 0) return;

    const update: Partial<import('./order-wizard.models').OrderWizardFormData> = {};
    if (serviceId && services.some((s) => s.id === serviceId)) {
      update.selectedServiceIds = [serviceId];
      this.pendingServiceId.set(null);
    }
    if (packageId && packages.some((p) => p.id === packageId)) {
      update.selectedPackageIds = [packageId];
      this.pendingPackageId.set(null);
    }
    if (Object.keys(update).length > 0) {
      this.facade.updateFormData(update);
    }
  });

  ngOnInit(): void {
    this.facade.initialize();

    // Pre-select service or package from query params (e.g., from services catalog)
    const serviceId = this.route.snapshot.queryParamMap.get('serviceId');
    const packageId = this.route.snapshot.queryParamMap.get('packageId');
    if (serviceId) {
      this.pendingServiceId.set(serviceId);
    }
    if (packageId) {
      this.pendingPackageId.set(packageId);
    }

    const rebook = this.route.snapshot.queryParamMap.get('rebook');
    if (rebook === 'true' && this.isBrowser) {
      const raw = sessionStorage.getItem('cleansia_rebook_data');
      if (raw) {
        sessionStorage.removeItem('cleansia_rebook_data');
        try {
          this.pendingRebook.set(JSON.parse(raw));
        } catch {
          // Invalid rebook data, ignore
        }
      }
    }
  }

  updateAddressField(field: string, value: string): void {
    const current = this.facade.formData().address;
    this.facade.updateFormData({
      address: new AddressDto({ ...current, [field]: value }),
    });
  }

  isServiceSelected(id: string): boolean {
    return this.facade.formData().selectedServiceIds.includes(id);
  }

  toggleService(id: string): void {
    const current = this.facade.formData().selectedServiceIds;
    const updated = current.includes(id)
      ? current.filter((s) => s !== id)
      : [...current, id];
    this.facade.updateFormData({ selectedServiceIds: updated });
  }

  isPackageSelected(id: string): boolean {
    return this.facade.formData().selectedPackageIds.includes(id);
  }

  togglePackage(id: string): void {
    const current = this.facade.formData().selectedPackageIds;
    const updated = current.includes(id)
      ? current.filter((p) => p !== id)
      : [...current, id];
    this.facade.updateFormData({ selectedPackageIds: updated });
  }

  getServiceById(id: string): ServiceListItem | undefined {
    return this.facade.services().find((s) => s.id === id);
  }

  getPackageById(id: string): PackageListItem | undefined {
    return this.facade.packages().find((p) => p.id === id);
  }

  getTranslation(item: ServiceListItem | PackageListItem, field: string): string {
    const lang = this.translate.currentLang || this.translate.getDefaultLang();
    const translations = item.translations;
    if (translations && translations[lang]) {
      const translated = (translations[lang] as unknown as Record<string, string>)[field];
      if (translated) return translated;
    }
    return (item as unknown as Record<string, string>)[field] || '';
  }

  formatPrice(price: number): string {
    return new Intl.NumberFormat('cs-CZ', {
      style: 'currency',
      currency: 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }

  onNextStep(): void {
    if (this.facade.activeStep() === 1 && this.saveNewAddress() && this.isCustomAddress()) {
      this.saveCurrentAddressToList();
    }
    this.facade.nextStep();
  }

  isDateSelected = computed(() => !!this.facade.formData().cleaningDate);

  isTimeSelected(value: string): boolean {
    return this.facade.formData().cleaningTime === value;
  }

  selectTime(value: string): void {
    this.facade.updateFormData({ cleaningTime: value });
  }

  onDateChange(date: Date | null): void {
    this.facade.updateFormData({ cleaningDate: date });

    // Reset time if currently selected time is no longer available
    const available = this.timeOptions();
    const currentTime = this.facade.formData().cleaningTime;
    if (available.length && !available.some((o) => o.value === currentTime)) {
      this.facade.updateFormData({ cleaningTime: available[0].value });
    }
  }

  hasValidTime = computed(() => {
    const available = this.timeOptions();
    const currentTime = this.facade.formData().cleaningTime;
    return available.some((o) => o.value === currentTime);
  });

  incrementRooms(): void {
    const current = this.facade.formData().rooms;
    this.facade.updateFormData({ rooms: current + 1 });
  }

  decrementRooms(): void {
    const current = this.facade.formData().rooms;
    if (current > 1) {
      this.facade.updateFormData({ rooms: current - 1 });
    }
  }

  incrementBathrooms(): void {
    const current = this.facade.formData().bathrooms;
    this.facade.updateFormData({ bathrooms: current + 1 });
  }

  decrementBathrooms(): void {
    const current = this.facade.formData().bathrooms;
    if (current > 1) {
      this.facade.updateFormData({ bathrooms: current - 1 });
    }
  }

  isAddressSelected(addr: { id: string; street: string; city: string; zip: string }): boolean {
    const current = this.facade.formData().address;
    return (
      current.street === addr.street &&
      current.city === addr.city &&
      current.zipCode === addr.zip
    );
  }

  isCustomAddress(): boolean {
    const saved = this.facade.savedAddresses();
    if (saved.length === 0) return true;
    return !saved.some((addr) => this.isAddressSelected(addr));
  }

  clearAddress(): void {
    this.saveNewAddress.set(false);
    this.facade.updateFormData({
      address: new AddressDto({
        street: '',
        city: '',
        zipCode: '',
        countryId: '',
        state: '',
      }),
    });
  }

  saveCurrentAddressToList(): void {
    const addr = this.facade.formData().address;
    if (!addr.street || !addr.city || !addr.zipCode) return;

    const newAddr = {
      id: crypto.randomUUID(),
      street: addr.street,
      city: addr.city,
      zip: addr.zipCode,
      country: addr.countryId || '',
      isDefault: this.facade.savedAddresses().length === 0,
    };

    const updated = [...this.facade.savedAddresses(), newAddr];
    this.facade.savedAddresses.set(updated);
    if (this.isBrowser) {
      try {
        localStorage.setItem('cleansia_saved_addresses', JSON.stringify(updated));
      } catch {
        // ignore
      }
    }
    this.saveNewAddress.set(false);
  }

  private generateTimeOptions(): { label: string; value: string }[] {
    const options = [];
    for (let h = 7; h <= 20; h++) {
      options.push({ label: `${h.toString().padStart(2, '0')}:00`, value: `${h.toString().padStart(2, '0')}:00` });
      if (h < 20) {
        options.push({ label: `${h.toString().padStart(2, '0')}:30`, value: `${h.toString().padStart(2, '0')}:30` });
      }
    }
    return options;
  }
}
