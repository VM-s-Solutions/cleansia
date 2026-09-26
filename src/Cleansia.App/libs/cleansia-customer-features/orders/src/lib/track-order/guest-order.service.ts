import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export interface GuestOrder {
  orderId: string;
  accessToken: string;
  createdAt: string;
}

const STORAGE_KEY = 'cleansia_guest_orders';
const MAX_ORDERS = 5;

/**
 * The bookings this browser can still PROVE. An entry is the order's id and the access token that
 * arrived with the guest's e-mail link — the credential is what makes the entry worth keeping, so an
 * entry without one (a bundle written by an earlier release) is dropped on read.
 */
@Injectable({ providedIn: 'root' })
export class GuestOrderService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  save(orderId: string, accessToken: string): void {
    if (!this.isBrowser) return;
    const orders = this.getAll().filter((o) => o.orderId !== orderId);
    orders.unshift({ orderId, accessToken, createdAt: new Date().toISOString() });
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify(orders.slice(0, MAX_ORDERS))
    );
  }

  getAll(): GuestOrder[] {
    if (!this.isBrowser) return [];
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      const stored = raw ? (JSON.parse(raw) as GuestOrder[]) : [];
      return stored.filter((o) => !!o?.orderId && !!o?.accessToken);
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
