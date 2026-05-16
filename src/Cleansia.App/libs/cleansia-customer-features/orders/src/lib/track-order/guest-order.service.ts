import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export interface GuestOrder {
  orderId: string;
  email: string;
  createdAt: string;
}

const STORAGE_KEY = 'cleansia_guest_orders';
const MAX_ORDERS = 5;

@Injectable({ providedIn: 'root' })
export class GuestOrderService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  save(orderId: string, email: string): void {
    if (!this.isBrowser) return;
    const orders = this.getAll();
    // Avoid duplicates
    if (orders.some((o) => o.orderId === orderId)) return;
    orders.unshift({ orderId, email, createdAt: new Date().toISOString() });
    // Keep only the most recent
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(orders.slice(0, MAX_ORDERS))
    );
  }

  getAll(): GuestOrder[] {
    if (!this.isBrowser) return [];
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : [];
    } catch {
      return [];
    }
  }

  clear(): void {
    if (!this.isBrowser) return;
    localStorage.removeItem(STORAGE_KEY);
  }

  hasOrders(): boolean {
    return this.getAll().length > 0;
  }
}
