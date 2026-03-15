import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CleansiaButtonComponent, CleansiaTitleComponent } from '@cleansia/components';
import { CustomerClient } from '@cleansia/customer-services';
import { OrderItem, OrderStatus, PaymentStatus } from '@cleansia/partner-services';
import { CleansiaCustomerRoute, SnackbarService } from '@cleansia/services';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { TagModule } from 'primeng/tag';
import { SkeletonModule } from 'primeng/skeleton';
import { TimelineModule } from 'primeng/timeline';

@Component({
  selector: 'cleansia-customer-order-detail',
  standalone: true,
  imports: [
    CommonModule,
    TranslatePipe,
    TagModule,
    SkeletonModule,
    TimelineModule,
    CleansiaButtonComponent,
    CleansiaTitleComponent,
  ],
  templateUrl: './order-detail.component.html',
})
export class OrderDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly customerClient = inject(CustomerClient);
  private readonly translate = inject(TranslateService);
  private readonly snackbar = inject(SnackbarService);

  order = signal<OrderItem | null>(null);
  loading = signal(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) {
      this.loadOrder(orderId);
    }
  }

  loadOrder(orderId: string): void {
    this.loading.set(true);
    this.customerClient.orderClient.getById(orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.message || 'Failed to load order');
        this.loading.set(false);
      },
    });
  }

  goBack(): void {
    this.router.navigate([CleansiaCustomerRoute.ORDERS]);
  }

  downloadReceipt(): void {
    const order = this.order();
    if (!order?.id) return;
    this.customerClient.orderClient.downloadReceipt(order.id).subscribe({
      next: (file) => {
        const url = URL.createObjectURL(file.data);
        const a = document.createElement('a');
        a.href = url;
        a.download = file.fileName || `receipt-${order.displayOrderNumber}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.snackbar.showError(this.translate.instant('pages.order_detail.download_error'));
      },
    });
  }

  reportIssue(): void {
    const order = this.order();
    if (!order?.id) return;
    this.router.navigate([CleansiaCustomerRoute.DISPUTES], {
      queryParams: { orderId: order.id },
    });
  }

  getOrderStatusSeverity(status: { value?: number }): string {
    switch (status?.value) {
      case OrderStatus.Pending: return 'warn';
      case OrderStatus.Confirmed: return 'info';
      case OrderStatus.InProgress: return 'info';
      case OrderStatus.Completed: return 'success';
      case OrderStatus.Cancelled: return 'danger';
      default: return 'info';
    }
  }

  getPaymentStatusSeverity(status: { value?: number }): string {
    switch (status?.value) {
      case PaymentStatus.Pending: return 'warn';
      case PaymentStatus.Paid: return 'success';
      case PaymentStatus.Failed: return 'danger';
      case PaymentStatus.Refunded: return 'info';
      case PaymentStatus.Disputed: return 'danger';
      default: return 'info';
    }
  }

  getStatusIcon(value: number): string {
    switch (value) {
      case OrderStatus.Pending: return 'pi pi-clock';
      case OrderStatus.Confirmed: return 'pi pi-check';
      case OrderStatus.InProgress: return 'pi pi-spin pi-spinner';
      case OrderStatus.Completed: return 'pi pi-check-circle';
      case OrderStatus.Cancelled: return 'pi pi-times-circle';
      default: return 'pi pi-circle';
    }
  }

  private getLocale(): string {
    const localeMap: Record<string, string> = { cs: 'cs-CZ', en: 'en-US', pl: 'pl-PL' };
    return localeMap[this.translate.currentLang] || 'en-US';
  }

  formatDate(date: Date | undefined): string {
    if (!date) return '';
    return new Date(date).toLocaleDateString(this.getLocale(), {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  formatPrice(price: number | undefined): string {
    if (price == null) return '';
    const code = this.order()?.currency?.code || 'CZK';
    return new Intl.NumberFormat(this.getLocale(), {
      style: 'currency',
      currency: code,
      minimumFractionDigits: 0,
    }).format(price);
  }
}
