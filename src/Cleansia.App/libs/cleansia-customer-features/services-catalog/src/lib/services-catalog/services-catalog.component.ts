import { CommonModule } from '@angular/common';
import { Component, inject, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import {
  loadCustomerPackages,
  loadCustomerServices,
  selectCustomerCatalogLoading,
  selectCustomerPackages,
  selectCustomerServices,
} from '@cleansia/customer-stores';
import { PackageListItem, ServiceListItem } from '@cleansia/partner-services';
import { CleansiaCustomerRoute } from '@cleansia/services';
import { Store } from '@ngrx/store';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Skeleton } from 'primeng/skeleton';
import { CardModule } from 'primeng/card';

@Component({
  selector: 'cleansia-customer-services-catalog',
  standalone: true,
  imports: [
    CommonModule,
    TranslateModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
    Skeleton,
    CardModule,
  ],
  templateUrl: './services-catalog.component.html',
})
export class ServicesCatalogComponent implements OnInit {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  services = toSignal(this.store.select(selectCustomerServices), { initialValue: [] });
  packages = toSignal(this.store.select(selectCustomerPackages), { initialValue: [] });
  loading = toSignal(this.store.select(selectCustomerCatalogLoading), { initialValue: false });

  ngOnInit(): void {
    this.store.dispatch(loadCustomerServices());
    this.store.dispatch(loadCustomerPackages());
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

  bookNow(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDER]);
  }
}
