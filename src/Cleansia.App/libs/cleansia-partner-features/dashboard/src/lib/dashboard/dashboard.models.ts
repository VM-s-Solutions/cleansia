import { Code } from '@cleansia/partner-services';

export interface StatCard {
  title: string;
  value: number | string;
  icon: string;
  route?: string;
  trend?: {
    value: number;
    direction: 'up' | 'down' | 'neutral';
  };
}

/** An upcoming order as the dashboard card prints it: dates and money already in the session language. */
export interface UpcomingOrderCard {
  id: string;
  displayOrderNumber: string;
  orderStatus: Code;
  customerName: string;
  cleaningDate: string;
  customerAddress: string;
  totalPrice: string;
}
