import { OrderItem } from '@cleansia/customer-services';
import { OrderStatus } from '@cleansia/models';

type StatusRow = NonNullable<OrderItem['statusHistory']>[number];

/**
 * When a booking reached `status` in its latest pass: the rows from its last New on, since the only New
 * after the first is the walk-back when its last cleaner leaves (ADR-0067). New itself keeps the booking
 * time. The server sends the rows in no guaranteed order, so they are sorted first.
 */
export function statusReachedAt(history: readonly StatusRow[] | undefined, status: number): Date | undefined {
  const rows = [...(history ?? [])].sort((a, b) => timeOf(a) - timeOf(b));
  const isNew = (row: StatusRow) => row.status?.value === OrderStatus.New;
  if (status === OrderStatus.New) {
    return rows.find(isNew)?.createdOn;
  }
  let latestPass = 0;
  rows.forEach((row, index) => {
    if (isNew(row)) latestPass = index;
  });
  return rows.slice(latestPass).find((row) => row.status?.value === status)?.createdOn;
}

function timeOf(row: StatusRow): number {
  return row.createdOn ? new Date(row.createdOn).getTime() : 0;
}
