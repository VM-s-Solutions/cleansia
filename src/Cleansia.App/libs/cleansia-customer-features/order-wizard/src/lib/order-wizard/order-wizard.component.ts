import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { AddressDto, PackageListItem, PaymentType, ServiceListItem } from '@cleansia/partner-services';
import { Store } from '@ngrx/store';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MenuItem } from 'primeng/api';
import { StepsModule } from 'primeng/steps';
import { CheckboxModule } from 'primeng/checkbox';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { RadioButtonModule } from 'primeng/radiobutton';
import { TextareaModule } from 'primeng/textarea';
import { CardModule } from 'primeng/card';
import { OrderWizardFacade } from './order-wizard.facade';

@Component({
  selector: 'cleansia-customer-order-wizard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslateModule,
    StepsModule,
    CheckboxModule,
    InputNumberModule,
    InputTextModule,
    DatePickerModule,
    SelectModule,
    RadioButtonModule,
    TextareaModule,
    CardModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './order-wizard.component.html',
  providers: [OrderWizardFacade],
})
export class OrderWizardComponent implements OnInit {
  protected readonly facade = inject(OrderWizardFacade);
  protected readonly translate = inject(TranslateService);
  protected readonly PaymentType = PaymentType;

  minDate = new Date();
  timeOptions = this.generateTimeOptions();

  stepsMenuItems = computed<MenuItem[]>(() =>
    this.facade.steps.map((key) => ({ label: this.translate.instant(key) }))
  );

  ngOnInit(): void {
    this.facade.initialize();
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
