import { OrderItem } from '@cleansia/customer-services';

/** One crew member's acceptance of the contract for work, as the customer's detail states it. */
export interface WorkContractAcceptanceLine {
  id: string;
  cleanerName: string;
  acceptedOn: Date;
  documentVersion: string;
}

/**
 * The acceptance carries no name; the crew entry whose id is the acceptance's seat does, already
 * masked for the customer's eyes. Pairing them here keeps one masking path.
 */
export function buildWorkContractAcceptanceLines(
  order: OrderItem | null | undefined,
): WorkContractAcceptanceLine[] {
  if (!order) return [];
  const crew = order.assignedEmployees ?? [];
  return (order.workContractAcceptances ?? []).flatMap((acceptance) => {
    const seat = crew.find((entry) => entry.id === acceptance.orderEmployeeId);
    if (!seat || !acceptance.id) return [];
    return [
      {
        id: acceptance.id,
        cleanerName: seat.fullName ?? '',
        acceptedOn: acceptance.acceptedOn,
        documentVersion: acceptance.documentVersion ?? '',
      },
    ];
  });
}
