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
import {
  RebookParams,
  TimeOption,
  filterTimeOptionsForToday,
  formatPrice,
  generateTimeOptions,
  getFieldError,
  getItemTranslation,
} from './order-wizard.models';
import { WizardSummaryStepComponent } from './components/wizard-summary-step.component';

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
    WizardSummaryStepComponent,
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

  fieldError(field: string): string | null {
    if (!this.isTouched(field)) return null;
    return getFieldError(field, this.facade.formData(), this.translate);
  }

  allTimeOptions: TimeOption[] = generateTimeOptions();

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

  timeOptions = computed(() =>
    filterTimeOptionsForToday(this.allTimeOptions, this.facade.formData().cleaningDate)
  );

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

    // Wait until both lists have loaded before attempting to match.
    // If the rebook references services, wait for services to load.
    // If it references packages, wait for packages to load.
    const needsServices = params.selectedServiceIds?.length > 0;
    const needsPackages = params.selectedPackageIds?.length > 0;
    if ((needsServices && services.length === 0) || (needsPackages && packages.length === 0)) return;

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
    return getItemTranslation(item, field, this.translate);
  }

  formatPrice(price: number): string {
    return formatPrice(price);
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

}
