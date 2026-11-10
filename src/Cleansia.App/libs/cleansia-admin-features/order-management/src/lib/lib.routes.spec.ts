import { Policy } from '@cleansia/services';
import { orderManagementRoutes } from './lib.routes';
import { RefundsComponent } from './refunds/refunds.component';

describe('order management routes', () => {
  it('opens the Refunds page at refunds, ahead of the order id route that would read it as an order', () => {
    const paths = orderManagementRoutes.map((route) => route.path);
    const refunds = orderManagementRoutes.find((route) => route.path === 'refunds');

    expect(refunds?.component).toBe(RefundsComponent);
    expect(refunds?.data?.['permission']).toBe(Policy.CanViewOrderDetailAdmin);
    expect(paths.indexOf('refunds')).toBeLessThan(paths.indexOf(':orderId'));
  });
});
