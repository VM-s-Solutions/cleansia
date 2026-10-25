import { OrderStatus, PaymentStatus } from '@cleansia/admin-services';
import { TranslateService } from '@ngx-translate/core';
import { buildFilterChips, buildFilterPayload } from './order-management.helpers';

const NOW = new Date(2026, 9, 24, 15, 30);
const START_OF_TODAY = new Date(2026, 9, 24);
const NOT_OVER = [OrderStatus.New, OrderStatus.Confirmed, OrderStatus.OnTheWay, OrderStatus.InProgress];

const translate = { instant: (key: string) => key, currentLang: 'cs' } as unknown as TranslateService;

const chipsFor = (formValues: Parameters<typeof buildFilterChips>[0]) =>
  buildFilterChips(formValues, [], [], [], translate);

describe('the order list query', () => {
  it('asks nothing about the crew while the box is off', () => {
    const payload = buildFilterPayload({ unassignedOnly: false, orderStatus: [], cleaningDateFrom: null }, NOW);

    expect(payload.isUnassigned).toBeUndefined();
    expect(payload.orderStatuses).toBeUndefined();
    expect(payload.cleaningDateFrom).toBeUndefined();
  });

  it('asks for orders with nobody on them that are not over, from the start of today, when the box is on', () => {
    expect(buildFilterPayload({ unassignedOnly: true, orderStatus: [], cleaningDateFrom: null }, NOW)).toEqual({
      isUnassigned: true,
      orderStatuses: NOT_OVER,
      cleaningDateFrom: START_OF_TODAY,
    });
  });

  it('keeps the statuses and the from-date the administrator picked', () => {
    const from = new Date(2026, 8, 1);

    expect(
      buildFilterPayload({ unassignedOnly: true, orderStatus: [OrderStatus.Cancelled], cleaningDateFrom: from }, NOW)
    ).toEqual({
      isUnassigned: true,
      orderStatuses: [OrderStatus.Cancelled],
      cleaningDateFrom: from,
    });
  });

  it('carries the other filters through unchanged with the box on', () => {
    const payload = buildFilterPayload(
      {
        unassignedOnly: true,
        paymentStatus: [PaymentStatus.Pending],
        searchTerm: ' ORD-7 ',
        cleaningDateTo: new Date(2026, 9, 31),
        currencyId: 'cur-czk',
      },
      NOW
    );

    expect(payload).toEqual({
      isUnassigned: true,
      orderStatuses: NOT_OVER,
      paymentStatuses: [PaymentStatus.Pending],
      searchTerm: 'ORD-7',
      cleaningDateFrom: START_OF_TODAY,
      cleaningDateTo: new Date(2026, 9, 31),
      currencyId: 'cur-czk',
    });
  });
});

describe('the order list chips', () => {
  it('names the staffing filter in one chip while the box is on', () => {
    expect(chipsFor({ unassignedOnly: true })).toEqual([
      {
        key: 'unassignedOnly',
        label: 'pages.order_management.filters.staffing',
        value: 'pages.order_management.filters.no_cleaner',
      },
    ]);
  });

  it('shows no staffing chip while the box is off', () => {
    expect(chipsFor({ unassignedOnly: false })).toEqual([]);
  });
});
