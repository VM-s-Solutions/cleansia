import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import {
  loadCustomerServices,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { PackageListItem, ServiceListItem } from '@cleansia/partner-services';
import { Store } from '@ngrx/store';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'cleansia-services',
  templateUrl: './services.component.html',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, TranslateModule, ButtonModule],
})
export class ServicesComponent {
  private readonly store = inject(Store);
  private readonly translate = inject(TranslateService);

  services = toSignal(this.store.select(selectCustomerServices), {
    initialValue: [] as ServiceListItem[],
  });

  fallbackServices = [
    { name: 'pages.home.fallback_services.s1.name', desc: 'pages.home.fallback_services.s1.desc', price: 500, icon: 'pi-home' },
    { name: 'pages.home.fallback_services.s2.name', desc: 'pages.home.fallback_services.s2.desc', price: 800, icon: 'pi-sparkles', popular: true },
    { name: 'pages.home.fallback_services.s3.name', desc: 'pages.home.fallback_services.s3.desc', price: 300, icon: 'pi-sun' },
  ];

  extraServices = [
    { name: 'pages.home.extra_services.s1.name', icon: 'pi-car', price: 400 },
    { name: 'pages.home.extra_services.s2.name', icon: 'pi-building', price: 600 },
    { name: 'pages.home.extra_services.s3.name', icon: 'pi-briefcase', price: 350 },
  ];

  getTranslation(item: ServiceListItem | PackageListItem, field: string): string {
    const lang = this.translate.currentLang || this.translate.getDefaultLang();
    const translations = item.translations;
    if (translations && translations[lang]) {
      const translated = (translations[lang] as unknown as Record<string, string>)[field];
      if (translated) return translated;
    }
    return (item as unknown as Record<string, string>)[field] || '';
  }

  formatPrice(price: number | undefined): string {
    if (price == null) return '';
    return new Intl.NumberFormat('cs-CZ', {
      style: 'currency',
      currency: 'CZK',
      minimumFractionDigits: 0,
    }).format(price);
  }
}
